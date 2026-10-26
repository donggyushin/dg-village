from collections.abc import AsyncIterator
from contextlib import asynccontextmanager
from typing import Literal

from fastapi import FastAPI, Request
from pydantic import BaseModel, ConfigDict, Field
from sqlalchemy import select

from .config import Settings
from .db import Server, create_engine, create_sessionmaker


class HealthOut(BaseModel):
    status: Literal["ok"] = Field(description='Always "ok" while the server is up')


class ServerOut(BaseModel):
    """A server as clients see it."""

    model_config = ConfigDict(from_attributes=True)

    id: int = Field(description="Server id", examples=[1])
    name: str = Field(description="Server name shown to players", examples=["square"])


def create_app(settings: Settings | None = None) -> FastAPI:
    settings = settings or Settings.from_env()

    @asynccontextmanager
    async def lifespan(app: FastAPI) -> AsyncIterator[None]:
        engine = create_engine(settings.database_url)
        app.state.sessionmaker = create_sessionmaker(engine)
        yield
        await engine.dispose()

    app = FastAPI(lifespan=lifespan)

    @app.get("/health", summary="Health check")
    async def health() -> HealthOut:
        """Responds as long as the server process is up. Does not check the database."""
        return HealthOut(status="ok")

    @app.get("/servers", summary="List servers")
    async def list_servers(request: Request) -> list[ServerOut]:
        """All servers, ordered by id."""
        async with request.app.state.sessionmaker() as session:
            servers = await session.scalars(select(Server).order_by(Server.id))
            return [ServerOut.model_validate(s) for s in servers]

    return app


app = create_app()
