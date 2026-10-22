#!/usr/local/searxng/.venv/bin/python
"""Small API-key gateway for a private KAYRON-owned SearXNG instance."""
from __future__ import annotations

import hmac
import json
import os
import signal
import subprocess
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.error import HTTPError, URLError
from urllib.parse import parse_qs, urlencode, urlsplit
from urllib.request import Request, urlopen

UPSTREAM_HOST = "127.0.0.1"
UPSTREAM_PORT = 8080
UPSTREAM_URL = f"http://{UPSTREAM_HOST}:{UPSTREAM_PORT}"
PUBLIC_PORT = int(os.environ.get("PORT", "10000"))
API_KEY = os.environ.get("KAYRON_SEARCH_API_KEY", "")
MAX_QUERY_LENGTH = 512
UPSTREAM_TIMEOUT = 18
MAX_RESPONSE_BYTES = 2 * 1024 * 1024


def json_bytes(payload: object) -> bytes:
    return json.dumps(payload, ensure_ascii=False, separators=(",", ":")).encode("utf-8")


class GatewayHandler(BaseHTTPRequestHandler):
    server_version = "KAYRON-Search-Gateway"
    sys_version = ""

    def _send(self, status: int, body: bytes, content_type: str = "application/json; charset=utf-8") -> None:
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Referrer-Policy", "no-referrer")
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self) -> None:
        path = urlsplit(self.path).path
        if path == "/healthz":
            self._send(200, json_bytes({"status": "ok"}))
            return

        if path != "/search":
            self._send(404, json_bytes({"error": "not_found"}))
            return

        supplied = self.headers.get("X-KAYRON-Search-Key", "")
        if not API_KEY or not hmac.compare_digest(supplied, API_KEY):
            self._send(401, json_bytes({"error": "unauthorized"}))
            return

        try:
            query = parse_qs(urlsplit(self.path).query, keep_blank_values=False, max_num_fields=12)
        except ValueError:
            self._send(400, json_bytes({"error": "invalid_query"}))
            return

        term = query.get("q", [""])[0].strip()
        if not term or len(term) > MAX_QUERY_LENGTH:
            self._send(400, json_bytes({"error": "query_required_or_too_long"}))
            return

        language = query.get("language", ["pt-BR"])[0][:20]
        params = urlencode({"q": term, "format": "json", "language": language})
        request = Request(
            f"{UPSTREAM_URL}/search?{params}",
            headers={"Accept": "application/json", "User-Agent": "KAYRON-Search/1.0"},
            method="GET",
        )
        try:
            with urlopen(request, timeout=UPSTREAM_TIMEOUT) as response:
                content = response.read(MAX_RESPONSE_BYTES + 1)
                if len(content) > MAX_RESPONSE_BYTES:
                    self._send(502, json_bytes({"error": "upstream_response_too_large"}))
                    return
                self._send(response.status, content, response.headers.get("Content-Type", "application/json; charset=utf-8"))
        except HTTPError as exc:
            self._send(502, json_bytes({"error": "upstream_http_error", "status": exc.code}))
        except (URLError, TimeoutError, OSError) as exc:
            print(f"Upstream search failed: {type(exc).__name__}", file=sys.stderr, flush=True)
            self._send(502, json_bytes({"error": "upstream_unavailable"}))

    def do_POST(self) -> None:
        self._send(405, json_bytes({"error": "method_not_allowed"}))

    def log_message(self, fmt: str, *args: object) -> None:
        # Avoid logging queries, headers, API keys, or other user-provided content.
        print(f"gateway client={self.client_address[0]} response={args[1] if len(args) > 1 else 'n/a'}", flush=True)


def main() -> int:
    if len(API_KEY) < 32:
        print("KAYRON_SEARCH_API_KEY must be configured (minimum 32 characters).", file=sys.stderr, flush=True)
        return 2
    if not os.environ.get("SEARXNG_SECRET"):
        print("SEARXNG_SECRET must be configured.", file=sys.stderr, flush=True)
        return 2

    engine_env = os.environ.copy()
    engine_env["SEARXNG_PORT"] = str(UPSTREAM_PORT)
    engine_env["GRANIAN_PORT"] = str(UPSTREAM_PORT)
    engine_env["GRANIAN_HOST"] = UPSTREAM_HOST
    engine = subprocess.Popen(
        ["/usr/local/searxng/entrypoint.sh"],
        env=engine_env,
        stdin=subprocess.DEVNULL,
    )

    ready = False
    for _ in range(90):
        if engine.poll() is not None:
            print(f"SearXNG exited during startup with code {engine.returncode}.", file=sys.stderr, flush=True)
            return int(engine.returncode or 1)
        try:
            with urlopen(f"{UPSTREAM_URL}/", timeout=1) as response:
                if 200 <= response.status < 500:
                    ready = True
                    break
        except (URLError, TimeoutError, OSError):
            pass
        time.sleep(1)

    if not ready:
        engine.terminate()
        print("SearXNG did not become ready within 90 seconds.", file=sys.stderr, flush=True)
        return 1

    server = ThreadingHTTPServer(("0.0.0.0", PUBLIC_PORT), GatewayHandler)
    server.daemon_threads = True
    print(f"KAYRON SearXNG gateway listening on port {PUBLIC_PORT}; search API is key-protected.", flush=True)

    def stop(_signum: int, _frame: object) -> None:
        threading.Thread(target=server.shutdown, daemon=True).start()
        try:
            engine.terminate()
        except OSError:
            pass

    signal.signal(signal.SIGTERM, stop)
    signal.signal(signal.SIGINT, stop)
    try:
        server.serve_forever(poll_interval=0.5)
    finally:
        server.server_close()
        if engine.poll() is None:
            engine.terminate()
            try:
                engine.wait(timeout=5)
            except subprocess.TimeoutExpired:
                engine.kill()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
