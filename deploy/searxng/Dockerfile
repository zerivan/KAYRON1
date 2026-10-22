FROM ghcr.io/searxng/searxng:latest

# The official SearXNG image supplies the engine and its Python environment.
# The custom gateway exposes only a key-protected JSON search endpoint.
COPY --chown=977:977 settings.yml /etc/searxng/settings.yml
COPY proxy.py /opt/kayron-searxng/proxy.py

ENV PYTHONUNBUFFERED=1
ENTRYPOINT ["/usr/local/searxng/.venv/bin/python", "/opt/kayron-searxng/proxy.py"]
