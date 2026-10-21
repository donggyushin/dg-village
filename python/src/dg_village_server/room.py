"""In-memory rooms. Nothing here is persisted: a server restart empties every room."""

import asyncio
import logging
import math
from dataclasses import dataclass, field
from typing import Protocol

from .maps import GameMap
from .protocol import (
    ChatBroadcastMessage,
    ErrorMessage,
    MoveMessage,
    PlayerJoinedMessage,
    PlayerLeftMessage,
    PlayerState,
    StateMessage,
)

logger = logging.getLogger(__name__)

# How close (units) a player must stand to a seat to sit on it.
SIT_RANGE = 1.5
# A move this far from the seat while sitting means the player got up.
STAND_DISTANCE = 0.05


class Connection(Protocol):
    """The part of a WebSocket that rooms need. Lets tests use a fake."""

    async def send_text(self, data: str) -> None: ...

    async def close(self, code: int = 1000) -> None: ...


@dataclass
class Player:
    state: PlayerState
    connection: Connection


@dataclass
class Room:
    name: str
    # None = everyone in the room hears every chat message (no zones).
    game_map: GameMap | None = None
    players: dict[str, Player] = field(default_factory=dict)
    # Set when a position changes; the tick loop only broadcasts dirty rooms.
    dirty: bool = False

    async def broadcast(self, data: str, exclude: str | None = None) -> None:
        await self.send_to([p for pid, p in self.players.items() if pid != exclude], data)

    async def send_to(self, targets: list[Player], data: str) -> None:
        results = await asyncio.gather(
            *(p.connection.send_text(data) for p in targets), return_exceptions=True
        )
        for player, result in zip(targets, results, strict=True):
            # A failed send means the socket is closing; its own receive loop cleans it up.
            if isinstance(result, Exception):
                logger.debug("send to %s failed: %r", player.state.id, result)

    def apply_move(self, player_id: str, move: MoveMessage) -> None:
        state = self.players[player_id].state
        if state.seat is not None:
            if math.dist((state.x, state.z), (move.x, move.z)) <= STAND_DISTANCE:
                return  # still sitting; the seat decides position and facing
            self.stand(player_id)
        state.x, state.z = move.x, move.z
        state.facing, state.moving = move.facing, move.moving
        self.dirty = True

    def sit(self, player_id: str, seat_id: str) -> str | None:
        """Seat the player (snapped onto the seat, facing the camera). Returns why not, or None."""
        seat = self.game_map.seats.get(seat_id) if self.game_map else None
        if seat is None:
            return "unknown seat"
        state = self.players[player_id].state
        if any(p.state.seat == seat_id for pid, p in self.players.items() if pid != player_id):
            return "seat taken"
        if math.dist((state.x, state.z), (seat.x, seat.z)) > SIT_RANGE:
            return "too far from the seat"
        state.seat, state.focused = seat.id, seat.kind == "desk"
        state.x, state.z, state.facing, state.moving = seat.x, seat.z, "down", False
        self.dirty = True
        return None

    def stand(self, player_id: str) -> None:
        state = self.players[player_id].state
        if state.seat is not None:
            state.seat, state.focused = None, False
            self.dirty = True

    async def chat(self, player_id: str, text: str) -> None:
        speaker = self.players[player_id].state
        message = ChatBroadcastMessage(id=player_id, name=speaker.name, text=text).to_json()
        await self.send_to(self.hearers(player_id), message)

    def hearers(self, player_id: str) -> list[Player]:
        """Who receives a chat message from this player (always including the speaker's echo)."""
        if self.game_map is None:
            return list(self.players.values())
        speaker = self.players[player_id].state
        game_map = self.game_map
        return [
            p
            for pid, p in self.players.items()
            if pid == player_id
            or (
                # focus mode (sitting at a desk): neither side hears the other
                not speaker.focused
                and not p.state.focused
                and game_map.can_hear((speaker.x, speaker.z), (p.state.x, p.state.z))
            )
        ]

    def snapshot(self) -> list[PlayerState]:
        return [p.state for p in self.players.values()]


class RoomManager:
    def __init__(self, game_map: GameMap | None = None) -> None:
        # Map used by every room (chat zones). None until the client's village is the office.
        self.game_map = game_map
        self.rooms: dict[str, Room] = {}
        # Player id (= account id) -> the room they are in. One session per account.
        self._player_rooms: dict[str, Room] = {}

    async def join(
        self,
        room_name: str,
        player_id: str,
        name: str,
        character_id: str,
        connection: Connection,
    ) -> tuple[Room, PlayerState]:
        # Same account logging in again (another window/device): the newer session wins.
        await self.kick(player_id, "logged in from another session")

        room = self.rooms.setdefault(room_name, Room(room_name, self.game_map))
        state = PlayerState(id=player_id, name=name, character_id=character_id)
        room.players[player_id] = Player(state, connection)
        self._player_rooms[player_id] = room
        await room.broadcast(PlayerJoinedMessage(player=state).to_json(), exclude=player_id)
        logger.info("%s (%s) joined room %r", name, player_id, room_name)
        return room, state

    def player_count(self, room_name: str) -> int:
        room = self.rooms.get(room_name)
        return len(room.players) if room else 0

    async def leave(self, room: Room, player_id: str, connection: Connection) -> None:
        player = room.players.get(player_id)
        # Already kicked and replaced by a newer session of the same account: leave that one be.
        if player is None or player.connection is not connection:
            return
        del room.players[player_id]
        self._player_rooms.pop(player_id, None)
        logger.info("%s (%s) left room %r", player.state.name, player_id, room.name)
        if room.players:
            await room.broadcast(PlayerLeftMessage(id=player_id).to_json())
        else:
            self.rooms.pop(room.name, None)

    async def kick(self, player_id: str, reason: str) -> None:
        room = self._player_rooms.get(player_id)
        if room is None:
            return
        player = room.players[player_id]
        await self.leave(room, player_id, player.connection)
        try:
            await player.connection.send_text(ErrorMessage(message=reason).to_json())
            await player.connection.close(code=4000)
        except Exception as e:  # already closing
            logger.debug("kick of %s: %r", player_id, e)

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
