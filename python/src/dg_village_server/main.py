import uuid
from collections.abc import AsyncIterator
from contextlib import asynccontextmanager
from datetime import datetime
from typing import Annotated, Literal

from fastapi import FastAPI, Header, HTTPException, Request, status
from pydantic import BaseModel, ConfigDict, Field, StringConstraints
from sqlalchemy import select
from sqlalchemy.exc import IntegrityError

from .config import Settings
from .db import Character, Server, create_engine, create_sessionmaker

# Hangul syllables, ASCII letters and digits. No spaces, so names can't look alike by spacing.
Nickname = Annotated[
    str, StringConstraints(strip_whitespace=True, pattern=r"^[0-9A-Za-z가-힣]{2,12}$")
]
SpriteSetId = Annotated[str, StringConstraints(pattern=r"^[a-z0-9_]{1,64}$")]
AccountId = Annotated[
    uuid.UUID,
    Header(
        alias="X-Account-Id",
        description="Random id the client generates once and keeps (no login yet)",
    ),
]


class HealthOut(BaseModel):
    status: Literal["ok"] = Field(description='Always "ok" while the server is up')


class ServerOut(BaseModel):
    """A server as clients see it."""

    model_config = ConfigDict(from_attributes=True)

    id: int = Field(description="Server id", examples=[1])
    name: str = Field(description="Server name shown to players", examples=["square"])


class CharacterIn(BaseModel):
    """A character to create."""

    nickname: Nickname = Field(
        description="2-12 Hangul syllables, ASCII letters or digits; unique per server",
        examples=["라벤더"],
    )
    sprite_set_id: SpriteSetId = Field(
        description="Client CharacterSpriteSet id", examples=["lavender_girl"]
    )


class CharacterOut(BaseModel):
    """A character as its owner sees it."""

    model_config = ConfigDict(from_attributes=True)

    id: int = Field(description="Character id", examples=[1])
    server_id: int = Field(description="Server the character lives on", examples=[1])
    nickname: str = Field(description="Nickname, unique per server", examples=["라벤더"])
    sprite_set_id: str = Field(
        description="Client CharacterSpriteSet id", examples=["lavender_girl"]
    )
    created_at: datetime = Field(description="When the character was created")


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

    @app.get("/servers/{server_id}/characters", summary="List my characters")
    async def list_characters(
        request: Request, server_id: int, account_id: AccountId
    ) -> list[CharacterOut]:
        """The caller's characters on this server, oldest first."""
        async with request.app.state.sessionmaker() as session:
            if await session.get(Server, server_id) is None:
                raise HTTPException(status.HTTP_404_NOT_FOUND, "Server not found")
            characters = await session.scalars(
                select(Character)
                .where(Character.server_id == server_id, Character.account_id == account_id)
                .order_by(Character.id)
            )
            return [CharacterOut.model_validate(c) for c in characters]

    @app.post(
        "/servers/{server_id}/characters",
        summary="Create a character",
        status_code=status.HTTP_201_CREATED,
        responses={409: {"description": "Nickname already taken on this server"}},
    )
    async def create_character(
        request: Request, server_id: int, account_id: AccountId, body: CharacterIn
    ) -> CharacterOut:
        """Creates a character for the caller on this server."""
        async with request.app.state.sessionmaker() as session:
            if await session.get(Server, server_id) is None:
                raise HTTPException(status.HTTP_404_NOT_FOUND, "Server not found")
            character = Character(
                server_id=server_id,
                account_id=account_id,
                nickname=body.nickname,
                sprite_set_id=body.sprite_set_id,
            )
            session.add(character)
            try:
                await session.commit()
            except IntegrityError:
                # Checking first would still race with another request; the unique
                # constraint is the real check.
                raise HTTPException(status.HTTP_409_CONFLICT, "Nickname already taken") from None
            await session.refresh(character)
            return CharacterOut.model_validate(character)

    return app


app = create_app()
