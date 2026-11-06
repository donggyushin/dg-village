import asyncio
import logging
import time
import uuid
from collections.abc import AsyncIterator
from contextlib import asynccontextmanager
from dataclasses import dataclass
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
from pydantic import BaseModel, ValidationError
from sqlalchemy import select, update
from sqlalchemy.exc import IntegrityError

from .config import Settings
from .db import Account, Character, Server, create_engine, create_sessionmaker
from .passwords import hash_password, verify_password
from .protocol import (
    ChangeSceneIn,
    ChatIn,
    ChatMessageOut,
    ClientMessage,
    ErrorCode,
    ErrorOut,
    JoinIn,
    MoveIn,
    PlayerJoinedOut,
    PlayerLeftOut,
    PlayerMovedOut,
    PlayerOut,
    SceneChangedOut,
    ScenePlayersOut,
    WelcomeOut,
)
from .schemas import AccountOut, CharacterIn, CharacterOut, CredentialsIn, HealthOut, ServerOut

AccountId = Annotated[
    uuid.UUID,
    Header(
        alias="X-Account-Id",
        description="account_id from POST /signup or /login",
    ),
]

logger = logging.getLogger(__name__)

JOIN_TIMEOUT_SECONDS = 10
CHAT_MIN_INTERVAL_SECONDS = 0.5


@dataclass
class Player:
    """A character in the game. Gone on disconnect; only its scene and position are saved."""

    websocket: WebSocket
    id: int
    # Players only meet within one server. A server is a channel, not a separate process.
    server_id: int
    nickname: str
    sprite_set_id: str
    scene: str
    # Latest position in the current scene's world coordinates. Starts at the saved position.
    # Moves are too frequent to save, so it reaches the database on disconnect.
    position: tuple[float, float, float]
    # time.monotonic() of the last accepted chat, to refuse flooding.
    last_chat_at: float = float("-inf")

    def to_out(self) -> PlayerOut:
        x, y, z = self.position
        return PlayerOut(
            id=self.id, nickname=self.nickname, sprite_set_id=self.sprite_set_id, x=x, y=y, z=z
        )


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
    players: dict[int, Player] = {}

    @app.get("/health", summary="Health check")
    async def health() -> HealthOut:
        """Responds as long as the server process is up. Does not check the database."""
        return HealthOut(status="ok")

    @app.post(
        "/signup",
        summary="Create an account",
        status_code=status.HTTP_201_CREATED,
        responses={409: {"description": "Username already taken"}},
    )
    async def signup(request: Request, body: CredentialsIn) -> AccountOut:
        """Creates an account and returns its id, so the client is signed in right away."""
        # scrypt takes tens of ms; a thread keeps the game loop moving meanwhile.
        password_hash = await asyncio.to_thread(hash_password, body.password)
        async with request.app.state.sessionmaker() as session:
            account = Account(username=body.username, password_hash=password_hash)
            session.add(account)
            try:
                await session.commit()
            except IntegrityError:
                raise HTTPException(status.HTTP_409_CONFLICT, "Username already taken") from None
            return AccountOut(account_id=account.id)

    @app.post(
        "/login",
        summary="Sign in",
        responses={401: {"description": "Wrong username or password"}},
    )
    async def login(request: Request, body: CredentialsIn) -> AccountOut:
        """Returns the account id for this username and password."""
        async with request.app.state.sessionmaker() as session:
            account = await session.scalar(select(Account).where(Account.username == body.username))
        if account is None or not await asyncio.to_thread(
            verify_password, body.password, account.password_hash
        ):
            # Same answer for both, so it doesn't tell which usernames exist.
            raise HTTPException(status.HTTP_401_UNAUTHORIZED, "Wrong username or password")
        return AccountOut(account_id=account.id)

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

    @app.delete(
        "/servers/{server_id}/characters/{character_id}",
        summary="Delete a character",
        status_code=status.HTTP_204_NO_CONTENT,
        responses={
            404: {"description": "No such character on this server and account"},
            409: {"description": "Character is in the game"},
        },
    )
    async def delete_character(
        request: Request, server_id: int, character_id: int, account_id: AccountId
    ) -> None:
        """Permanently deletes one of the caller's characters."""
        async with request.app.state.sessionmaker() as session:
            character = await session.get(Character, character_id)
            # Someone else's character gets the same 404, so ids can't be probed.
            if (
                character is None
                or character.server_id != server_id
                or character.account_id != account_id
            ):
                raise HTTPException(status.HTTP_404_NOT_FOUND, "Character not found")
            if character_id in players:
                raise HTTPException(status.HTTP_409_CONFLICT, "Character is in the game")
            await session.delete(character)
            await session.commit()

    async def send_error(websocket: WebSocket, code: ErrorCode, message: str) -> None:
        await websocket.send_text(ErrorOut(code=code, message=message).model_dump_json())

    def players_in(server_id: int, scene: str) -> list[Player]:
        """Everyone in this scene of this server: the players who can see each other."""
        return [p for p in players.values() if p.server_id == server_id and p.scene == scene]

    async def broadcast(
        server_id: int, scene: str, message: BaseModel, exclude: Player | None = None
    ) -> None:
        """Sends to everyone in the scene of this server but `exclude`, all at once so a
        slow client doesn't hold up the others. A failed send is ignored: that client's own
        handler sees the disconnect and cleans up."""
        text = message.model_dump_json()
        targets = [p for p in players_in(server_id, scene) if p is not exclude]
        await asyncio.gather(
            *(p.websocket.send_text(text) for p in targets), return_exceptions=True
        )

    async def enter_scene(player: Player) -> None:
        """Tells the player who is already in its scene, and the others that it arrived."""
        others = [p.to_out() for p in players_in(player.server_id, player.scene) if p is not player]
        await player.websocket.send_text(ScenePlayersOut(players=others).model_dump_json())
        joined = PlayerJoinedOut(player=player.to_out())
        await broadcast(player.server_id, player.scene, joined, exclude=player)

    async def save_position(player: Player) -> None:
        x, y, z = player.position
        async with app.state.sessionmaker() as session:
            await session.execute(
                update(Character)
                .where(Character.id == player.id)
                .values(last_x=x, last_y=y, last_z=z)
            )
            await session.commit()

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
            if character is not None and character.account_id != join.account_id:
                character = None
            connected = character is not None and character.id in players
            if character is not None and not connected:
                # The first read may have raced the previous connection's position save.
                # A player leaves `players` only after that save, so this read has it.
                await session.refresh(character)
        if character is None:
            await reject(websocket, "character_not_found", "No such character on this account")
            return None
        # Checked again: another join may have entered while the database was being read.
        if connected or character.id in players:
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

        player = Player(
            websocket=websocket,
            id=character.id,
            server_id=character.server_id,
            nickname=character.nickname,
            sprite_set_id=character.sprite_set_id,
            scene=character.last_visited_scene,
            position=(character.last_x, character.last_y, character.last_z),
        )
        players[player.id] = player
        try:
            welcome = WelcomeOut(character=CharacterOut.model_validate(character))
            await websocket.send_text(welcome.model_dump_json())
            await enter_scene(player)
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
                    case ChangeSceneIn(scene=scene, x=x, y=y, z=z):
                        # Saved right away rather than on disconnect: scene changes are
                        # rare, and a server crash would skip the disconnect save. One
                        # UPDATE, so the scene and its position can't get out of step.
                        async with app.state.sessionmaker() as session:
                            await session.execute(
                                update(Character)
                                .where(Character.id == player.id)
                                .values(last_visited_scene=scene, last_x=x, last_y=y, last_z=z)
                            )
                            await session.commit()
                        old_scene = player.scene
                        player.scene = scene
                        player.position = (x, y, z)
                        left = PlayerLeftOut(id=player.id)
                        await broadcast(player.server_id, old_scene, left, exclude=player)
                        changed = SceneChangedOut(scene=scene, x=x, y=y, z=z)
                        await websocket.send_text(changed.model_dump_json())
                        await enter_scene(player)
                    case MoveIn(x=x, y=y, z=z):
                        player.position = (x, y, z)
                        moved = PlayerMovedOut(id=player.id, x=x, y=y, z=z)
                        await broadcast(player.server_id, player.scene, moved, exclude=player)
                    case ChatIn(text=chat_text):
                        now = time.monotonic()
                        if now - player.last_chat_at < CHAT_MIN_INTERVAL_SECONDS:
                            await send_error(websocket, "chat_too_fast", "Chat sent too fast")
                            continue
                        player.last_chat_at = now
                        chat = ChatMessageOut(
                            id=player.id, nickname=player.nickname, text=chat_text
                        )
                        # The speaker gets it too: everyone sees the same order, and the
                        # speaker sees only what the server accepted.
                        await broadcast(player.server_id, player.scene, chat)
        except WebSocketDisconnect:
            pass
        finally:
            # Saved while still in `players`: receive_join relies on this to read the new position.
            try:
                await save_position(player)
            except Exception:
                logger.exception("Failed to save position of character %s", player.id)
            finally:
                # Even if the save failed: staying in `players` would lock the character out.
                del players[player.id]
                left = PlayerLeftOut(id=player.id)
                await broadcast(player.server_id, player.scene, left, exclude=player)

    return app


app = create_app()
