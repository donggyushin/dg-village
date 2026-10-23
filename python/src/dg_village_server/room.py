"""In-memory rooms. Nothing here is persisted: a server restart empties every room."""

import asyncio
import logging
import math
from dataclasses import dataclass, field
from typing import Protocol

from .maps import GameMap
from .pets import PetHerd, make_herd
from .protocol import (
    ChatBroadcastMessage,
    ErrorMessage,
    MoveMessage,
    PetState,
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
# How far outside a portal (units) a travel request is still accepted: the server's position lags
# the client's by up to one move interval.
PORTAL_SLACK = 1.0


class Connection(Protocol):
    """The part of a WebSocket that rooms need. Lets tests use a fake."""

    async def send_text(self, data: str) -> None: ...

    async def close(self, code: int = 1000) -> None: ...


@dataclass
class Player:
    state: PlayerState
    connection: Connection
    # Where the player stood before sitting (the seat itself is inside the chair's footprint).
    stood_at: tuple[float, float] | None = None

    def resume_position(self) -> tuple[float, float]:
        """Where to put the player when they come back: standing, never inside a chair."""
        if self.state.seat is not None and self.stood_at is not None:
            return self.stood_at
        return self.state.x, self.state.z


@dataclass
class Room:
    name: str
    # Channel id (hex) the room belongs to; a channel has one room per map.
    channel: str = ""
    map_id: str = ""
    # Seats and portals; None = a bare room (tests).
    game_map: GameMap | None = None
    players: dict[str, Player] = field(default_factory=dict)
    # Animals wandering the map (pets.py); None = the map has none.
    pets: PetHerd | None = None
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
        """Seat the player (snapped onto the seat, facing its way). Returns why not, or None."""
        seat = self.game_map.seats.get(seat_id) if self.game_map else None
        if seat is None:
            return "unknown seat"
        player = self.players[player_id]
        state = player.state
        if any(p.state.seat == seat_id for pid, p in self.players.items() if pid != player_id):
            return "seat taken"
        if math.dist((state.x, state.z), (seat.x, seat.z)) > SIT_RANGE:
            return "too far from the seat"
        if state.seat is None:
            player.stood_at = state.x, state.z
        state.seat = seat.id
        state.x, state.z, state.facing, state.moving = seat.x, seat.z, seat.facing, False
        self.dirty = True
        return None

    def stand(self, player_id: str) -> None:
        state = self.players[player_id].state
        if state.seat is not None:
            state.seat = None
            self.dirty = True

    async def chat(self, player_id: str, text: str) -> None:
        speaker = self.players[player_id].state
        message = ChatBroadcastMessage(id=player_id, name=speaker.name, text=text).to_json()
        # everyone on the map, the speaker included (their bubble comes from this echo)
        await self.broadcast(message)

    def snapshot(self) -> list[PlayerState]:
        return [p.state for p in self.players.values()]

    def pet_snapshot(self) -> list[PetState]:
        return self.pets.snapshot() if self.pets else []

    def step_pets(self, dt: float) -> None:
        if self.pets is None:
            return
        players = [(p.state.x, p.state.z) for p in self.players.values()]
        if self.pets.step(dt, players):
            self.dirty = True


class RoomManager:
    def __init__(self, maps: dict[str, GameMap] | None = None) -> None:
        # Map id -> map (seats, portals). A map id not in here = bare room without seats (tests).
        self.maps = maps or {}
        # "<channel>/<map>" -> room
        self.rooms: dict[str, Room] = {}
        # Player id (= account id) -> the room they are in. One session per account.
        self._player_rooms: dict[str, Room] = {}

    async def join(
        self,
        channel: str,
        map_id: str,
        player_id: str,
        name: str,
        character_id: str,
        connection: Connection,
        position: tuple[float, float] = (0.0, 0.0),
    ) -> tuple[Room, PlayerState]:
        # Same account logging in again (another window/device): the newer session wins.
        # Walking through a portal is also a new session (on the other map's room).
        await self.kick(player_id, "logged in from another session")

        room = self._room(channel, map_id)
        x, z = position
        state = PlayerState(id=player_id, name=name, character_id=character_id, x=x, z=z)
        room.players[player_id] = Player(state, connection)
        self._player_rooms[player_id] = room
        await room.broadcast(PlayerJoinedMessage(player=state).to_json(), exclude=player_id)
        logger.info("%s (%s) joined room %r", name, player_id, room.name)
        return room, state

    async def travel(self, room: Room, player_id: str, to: str) -> tuple[Room, str | None]:
        """Move the player through a portal of their map to `to`. Returns (room now in, error)."""
        player = room.players[player_id]
        state = player.state
        game_map = room.game_map
        portal = game_map.portal_to(to, state.x, state.z, PORTAL_SLACK) if game_map else None
        if portal is None or to not in self.maps:
            return room, "not at a portal"
        await self.leave(room, player_id, player.connection)
        state.seat, state.moving = None, False
        state.x, state.z = portal.spawn_x, portal.spawn_z
        target = self._room(room.channel, to)
        target.players[player_id] = player
        self._player_rooms[player_id] = target
        await target.broadcast(PlayerJoinedMessage(player=state).to_json(), exclude=player_id)
        logger.info("%s (%s) travelled to room %r", state.name, player_id, target.name)
        return target, None

    def _room(self, channel: str, map_id: str) -> Room:
        name = f"{channel}/{map_id}"
        room = self.rooms.get(name)
        if room is None:
            game_map = self.maps.get(map_id)
            room = self.rooms[name] = Room(name, channel, map_id, game_map)
            if game_map is not None:
                room.pets = make_herd(game_map.data, game_map.is_floor)
        return room

    def player_count(self, channel: str) -> int:
        """Players in the channel, all maps together."""
        return sum(len(room.players) for room in self.rooms.values() if room.channel == channel)

    async def leave(self, room: Room, player_id: str, connection: Connection) -> Player | None:
        """Remove the player; returns them (to save where they were), or None if already gone."""
        player = room.players.get(player_id)
        # Already kicked and replaced by a newer session of the same account: leave that one be.
        if player is None or player.connection is not connection:
            return None
        del room.players[player_id]
        self._player_rooms.pop(player_id, None)
        logger.info("%s (%s) left room %r", player.state.name, player_id, room.name)
        if room.players:
            await room.broadcast(PlayerLeftMessage(id=player_id).to_json())
        else:
            self.rooms.pop(room.name, None)
        return player

    def everyone(self) -> list[tuple[Room, Player]]:
        """Every connected player with their room (saving positions at shutdown)."""
        return [(room, player) for room in self.rooms.values() for player in room.players.values()]

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

    async def tick(self, dt: float = 0.0) -> None:
        for room in list(self.rooms.values()):
            room.step_pets(dt)
            if room.dirty:
                room.dirty = False
                state = StateMessage(players=room.snapshot(), pets=room.pet_snapshot())
                await room.broadcast(state.to_json())

    async def run_tick_loop(self, interval: float) -> None:
        while True:
            await asyncio.sleep(interval)
            try:
                await self.tick(interval)
            except Exception:
                logger.exception("tick failed")
