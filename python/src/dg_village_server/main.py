from collections.abc import AsyncIterator
from contextlib import asynccontextmanager

from fastapi import FastAPI, Request
from sqlalchemy import select

from .config import Settings
from .db import Server, create_engine, create_sessionmaker


def create_app(settings: Settings | None = None) -> FastAPI:
    settings = settings or Settings.from_env()

    @asynccontextmanager
    async def lifespan(app: FastAPI) -> AsyncIterator[None]:
        engine = create_engine(settings.database_url)
        app.state.sessionmaker = create_sessionmaker(engine)
        yield
        await engine.dispose()

    app = FastAPI(lifespan=lifespan)

    @app.get("/health")
    async def health() -> dict[str, str]:
        return {"status": "ok"}

    @app.get("/servers")
    async def list_servers(request: Request) -> list[dict[str, int | str]]:
        async with request.app.state.sessionmaker() as session:
            servers = await session.scalars(select(Server).order_by(Server.id))
            return [{"id": s.id, "name": s.name} for s in servers]

    return app


app = create_app()
