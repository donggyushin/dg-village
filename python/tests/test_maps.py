import asyncio

import pytest

from dg_village_server.maps import HALLWAY, MAP_IDS, GameMap, load_map, load_maps
from dg_village_server.protocol import MoveMessage, PlayerState
from dg_village_server.room import Player, Room

# 6 × 4 tiles → x from -3 to 3, z from 2 (north) to -2. Two separate carpet areas.
SMALL = GameMap(
    {
        "id": "small",
        "hearingRadius": 2.0,
        "layout": [
            "cc..cc",
            "cc..cc",
            "ssssss",
            "ssssss",
        ],
    }
)


def test_zones_follow_the_layout() -> None:
    assert SMALL.zone_at(-2.5, 1.5) == "lounge:0"  # top-left carpet
    assert SMALL.zone_at(2.5, 1.5) == "lounge:1"  # top-right carpet, not connected
    assert SMALL.zone_at(0.0, -1.0) == HALLWAY
    assert SMALL.zone_at(50.0, 50.0) == HALLWAY  # off the map


def test_lounge_hears_regardless_of_distance() -> None:
    assert SMALL.can_hear((-2.9, 1.9), (-1.1, 0.1))


def test_hallway_hears_only_within_radius() -> None:
    assert SMALL.can_hear((-2.0, -1.0), (-0.5, -1.0))
    assert not SMALL.can_hear((-2.5, -1.0), (2.5, -1.0))


@pytest.mark.parametrize(
    ("speaker", "listener"),
    [
        ((-2.5, 0.5), (-2.5, -0.5)),  # lounge edge ↔ hallway right next to it
        ((-2.5, 1.5), (2.5, 1.5)),  # two different lounges
    ],
)
def test_zones_dont_hear_each_other(speaker, listener) -> None:
    assert not SMALL.can_hear(speaker, listener)
    assert not SMALL.can_hear(listener, speaker)


def test_portals_lead_to_walkable_spots_on_existing_maps() -> None:
    maps = load_maps()
    assert set(maps) == set(MAP_IDS)
    for game_map in maps.values():
        assert game_map.portals, f"{game_map.id} has no way out"
        for portal in game_map.portals:
            target = maps[portal.to]
            tx, ty = target.tile_at(portal.spawn_x, portal.spawn_z)
            where = f"{game_map.id} → {portal.to}"
            assert target.layout[ty][tx] in "swcgp", f"{where} spawns off the floor"
            # spawning inside the target's own portal would bounce the player straight back
            for back in target.portals:
                inside_x = abs(portal.spawn_x - back.x) < back.w / 2
                inside_z = abs(portal.spawn_z - back.z) < back.d / 2
                assert not (inside_x and inside_z), f"{where} spawns inside a portal"


def test_office_map_loads() -> None:
    office = load_map("office")
    assert office.id == "office"
    assert office.zone_at(5.5, 3.0) == "lounge:0"  # the carpet lounge
    assert office.zone_at(-0.5, 0.0) == HALLWAY
    assert {s.kind for s in office.seats.values()} == {"desk", "lounge"}


class FakeConnection:
    def __init__(self) -> None:
        self.sent: list[str] = []

    async def send_text(self, data: str) -> None:
        self.sent.append(data)

    async def close(self, code: int = 1000) -> None:
        pass


def make_room(
    game_map: GameMap | None, positions: dict[str, tuple[float, float]]
) -> tuple[Room, dict[str, FakeConnection]]:
    room = Room("test", game_map=game_map)
    connections = {pid: FakeConnection() for pid in positions}
    for pid, (x, z) in positions.items():
        state = PlayerState(id=pid, name=pid, character_id="girl01", x=x, z=z)
        room.players[pid] = Player(state, connections[pid])
    return room, connections


def received_chat(connections: dict[str, FakeConnection]) -> set[str]:
    return {pid for pid, c in connections.items() if any('"chat"' in m for m in c.sent)}


def test_room_chat_goes_only_to_hearers() -> None:
    room, connections = make_room(
        SMALL,
        {"speaker": (-2.0, -1.0), "near": (-1.0, -1.0), "far": (2.5, -1.5), "lounge": (-2.5, 1.5)},
    )
    asyncio.run(room.chat("speaker", "hi"))
    assert received_chat(connections) == {"speaker", "near"}


def test_room_without_map_broadcasts_to_everyone() -> None:
    room, connections = make_room(None, {"a": (0.0, 0.0), "b": (100.0, 100.0)})
    asyncio.run(room.chat("a", "hi"))
    assert received_chat(connections) == {"a", "b"}


OFFICE = load_map("office")
DESK = OFFICE.seats["desk-1"]
SOFA = OFFICE.seats["sofa"]


def test_sit_snaps_onto_the_seat_and_desks_focus() -> None:
    room, _ = make_room(OFFICE, {"a": (DESK.x + 1.0, DESK.z - 0.5), "b": (SOFA.x, SOFA.z - 1.0)})
    assert room.sit("a", "desk-1") is None
    a = room.players["a"].state
    assert (a.x, a.z, a.facing, a.seat, a.focused) == (DESK.x, DESK.z, "down", "desk-1", True)
    assert room.sit("b", "sofa") is None
    assert room.players["b"].state.focused is False  # lounge seat: just sitting


def test_sit_faces_the_seats_way() -> None:
    sideways = GameMap(
        {
            "id": "sideways",
            "hearingRadius": 2.0,
            "layout": ["ssss"],
            "seats": [{"id": "s", "kind": "lounge", "x": 0.5, "z": 0.0, "facing": "left"}],
        }
    )
    room, _ = make_room(sideways, {"a": (0.0, 0.0)})
    assert room.sit("a", "s") is None
    assert room.players["a"].state.facing == "left"


@pytest.mark.parametrize(
    ("seat", "position", "error"),
    [
        ("nope", (DESK.x, DESK.z), "unknown seat"),
        ("desk-1", (DESK.x + 3.0, DESK.z), "too far from the seat"),
    ],
)
def test_sit_is_rejected(seat, position, error) -> None:
    room, _ = make_room(OFFICE, {"a": position})
    assert room.sit("a", seat) == error
    assert room.players["a"].state.seat is None


def test_a_seat_holds_one_player() -> None:
    room, _ = make_room(OFFICE, {"a": (DESK.x, DESK.z), "b": (DESK.x, DESK.z - 1.0)})
    assert room.sit("a", "desk-1") is None
    assert room.sit("b", "desk-1") == "seat taken"
    room.stand("a")
    assert room.sit("b", "desk-1") is None


def move(x: float, z: float) -> MoveMessage:
    return MoveMessage(type="move", x=x, z=z, facing="left", moving=True)


def test_moving_away_stands_up_but_resending_the_seat_position_doesnt() -> None:
    room, _ = make_room(OFFICE, {"a": (DESK.x, DESK.z)})
    room.sit("a", "desk-1")
    room.apply_move("a", move(DESK.x, DESK.z))
    assert room.players["a"].state.seat == "desk-1"
    room.apply_move("a", move(DESK.x, DESK.z - 0.3))
    a = room.players["a"].state
    assert (a.seat, a.focused, a.z) == (None, False, DESK.z - 0.3)


def test_focused_player_neither_hears_nor_is_heard() -> None:
    near = (DESK.x + 1.0, DESK.z - 1.0)  # hallway-rules area, well within the radius
    room, connections = make_room(OFFICE, {"desk": (DESK.x, DESK.z), "near": near})
    room.sit("desk", "desk-1")
    asyncio.run(room.chat("near", "hi"))
    asyncio.run(room.chat("desk", "hello?"))
    assert [m for m in connections["desk"].sent if '"chat"' in m] == [
        m for m in connections["desk"].sent if "hello?" in m
    ]  # only its own echo
    assert all("hello?" not in m for m in connections["near"].sent)
