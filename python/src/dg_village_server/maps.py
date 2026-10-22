"""Map data shared with the Unity client.

`maps/<id>.json` is the single source of truth: the server reads it here and sends it to the
client in `welcome`; the client lays the room (floor, walls, furniture) from it at runtime.

Coordinates match Unity's FloorMesh: 1 tile = 1 unit, the layout is centered on the origin,
the first row is north (+z). Layout characters: `s` stone, `w` wood, `c` carpet, `g` grass,
`p` path (floor materials only), anything else = no floor.

Each map is its own room per channel (room.py): players see and hear everyone on the same map
(no chat zones or focus mode since 2026-10-22).
`portals` are floor rectangles that send the player to another map (`to`) at (`spawnX`, `spawnZ`):
the client sends `travel` when its player steps in, the server checks the player is there and moves
them to the other map's room.
"""

import json
import math
from dataclasses import dataclass
from importlib import resources

# Every map a client may join. The first one is where a channel starts
# (also for old clients that send no map).
MAP_IDS = ("office", "outdoor")
DEFAULT_MAP_ID = MAP_IDS[0]


@dataclass(frozen=True)
class Seat:
    id: str
    kind: str  # "desk" | "lounge" (only how the seat looks in the editor)
    x: float
    z: float
    facing: str = "down"  # where a seated player looks (chair orientation); 4 directions


@dataclass(frozen=True)
class Portal:
    x: float  # center
    z: float
    w: float  # size along x
    d: float  # size along z
    to: str  # target map id
    spawn_x: float  # where the player appears on the target map
    spawn_z: float


class GameMap:
    def __init__(self, data: dict) -> None:
        # The JSON as loaded, sent to clients as-is (they lay the room from it).
        self.data = data
        self.id: str = data["id"]
        self.layout: list[str] = data["layout"]
        self.seats = {
            s["id"]: Seat(s["id"], s["kind"], s["x"], s["z"], s.get("facing", "down"))
            for s in data.get("seats", [])
        }
        self.portals = [
            Portal(p["x"], p["z"], p["w"], p["d"], p["to"], p["spawnX"], p["spawnZ"])
            for p in data.get("portals", [])
        ]
        width = max(len(row) for row in self.layout)
        self._left = -width / 2
        self._top = len(self.layout) / 2

    def tile_at(self, x: float, z: float) -> tuple[int, int]:
        return math.floor(x - self._left), math.floor(self._top - z)

    def portal_to(self, to: str, x: float, z: float, slack: float) -> Portal | None:
        """A portal to `to` that (x, z) stands in, the rectangle grown by `slack` on every side."""
        for portal in self.portals:
            if (
                portal.to == to
                and abs(x - portal.x) <= portal.w / 2 + slack
                and abs(z - portal.z) <= portal.d / 2 + slack
            ):
                return portal
        return None


def load_map(map_id: str) -> GameMap:
    text = resources.files(__package__).joinpath("maps", f"{map_id}.json").read_text("utf-8")
    return GameMap(json.loads(text))


def load_maps() -> dict[str, GameMap]:
    return {map_id: load_map(map_id) for map_id in MAP_IDS}
