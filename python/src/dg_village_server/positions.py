"""Where players start when they join a channel: back where they last left it.

Rooms live in memory (room.py); the last spot per (user, channel) is kept in Postgres
(db.SavedPosition) so it survives disconnects, restarts and deploys.
"""

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


async def save(sessionmaker: async_sessionmaker, left: Iterable[tuple[Room, Player]]) -> None:
    """Remember where these players are. Never raises: losing a spot only costs a walk."""
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
