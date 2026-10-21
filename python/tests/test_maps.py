import asyncio

import pytest

from dg_village_server.maps import HALLWAY, GameMap, load_map
from dg_village_server.protocol import PlayerState
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


def test_office_map_loads() -> None:
    office = load_map("office")
    assert office.id == "office"
    assert office.zone_at(5.5, 3.0) == "lounge:0"  # the carpet lounge
    assert office.zone_at(-0.5, 0.0) == HALLWAY
    assert {s.kind for s in office.seats} == {"desk", "lounge"}


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
    room = Room("test", game_map)
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
