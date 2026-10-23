"""Pets: animals that wander a map on their own (the two garden dogs).

The map JSON lists them (`pets`: `{id, character, x, z, roam}`); every room of that map gets its own
copies, moved by the room's tick and sent to clients next to the players (`welcome`/`state.pets`).
A pet idles a few seconds, then walks to a random spot within `roam` units of its home (x, z), and
sometimes trots over to a player standing in that circle instead.

The server doesn't know furniture footprints (the client owns them), so walks are checked against
the floor tiles and a rough radius per prop sprite (`_PROP_RADII`): good enough to keep a dog out of
trees, benches and buildings.
"""

import math
import random
from collections.abc import Callable
from dataclasses import dataclass, field

from .protocol import Facing, PetState

# Units per second (players walk at 4).
WALK_SPEED = 1.6
IDLE_SECONDS = (2.0, 6.0)
# Chance that the next walk goes to a player in the roam circle instead of a random spot.
VISIT_CHANCE = 0.4
# How far from a visited player the pet stops.
VISIT_DISTANCE = 0.9
# Path check step (units) and how many random targets to try before idling again.
PATH_STEP = 0.25
TARGET_TRIES = 12
# Pet body radius kept away from props.
BODY_RADIUS = 0.3

# Sprite id prefix -> blocked radius around the prop's position. Unknown sprites block nothing.
_PROP_RADII = {
    "guild_hall": 5.0,
    "library": 5.0,
    "shop": 5.0,
    "tree": 0.8,
    "bench": 1.1,
    "street_lamp": 0.4,
    "plant_pot": 0.4,
}

# Screen facing by 45° sector of the screen-space angle (0 = right, counter-clockwise),
# like PlayerMovement.FacingFromInput on the client.
_SECTORS: tuple[Facing, ...] = (
    "right",
    "upright",
    "up",
    "upleft",
    "left",
    "downleft",
    "down",
    "downright",
)


def facing_for(dx: float, dz: float, yaw_degrees: float) -> Facing:
    """Screen-relative facing of a world (x, z) direction for a camera turned `yaw_degrees`."""
    yaw = math.radians(yaw_degrees)
    # inverse of the client's ViewAngle.ScreenToGround (rotation about Y by the camera yaw)
    sx = dx * math.cos(yaw) - dz * math.sin(yaw)
    sy = dx * math.sin(yaw) + dz * math.cos(yaw)
    sector = round(math.degrees(math.atan2(sy, sx)) / 45) % 8
    return _SECTORS[sector]


@dataclass
class Pet:
    state: PetState
    home: tuple[float, float]
    roam: float
    target: tuple[float, float] | None = None
    # Seconds left idling before picking the next target.
    wait: float = 0.0


@dataclass
class PetHerd:
    """Every pet of one room. `is_clear(x, z)` says where a pet may stand."""

    pets: list[Pet]
    is_clear: Callable[[float, float], bool]
    # Camera yaw of the map (quarter view = 45°) for screen-relative facing.
    yaw: float = 0.0
    rng: random.Random = field(default_factory=random.Random)

    def snapshot(self) -> list[PetState]:
        return [pet.state for pet in self.pets]

    def step(self, dt: float, players: list[tuple[float, float]]) -> bool:
        """Advance every pet by dt seconds. Returns whether anything visible changed."""
        changed = False
        for pet in self.pets:
            changed |= self._step_pet(pet, dt, players)
        return changed

    def _step_pet(self, pet: Pet, dt: float, players: list[tuple[float, float]]) -> bool:
        state = pet.state
        if pet.target is None:
            pet.wait -= dt
            if pet.wait > 0:
                return False
            pet.target = self._pick_target(pet, players)
            if pet.target is None:
                pet.wait = self.rng.uniform(*IDLE_SECONDS)
                return False

        tx, tz = pet.target
        dx, dz = tx - state.x, tz - state.z
        distance = math.hypot(dx, dz)
        stride = WALK_SPEED * dt
        if distance <= stride:
            state.x, state.z, state.moving = tx, tz, False
            pet.target = None
            pet.wait = self.rng.uniform(*IDLE_SECONDS)
        else:
            state.x += dx / distance * stride
            state.z += dz / distance * stride
            state.facing = facing_for(dx, dz, self.yaw)
            state.moving = True
        return True

    def _pick_target(
        self, pet: Pet, players: list[tuple[float, float]]
    ) -> tuple[float, float] | None:
        start = pet.state.x, pet.state.z
        hx, hz = pet.home
        nearby = [p for p in players if math.dist(p, pet.home) <= pet.roam]
        if nearby and self.rng.random() < VISIT_CHANCE:
            px, pz = self.rng.choice(nearby)
            dx, dz = px - start[0], pz - start[1]
            distance = math.hypot(dx, dz)
            if distance > VISIT_DISTANCE * 1.5:
                scale = (distance - VISIT_DISTANCE) / distance
                target = start[0] + dx * scale, start[1] + dz * scale
                if self._path_clear(start, target):
                    return target
        for _ in range(TARGET_TRIES):
            angle = self.rng.uniform(0, math.tau)
            radius = pet.roam * math.sqrt(self.rng.random())
            target = hx + math.cos(angle) * radius, hz + math.sin(angle) * radius
            if math.dist(start, target) > 1.0 and self._path_clear(start, target):
                return target
        return None

    def _path_clear(self, start: tuple[float, float], end: tuple[float, float]) -> bool:
        (sx, sz), (ex, ez) = start, end
        steps = max(1, math.ceil(math.dist(start, end) / PATH_STEP))
        return all(
            self.is_clear(sx + (ex - sx) * i / steps, sz + (ez - sz) * i / steps)
            for i in range(1, steps + 1)
        )


class ClearCheck:
    """Where a pet may stand on a map: floor tiles, away from props."""

    def __init__(self, is_floor: Callable[[float, float], bool], props: list[dict]) -> None:
        self._is_floor = is_floor
        self._blockers = [
            (p["x"], p["z"], radius + BODY_RADIUS)
            for p in props
            if (radius := prop_radius(p["sprite"])) > 0
        ]

    def __call__(self, x: float, z: float) -> bool:
        if not self._is_floor(x, z):
            return False
        return all(math.dist((x, z), (bx, bz)) > r for bx, bz, r in self._blockers)


def prop_radius(sprite: str) -> float:
    for prefix, radius in _PROP_RADII.items():
        if sprite.startswith(prefix):
            return radius
    return 0.0


def make_herd(data: dict, is_floor: Callable[[float, float], bool]) -> PetHerd | None:
    """The pets of a map JSON (None if it has none), each at home and about to pick a walk."""
    entries = data.get("pets", [])
    if not entries:
        return None
    rng = random.Random()
    pets = [
        Pet(
            PetState(id=e["id"], character_id=e["character"], x=e["x"], z=e["z"]),
            home=(e["x"], e["z"]),
            roam=e["roam"],
            wait=rng.uniform(0.5, 3.0),
        )
        for e in entries
    ]
    yaw = 45.0 if data.get("view") == "quarter" else 0.0
    return PetHerd(pets, ClearCheck(is_floor, data.get("props", [])), yaw, rng)
