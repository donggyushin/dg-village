"""Map data shared with the Unity client, and who can hear whom on a map.

`maps/<id>.json` is the single source of truth: the server reads it here, and Unity's
**DgVillage → Create Map Test Scene** builds the room (floor, walls, furniture) from the same file.

Coordinates match Unity's FloorMesh: 1 tile = 1 unit, the layout is centered on the origin,
the first row is north (+z). Layout characters: `s` stone (hallway), `w` wood (desk area),
`c` carpet (lounge), `g` grass and `p` path (outdoor, hallway rules), anything else = no floor.

Each map is its own room per channel (room.py): players only see and hear players on the same map.
`portals` are floor rectangles that send the player to another map (`to`) at (`spawnX`, `spawnZ`);
the client does the travel (leave + join the other map), the server only lists them.

Zones decide chat delivery:
- lounge = a connected carpet area; everyone inside the same lounge hears each other
- hallway = everywhere else; only players within `hearingRadius` hear each other
- a lounge and the hallway don't hear each other, even when standing close
- a player sitting at a `desk` seat is in focus mode: hears nobody and nobody hears them
  (handled in room.py, since it depends on player state, not on the position)
"""

import json
import math
from dataclasses import dataclass
from importlib import resources

HALLWAY = "hallway"
LOUNGE_TILE = "c"
# Every map a client may join. The first one is where a channel starts
# (also for old clients that send no map).
MAP_IDS = ("office", "outdoor")
DEFAULT_MAP_ID = MAP_IDS[0]


@dataclass(frozen=True)
class Seat:
    id: str
    kind: str  # "desk" | "lounge"
    x: float
    z: float


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
        self.id: str = data["id"]
        self.hearing_radius: float = float(data["hearingRadius"])
        self.layout: list[str] = data["layout"]
        self.seats = {
            s["id"]: Seat(s["id"], s["kind"], s["x"], s["z"]) for s in data.get("seats", [])
        }
        self.portals = [
            Portal(p["x"], p["z"], p["w"], p["d"], p["to"], p["spawnX"], p["spawnZ"])
            for p in data.get("portals", [])
        ]
        width = max(len(row) for row in self.layout)
        self._left = -width / 2
        self._top = len(self.layout) / 2
        self._lounges = self._find_lounges()

    def tile_at(self, x: float, z: float) -> tuple[int, int]:
        return math.floor(x - self._left), math.floor(self._top - z)

    def zone_at(self, x: float, z: float) -> str:
        """`"lounge:<n>"` (n numbers the connected carpet areas) or `"hallway"`."""
        lounge = self._lounges.get(self.tile_at(x, z))
        return HALLWAY if lounge is None else f"lounge:{lounge}"

    def can_hear(self, speaker: tuple[float, float], listener: tuple[float, float]) -> bool:
        zone = self.zone_at(*speaker)
        if zone != self.zone_at(*listener):
            return False
        if zone != HALLWAY:
            return True
        return math.dist(speaker, listener) <= self.hearing_radius

    def _char(self, tx: int, ty: int) -> str:
        if 0 <= ty < len(self.layout) and 0 <= tx < len(self.layout[ty]):
            return self.layout[ty][tx]
        return " "

    def _find_lounges(self) -> dict[tuple[int, int], int]:
        """Number every connected carpet area (4-neighborhood flood fill)."""
        lounges: dict[tuple[int, int], int] = {}
        for ty, row in enumerate(self.layout):
            for tx, ch in enumerate(row):
                if ch != LOUNGE_TILE or (tx, ty) in lounges:
                    continue
                number = len(set(lounges.values()))
                stack = [(tx, ty)]
                while stack:
                    cx, cy = stack.pop()
                    if (cx, cy) in lounges or self._char(cx, cy) != LOUNGE_TILE:
                        continue
                    lounges[(cx, cy)] = number
                    stack += [(cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)]
        return lounges


def load_map(map_id: str) -> GameMap:
    text = resources.files(__package__).joinpath("maps", f"{map_id}.json").read_text("utf-8")
    return GameMap(json.loads(text))


def load_maps() -> dict[str, GameMap]:
    return {map_id: load_map(map_id) for map_id in MAP_IDS}
