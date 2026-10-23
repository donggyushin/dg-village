import asyncio

import pytest

from dg_village_server.maps import MAP_IDS, GameMap, load_map, load_maps
from dg_village_server.protocol import MoveMessage, PlayerState
from dg_village_server.room import Player, Room


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


def test_decor_floor_is_not_standable() -> None:
    game_map = GameMap({"id": "t", "layout": ["GgG"]})
    assert game_map.is_floor(0.0, 0.0)
    assert not game_map.is_floor(-1.0, 0.0)
    assert not game_map.is_floor(1.2, 0.0)


def test_office_map_loads() -> None:
    office = load_map("office")
    assert office.id == "office"
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


def test_chat_reaches_everyone_on_the_map() -> None:
    # far apart, on different floors, one of them sitting at a desk: everyone still hears
    office = load_map("office")
    desk = office.seats["desk-1"]
    room, connections = make_room(
        office,
        {
            "speaker": (-0.5, 0.0),
            "far": (6.0, -6.0),
            "lounge": (5.5, 3.0),
            "desk": (desk.x, desk.z),
        },
    )
    assert room.sit("desk", "desk-1") is None
    asyncio.run(room.chat("speaker", "hi"))
    assert received_chat(connections) == {"speaker", "far", "lounge", "desk"}


def test_room_without_map_broadcasts_to_everyone() -> None:
    room, connections = make_room(None, {"a": (0.0, 0.0), "b": (100.0, 100.0)})
    asyncio.run(room.chat("a", "hi"))
    assert received_chat(connections) == {"a", "b"}


OFFICE = load_map("office")
DESK = OFFICE.seats["desk-1"]
SOFA = OFFICE.seats["sofa"]


def test_sit_snaps_onto_the_seat() -> None:
    room, _ = make_room(OFFICE, {"a": (DESK.x + 1.0, DESK.z - 0.5), "b": (SOFA.x, SOFA.z - 1.0)})
    assert room.sit("a", "desk-1") is None
    a = room.players["a"].state
    assert (a.x, a.z, a.facing, a.seat) == (DESK.x, DESK.z, DESK.facing, "desk-1")
    assert room.sit("b", "sofa") is None


def test_sit_faces_the_seats_way() -> None:
    sideways = GameMap(
        {
            "id": "sideways",
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
    assert (a.seat, a.z) == (None, DESK.z - 0.3)
