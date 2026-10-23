import math
import random

import pytest

from dg_village_server.maps import load_map
from dg_village_server.pets import ClearCheck, Pet, PetHerd, facing_for, make_herd
from dg_village_server.protocol import PetState


@pytest.mark.parametrize(
    ("dx", "dz", "yaw", "facing"),
    [
        (1, 0, 0, "right"),
        (0, 1, 0, "up"),
        (-1, -1, 0, "downleft"),
        # quarter view: world +x runs to the screen's upper right, +z to the upper left
        (1, 0, 45, "upright"),
        (0, 1, 45, "upleft"),
        (0, -1, 45, "downright"),
        (1, 1, 45, "up"),
    ],
)
def test_facing_is_screen_relative(dx, dz, yaw, facing) -> None:
    assert facing_for(dx, dz, yaw) == facing


def make_pet_herd(is_clear=lambda x, z: True, seed: int = 1) -> PetHerd:
    pet = Pet(PetState(id="dog", character_id="corgi"), home=(0.0, 0.0), roam=5.0)
    return PetHerd([pet], is_clear, rng=random.Random(seed))


def test_pet_walks_and_stays_in_its_roam_circle() -> None:
    herd = make_pet_herd()
    pet = herd.pets[0]
    walked = False
    for _ in range(3000):
        herd.step(0.1, [])
        walked |= pet.state.moving
        assert math.dist((pet.state.x, pet.state.z), pet.home) <= pet.roam + 1e-6
    assert walked


def test_pet_never_steps_on_blocked_ground() -> None:
    # only the x >= 0 half is walkable
    herd = make_pet_herd(lambda x, z: x >= 0)
    pet = herd.pets[0]
    for _ in range(3000):
        herd.step(0.1, [])
        assert pet.state.x >= -1e-9


def test_pet_visits_players() -> None:
    herd = make_pet_herd(seed=3)
    pet = herd.pets[0]
    stops = []
    for _ in range(3000):
        herd.step(0.1, [(3.0, 3.0)])
        if not pet.state.moving:
            stops.append(math.dist((pet.state.x, pet.state.z), (3.0, 3.0)))
    # it comes to rest next to the player now and then
    assert any(d == pytest.approx(0.9, abs=0.01) for d in stops)


def test_props_block_pets() -> None:
    clear = ClearCheck(lambda x, z: True, [{"sprite": "tree_round", "x": 0, "z": 0}])
    assert not clear(0.5, 0)
    assert clear(2, 0)


def test_outdoor_dogs_start_on_clear_ground() -> None:
    game_map = load_map("outdoor")
    herd = make_herd(game_map.data, game_map.is_floor)
    assert herd is not None
    assert {p.state.character_id for p in herd.pets} == {"corgi", "poodle"}
    assert all(herd.is_clear(p.state.x, p.state.z) for p in herd.pets)
    assert herd.yaw == 45.0
