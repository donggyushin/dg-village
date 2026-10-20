"""In-memory rooms. Nothing here is persisted: a server restart empties every room."""

import asyncio
import logging
import uuid
from dataclasses import dataclass, field
from typing import Protocol

from .protocol import (
    MoveMessage,
    PlayerJoinedMessage,
    PlayerLeftMessage,
    PlayerState,
    StateMessage,
)

logger = logging.getLogger(__name__)


class Connection(Protocol):
    """The part of a WebSocket that rooms need. Lets tests use a fake."""

    async def send_text(self, data: str) -> None: ...


@dataclass
class Player:
    state: PlayerState
    connection: Connection


@dataclass
class Room:
    name: str
    players: dict[str, Player] = field(default_factory=dict)
    # Set when a position changes; the tick loop only broadcasts dirty rooms.
    dirty: bool = False

    async def broadcast(self, data: str, exclude: str | None = None) -> None:
        targets = [p for pid, p in self.players.items() if pid != exclude]
        results = await asyncio.gather(
            *(p.connection.send_text(data) for p in targets), return_exceptions=True
        )
        for player, result in zip(targets, results, strict=True):
            # A failed send means the socket is closing; its own receive loop cleans it up.
            if isinstance(result, Exception):
                logger.debug("send to %s failed: %r", player.state.id, result)

    def apply_move(self, player_id: str, move: MoveMessage) -> None:
        state = self.players[player_id].state
        state.x, state.z = move.x, move.z
        state.facing, state.moving = move.facing, move.moving
        self.dirty = True

    def snapshot(self) -> list[PlayerState]:
        return [p.state for p in self.players.values()]


class RoomManager:
    def __init__(self) -> None:
        self.rooms: dict[str, Room] = {}

    async def join(
        self, room_name: str, name: str, character_id: str, connection: Connection
    ) -> tuple[Room, PlayerState]:
        room = self.rooms.setdefault(room_name, Room(room_name))
        state = PlayerState(id=uuid.uuid4().hex, name=name, character_id=character_id)
        room.players[state.id] = Player(state, connection)
        await room.broadcast(PlayerJoinedMessage(player=state).to_json(), exclude=state.id)
        logger.info("%s (%s) joined room %r", name, state.id, room_name)
        return room, state

    async def leave(self, room: Room, player_id: str) -> None:
        player = room.players.pop(player_id, None)
        if player is None:
            return
        logger.info("%s (%s) left room %r", player.state.name, player_id, room.name)
        if room.players:
            await room.broadcast(PlayerLeftMessage(id=player_id).to_json())
        else:
            self.rooms.pop(room.name, None)

    async def tick(self) -> None:
        for room in list(self.rooms.values()):
            if room.dirty:
                room.dirty = False
                await room.broadcast(StateMessage(players=room.snapshot()).to_json())

    async def run_tick_loop(self, interval: float) -> None:
        while True:
            await asyncio.sleep(interval)
            try:
                await self.tick()
            except Exception:
                logger.exception("tick failed")
