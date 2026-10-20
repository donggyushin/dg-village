import asyncio
import logging
import math
import time
from collections.abc import AsyncIterator
from contextlib import asynccontextmanager

from fastapi import FastAPI, WebSocket, WebSocketDisconnect, status
from pydantic import ValidationError

from .protocol import (
    ChatMessage,
    ErrorMessage,
    JoinMessage,
    MoveMessage,
    WelcomeMessage,
    client_message_adapter,
)
from .room import RoomManager

TICK_RATE_HZ = 10
CHAT_MIN_INTERVAL_SECONDS = 0.5

logger = logging.getLogger(__name__)


def create_app() -> FastAPI:
    @asynccontextmanager
    async def lifespan(app: FastAPI) -> AsyncIterator[None]:
        rooms = RoomManager()
        app.state.rooms = rooms
        tick_task = asyncio.create_task(rooms.run_tick_loop(1 / TICK_RATE_HZ))
        yield
        tick_task.cancel()

    app = FastAPI(title="dg-village server", lifespan=lifespan)

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

        room, me = await rooms.join(first.room, first.name, first.character_id, ws)
        last_chat_at = -math.inf
        try:
            await ws.send_text(WelcomeMessage(id=me.id, players=room.snapshot()).to_json())
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
                else:
                    await _send_error(ws, "already joined")
        except WebSocketDisconnect:
            pass
        finally:
            await rooms.leave(room, me.id)

    return app


async def _send_error(ws: WebSocket, message: str) -> None:
    await ws.send_text(ErrorMessage(message=message).to_json())


async def _reject(ws: WebSocket, message: str) -> None:
    await _send_error(ws, message)
    await ws.close(code=status.WS_1008_POLICY_VIOLATION)


logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s: %(message)s")
app = create_app()
