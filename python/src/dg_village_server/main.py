import asyncio
import logging
import math
import time
import uuid
from collections.abc import AsyncIterator
from contextlib import asynccontextmanager

from fastapi import FastAPI, WebSocket, WebSocketDisconnect, status
from pydantic import ValidationError

from . import api, positions
from .auth import AuthError, read_access_token
from .config import Settings
from .db import (
    Base,
    Channel,
    User,
    create_engine,
    create_sessionmaker,
    ensure_default_channel,
)
from .maps import load_maps
from .protocol import (
    ChatMessage,
    ErrorMessage,
    JoinMessage,
    MoveMessage,
    SitMessage,
    StandMessage,
    TravelMessage,
    WelcomeMessage,
    client_message_adapter,
)
from .room import Room, RoomManager

TICK_RATE_HZ = 10
CHAT_MIN_INTERVAL_SECONDS = 0.5

logger = logging.getLogger(__name__)


def create_app(settings: Settings | None = None, *, create_tables: bool = False) -> FastAPI:
    """create_tables is for tests (in-memory SQLite); real databases use Alembic migrations."""
    settings = settings or Settings.from_env()

    @asynccontextmanager
    async def lifespan(app: FastAPI) -> AsyncIterator[None]:
        engine = create_engine(settings.database_url)
        if create_tables:
            async with engine.begin() as conn:
                await conn.run_sync(Base.metadata.create_all)
        app.state.settings = settings
        app.state.sessionmaker = create_sessionmaker(engine)
        await ensure_default_channel(app.state.sessionmaker)
        # Every channel has the same maps (office + outdoor); chat goes to everyone on the map
        rooms = RoomManager(load_maps())
        app.state.rooms = rooms
        tick_task = asyncio.create_task(rooms.run_tick_loop(1 / TICK_RATE_HZ))
        yield
        tick_task.cancel()
        # Deploys restart the server: keep the spot of anyone still connected
        await positions.save(app.state.sessionmaker, rooms.everyone())
        await engine.dispose()

    app = FastAPI(title="dg-village server", lifespan=lifespan)
    app.include_router(api.router)

    @app.get("/health")
    async def health() -> dict[str, str]:
        return {"status": "ok"}

    @app.websocket("/ws")
    async def websocket_endpoint(ws: WebSocket) -> None:
        await ws.accept()
        rooms: RoomManager = ws.app.state.rooms

        # The first message must be a join.
        try:
            first = client_message_adapter.validate_json(await ws.receive_text())
        except WebSocketDisconnect:
            return
        except ValidationError as e:
            await _reject(ws, f"invalid message: {e.errors()[0]['msg']}")
            return
        if not isinstance(first, JoinMessage):
            await _reject(ws, "first message must be 'join'")
            return

        try:
            user_id = read_access_token(first.token, settings)
        except AuthError as e:
            await _reject(ws, str(e))
            return
        try:
            channel_id = uuid.UUID(hex=first.room)
        except ValueError:
            channel_id = None
        async with ws.app.state.sessionmaker() as session:
            user = await session.get(User, user_id)
            channel = await session.get(Channel, channel_id) if channel_id else None
        if user is None:
            await _reject(ws, "unknown user")
            return
        if channel is None:
            await _reject(ws, "unknown channel")
            return
        sessionmaker = ws.app.state.sessionmaker
        if first.map is None:
            map_id, x, z = await positions.load_start(sessionmaker, rooms.maps, user.id, channel.id)
        elif first.map in rooms.maps:
            map_id, x, z = first.map, first.x, first.z
        else:
            await _reject(ws, "unknown map")
            return

        room, me = await rooms.join(
            channel.id.hex,
            map_id,
            user.id.hex,
            user.display_name,
            user.character_id,
            ws,
            (x, z),
        )
        last_chat_at = -math.inf
        try:
            await ws.send_text(_welcome(room, me.id))
            async for text in ws.iter_text():
                try:
                    msg = client_message_adapter.validate_json(text)
                except ValidationError as e:
                    await _send_error(ws, f"invalid message: {e.errors()[0]['msg']}")
                    continue
                if isinstance(msg, MoveMessage):
                    room.apply_move(me.id, msg)
                elif isinstance(msg, ChatMessage):
                    now = time.monotonic()
                    if now - last_chat_at < CHAT_MIN_INTERVAL_SECONDS:
                        await _send_error(ws, "chatting too fast")
                        continue
                    last_chat_at = now
                    await room.chat(me.id, msg.text)
                elif isinstance(msg, SitMessage):
                    error = room.sit(me.id, msg.seat)
                    if error:
                        await _send_error(ws, error)
                elif isinstance(msg, StandMessage):
                    room.stand(me.id)
                elif isinstance(msg, TravelMessage):
                    room, error = await rooms.travel(room, me.id, msg.to)
                    if error:
                        await _send_error(ws, error)
                    else:
                        await ws.send_text(_welcome(room, me.id))
                else:
                    await _send_error(ws, "already joined")
        except WebSocketDisconnect:
            pass
        finally:
            left = await rooms.leave(room, me.id, ws)
            if left is not None:
                await positions.save(sessionmaker, [(room, left)])

    return app


def _welcome(room: Room, player_id: str) -> str:
    game_map = room.game_map.data if room.game_map else None
    return WelcomeMessage(id=player_id, players=room.snapshot(), map=game_map).to_json()


async def _send_error(ws: WebSocket, message: str) -> None:
    await ws.send_text(ErrorMessage(message=message).to_json())


async def _reject(ws: WebSocket, message: str) -> None:
    await _send_error(ws, message)
    await ws.close(code=status.WS_1008_POLICY_VIOLATION)


logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s: %(message)s")
app = create_app()
