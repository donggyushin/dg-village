"""Scene weather. Rough on purpose: no state, no database.

Time is cut into fixed periods, and each (scene, period) pair hashes to a weather. So every
client asking about the same scene at the same time gets the same answer, and the weather
survives server restarts, without the server having to store anything.
"""

import hashlib
from datetime import UTC, datetime, timedelta
from typing import Literal

Weather = Literal["sunny", "rain", "snow"]

PERIOD = timedelta(minutes=20)
# Relative odds per period. Sunny most of the time, snow now and then.
WEIGHTS: dict[Weather, int] = {"sunny": 6, "rain": 3, "snow": 1}

_EPOCH = datetime(2026, 1, 1, tzinfo=UTC)


def weather_at(scene: str, now: datetime) -> tuple[Weather, datetime, datetime]:
    """The weather of `scene` at `now`, with the start and end of the period it holds for."""
    index = (now - _EPOCH) // PERIOD
    starts_at = _EPOCH + index * PERIOD
    # hashlib, not hash(): hash() of a str changes on every process start.
    digest = hashlib.sha256(f"{scene}:{index}".encode()).digest()
    roll = int.from_bytes(digest[:8]) % sum(WEIGHTS.values())
    for weather, weight in WEIGHTS.items():
        if roll < weight:
            return weather, starts_at, starts_at + PERIOD
        roll -= weight
    raise AssertionError("roll is below the sum of WEIGHTS")
