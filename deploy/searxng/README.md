# KAYRON SearXNG gateway (Render)

This branch adds an isolated, API-key-protected SearXNG service for the local KAYRON search engine.

## Architecture

- Upstream engine image: `ghcr.io/searxng/searxng:latest`
- SearXNG listens on container loopback port 8080.
- Only `GET /search` is exposed through a standard-library Python gateway.
- Search requires the secret `X-KAYRON-Search-Key` header.
- `GET /healthz` is intentionally public and reveals only process health.
- JSON output is enabled in `settings.yml`.
- Queries, headers, and the API key are not written to logs.

## Render configuration

Create a Docker web service from this repository branch, using:

- Docker context: `deploy/searxng`
- Dockerfile: `deploy/searxng/Dockerfile`
- Plan: Free for initial validation
- Environment variables:
  - `SEARXNG_SECRET`: a random secret
  - `KAYRON_SEARCH_API_KEY`: a random secret of at least 32 characters

Render supplies `PORT`; the gateway listens on it automatically. The service may spin down on the free plan, so the first request after idle can fall back to the local index while the service wakes up.

## KAYRON client

Set `KAYRON_SEARXNG_URL` to the service's HTTPS base URL and `KAYRON_SEARXNG_API_KEY` to the same key as `KAYRON_SEARCH_API_KEY`. The key must never be committed or included in normal logs.

The SearXNG upstream is not directly bound to the public network. Do not replace the gateway with the upstream container's bare public port.
