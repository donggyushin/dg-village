"""Where players start when they join a channel: back where they last left it.

Rooms live in memory (room.py); the last spot per (user, channel) is kept in Postgres
(db.SavedPosition) so it survives disconnects, restarts and deploys.
"""

import asyncio
import logging
import uuid
from collections.abc import Iterable
from datetime import UTC, datetime

from sqlalchemy.ext.asyncio import async_sessionmaker

from .db import SavedPosition
from .maps import DEFAULT_MAP_ID, GameMap
from .room import Player, Room

logger = logging.getLogger(__name__)

# First visit, or the saved spot is gone (map removed, layout changed under it).
DEFAULT_START = (DEFAULT_MAP_ID, 0.0, 0.0)


async def load_start(
    sessionmaker: async_sessionmaker,
    maps: dict[str, GameMap],
    user_id: uuid.UUID,
    channel_id: uuid.UUID,
) -> tuple[str, float, float]:
    """(map, x, z) to join the channel at."""
    async with sessionmaker() as session:
        saved = await session.get(SavedPosition, (user_id, channel_id))
    if saved is None:
        return DEFAULT_START
    game_map = maps.get(saved.map_id)
    if game_map is None or not game_map.is_floor(saved.x, saved.z):
        logger.info("saved position %s %r is no longer valid", user_id, saved.map_id)
        return DEFAULT_START
    return saved.map_id, saved.x, saved.z


async def write(sessionmaker: async_sessionmaker, left: list[tuple[Room, Player]]) -> None:
    """Remember where these players are. Never raises: losing a spot only costs a walk."""
    if not left:
        return
    try:
        async with sessionmaker() as session:
            for room, player in left:
                x, z = player.resume_position()
                await session.merge(
                    SavedPosition(
                        user_id=uuid.UUID(hex=player.state.id),
                        channel_id=uuid.UUID(hex=room.channel),
                        map_id=room.map_id,
                        x=x,
                        z=z,
                        updated_at=datetime.now(UTC),
                    )
                )
            await session.commit()
    except Exception:
        logger.exception("saving positions failed")


class PositionSaver:
    """Runs the saves so shutdown can wait for them before the database engine goes away.

    A write cut off by a cancel, or still running when the engine is disposed, can hang on the
    closed connection: saves are shielded from the leaving socket's cancel, and `close` drains
    them first.
    """

    def __init__(self, sessionmaker: async_sessionmaker) -> None:
        self._sessionmaker = sessionmaker
        self._pending: set[asyncio.Task] = set()
        self._closed = False

    async def save(self, left: Iterable[tuple[Room, Player]]) -> None:
        if self._closed:
            return
        task = asyncio.create_task(write(self._sessionmaker, list(left)))
        self._pending.add(task)
        task.add_done_callback(self._pending.discard)
        await asyncio.shield(task)

    async def close(self, everyone: Iterable[tuple[Room, Player]]) -> None:
        """Server shutdown: finish running saves, then save whoever is still connected."""
        self._closed = True
        if self._pending:
            await asyncio.gather(*self._pending, return_exceptions=True)
        await write(self._sessionmaker, list(everyone))
