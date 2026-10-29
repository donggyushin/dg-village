import asyncio
import uuid
from collections.abc import AsyncIterator
from contextlib import asynccontextmanager
from typing import Annotated

from fastapi import (
    FastAPI,
    Header,
    HTTPException,
    Request,
    WebSocket,
    WebSocketDisconnect,
    status,
)
from pydantic import ValidationError
from sqlalchemy import select, update
from sqlalchemy.exc import IntegrityError

from .config import Settings
from .db import Character, Server, create_engine, create_sessionmaker
from .protocol import (
    ChangeSceneIn,
    ClientMessage,
    ErrorCode,
    ErrorOut,
    JoinIn,
    MoveIn,
    SceneChangedOut,
    WelcomeOut,
)
from .schemas import CharacterIn, CharacterOut, HealthOut, ServerOut

AccountId = Annotated[
    uuid.UUID,
    Header(
        alias="X-Account-Id",
        description="Random id the client generates once and keeps (no login yet)",
    ),
]

JOIN_TIMEOUT_SECONDS = 10

DESCRIPTION = """\
REST endpoints for the lobby. The in-game connection is a WebSocket at `/ws`, which this
page can't show: see `docs/protocol.md` in the repo.
"""


def create_app(settings: Settings | None = None) -> FastAPI:
    settings = settings or Settings.from_env()

    @asynccontextmanager
    async def lifespan(app: FastAPI) -> AsyncIterator[None]:
        engine = create_engine(settings.database_url)
        app.state.sessionmaker = create_sessionmaker(engine)
        yield
        await engine.dispose()

    app = FastAPI(lifespan=lifespan, description=DESCRIPTION)
    # Characters in the game, by character id. Only touched from the event loop, so a
    # check followed by an insert with no await in between can't race.
    players: dict[int, WebSocket] = {}
    # Latest (x, y, z) per character. Memory only: moves are too frequent to save each one.
    positions: dict[int, tuple[float, float, float]] = {}

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

    async def send_error(websocket: WebSocket, code: ErrorCode, message: str) -> None:
        await websocket.send_text(ErrorOut(code=code, message=message).model_dump_json())

    async def reject(websocket: WebSocket, code: ErrorCode, message: str) -> None:
        await send_error(websocket, code, message)
        await websocket.close(status.WS_1008_POLICY_VIOLATION)

    async def receive_join(websocket: WebSocket) -> Character | None:
        """Waits for the join message and checks it. None means the connection is over."""
        try:
            async with asyncio.timeout(JOIN_TIMEOUT_SECONDS):
                text = await websocket.receive_text()
        except TimeoutError:
            await reject(websocket, "join_timeout", "No join message in time")
            return None
        except WebSocketDisconnect:
            return None

        try:
            join = ClientMessage.validate_json(text)
        except ValidationError as e:
            await reject(websocket, "invalid_message", str(e))
            return None
        if not isinstance(join, JoinIn):
            await reject(websocket, "not_joined", "The first message must be join")
            return None

        async with app.state.sessionmaker() as session:
            character = await session.get(Character, join.character_id)
        if character is None or character.account_id != join.account_id:
            await reject(websocket, "character_not_found", "No such character on this account")
            return None
        if character.id in players:
            await reject(websocket, "already_connected", "Character is already in the game")
            return None
        return character

    @app.websocket("/ws")
    async def game_socket(websocket: WebSocket) -> None:
        """In-game connection. Protocol: docs/protocol.md."""
        await websocket.accept()
        character = await receive_join(websocket)
        if character is None:
            return

        players[character.id] = websocket
        try:
            welcome = WelcomeOut(character=CharacterOut.model_validate(character))
            await websocket.send_text(welcome.model_dump_json())
            while True:
                text = await websocket.receive_text()
                try:
                    message = ClientMessage.validate_json(text)
                except ValidationError as e:
                    await send_error(websocket, "invalid_message", str(e))
                    continue
                match message:
                    case JoinIn():
                        await send_error(websocket, "already_joined", "Already in the game")
                    case ChangeSceneIn(scene=scene):
                        # Saved right away rather than on disconnect: scene changes are
                        # rare, and a server crash would skip the disconnect save.
                        async with app.state.sessionmaker() as session:
                            await session.execute(
                                update(Character)
                                .where(Character.id == character.id)
                                .values(last_visited_scene=scene)
                            )
                            await session.commit()
                        await websocket.send_text(SceneChangedOut(scene=scene).model_dump_json())
                    case MoveIn(x=x, y=y, z=z):
                        positions[character.id] = (x, y, z)
        except WebSocketDisconnect:
            pass
        finally:
            del players[character.id]
            positions.pop(character.id, None)

    return app


app = create_app()
