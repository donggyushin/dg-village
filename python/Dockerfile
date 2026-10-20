# Game server image. Built by deploy/compose.yaml; see deploy/README.md.
FROM python:3.13-slim

COPY --from=ghcr.io/astral-sh/uv:0.8.15 /uv /bin/uv

WORKDIR /app
ENV UV_COMPILE_BYTECODE=1 \
    UV_LINK_MODE=copy \
    PYTHONUNBUFFERED=1 \
    PATH="/app/.venv/bin:$PATH"

# Dependencies first so code changes don't reinstall them.
COPY pyproject.toml uv.lock ./
RUN uv sync --locked --no-dev --no-install-project

COPY alembic.ini ./
COPY migrations ./migrations
COPY src ./src
RUN uv sync --locked --no-dev

EXPOSE 8000

HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=3 \
    CMD python -c "import urllib.request; urllib.request.urlopen('http://127.0.0.1:8000/health')"

# Migrate before serving: the server needs its tables at startup (ensure_default_channel).
# Exactly one process: rooms live in memory, so a second worker would split the players.
CMD ["sh", "-c", "alembic upgrade head && exec uvicorn dg_village_server.main:app --host 0.0.0.0 --port 8000 --proxy-headers --forwarded-allow-ips '*'"]
