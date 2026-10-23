import threading

import pytest
from fastapi.testclient import TestClient
from starlette.websockets import WebSocketDisconnect

from dg_village_server import positions
from dg_village_server.db import DEFAULT_CHANNEL_ID

from conftest import create_channel, signup

PLAZA = DEFAULT_CHANNEL_ID.hex


def join(ws, token: str, room: str = PLAZA, **extra) -> dict:
    ws.send_json({"type": "join", "room": room, "token": token, **extra})
    welcome = ws.receive_json()
    assert welcome["type"] == "welcome", welcome
    return welcome


def receive_until(ws, msg_type: str) -> dict:
    """Skip periodic 'state' snapshots etc. until the wanted message arrives."""
    for _ in range(50):
        msg = ws.receive_json()
        if msg["type"] == msg_type:
            return msg
    raise AssertionError(f"no {msg_type!r} message")


def test_health(client: TestClient) -> None:
    assert client.get("/health").json() == {"status": "ok"}


def test_join_returns_own_id_and_existing_players(client: TestClient) -> None:
    with client.websocket_connect("/ws") as a:
        welcome_a = join(a, signup(client, "alice"))
        assert [p["id"] for p in welcome_a["players"]] == [welcome_a["id"]]

        with client.websocket_connect("/ws") as b:
            welcome_b = join(b, signup(client, "bob"))
            names = {p["name"] for p in welcome_b["players"]}
            assert names == {"alice", "bob"}
            assert welcome_b["players"][0]["characterId"] == "girl01"

            joined = receive_until(a, "player_joined")
            assert joined["player"]["id"] == welcome_b["id"]


def test_move_is_broadcast_in_state(client: TestClient) -> None:
    with client.websocket_connect("/ws") as a, client.websocket_connect("/ws") as b:
        join(a, signup(client, "alice"))
        welcome_b = join(b, signup(client, "bob"))

        b.send_json({"type": "move", "x": 1.5, "z": -2.0, "facing": "left", "moving": True})
        state = receive_until(a, "state")
        bob = next(p for p in state["players"] if p["id"] == welcome_b["id"])
        assert (bob["x"], bob["z"], bob["facing"], bob["moving"]) == (1.5, -2.0, "left", True)


def test_diagonal_facing_is_accepted(client: TestClient) -> None:
    with client.websocket_connect("/ws") as a:
        welcome = join(a, signup(client, "alice"))

        a.send_json({"type": "move", "x": 1.0, "z": 1.0, "facing": "upright", "moving": True})
        state = receive_until(a, "state")
        alice = next(p for p in state["players"] if p["id"] == welcome["id"])
        assert alice["facing"] == "upright"


def test_leave_is_broadcast(client: TestClient) -> None:
    with client.websocket_connect("/ws") as a:
        join(a, signup(client, "alice"))
        with client.websocket_connect("/ws") as b:
            welcome_b = join(b, signup(client, "bob"))
            # Close explicitly: leaving the `with` block cancels the server task
            # instead of delivering a disconnect like a real client would.
            b.close()
            left = receive_until(a, "player_left")
        assert left["id"] == welcome_b["id"]


def test_rooms_are_isolated(client: TestClient) -> None:
    with client.websocket_connect("/ws") as a, client.websocket_connect("/ws") as b:
        alice, bob = signup(client, "alice"), signup(client, "bob")
        join(a, alice, room=create_channel(client, alice, "r1"))
        welcome_b = join(b, bob, room=create_channel(client, bob, "r2"))
        assert len(welcome_b["players"]) == 1


def test_first_message_must_be_join(client: TestClient) -> None:
    with client.websocket_connect("/ws") as ws:
        ws.send_json({"type": "move", "x": 0, "z": 0, "facing": "down", "moving": False})
        assert ws.receive_json()["type"] == "error"
        with pytest.raises(WebSocketDisconnect):
            ws.receive_json()


def test_invalid_message_after_join_keeps_connection(client: TestClient) -> None:
    with client.websocket_connect("/ws") as ws:
        join(ws, signup(client, "alice"))
        ws.send_text("not json")
        assert receive_until(ws, "error")["message"].startswith("invalid message")
        ws.send_json({"type": "move", "x": 1, "z": 1, "facing": "up", "moving": False})
        assert receive_until(ws, "state")["type"] == "state"


def test_chat_is_broadcast_to_everyone_including_sender(client: TestClient) -> None:
    with client.websocket_connect("/ws") as a, client.websocket_connect("/ws") as b:
        welcome_a = join(a, signup(client, "alice"))
        join(b, signup(client, "bob"))

        a.send_json({"type": "chat", "text": "  hello\n\n  world  "})
        expected = {"type": "chat", "id": welcome_a["id"], "name": "alice", "text": "hello world"}
        for ws in (a, b):
            assert receive_until(ws, "chat") == expected


@pytest.mark.parametrize("text", ["", "   \n ", "x" * 201])
def test_invalid_chat_is_rejected(client: TestClient, text: str) -> None:
    with client.websocket_connect("/ws") as ws:
        join(ws, signup(client, "alice"))
        ws.send_json({"type": "chat", "text": text})
        assert receive_until(ws, "error")["message"].startswith("invalid message")


def test_chat_rate_limit(client: TestClient) -> None:
    with client.websocket_connect("/ws") as ws:
        join(ws, signup(client, "alice"))
        ws.send_json({"type": "chat", "text": "one"})
        ws.send_json({"type": "chat", "text": "two"})
        assert receive_until(ws, "chat")["text"] == "one"
        assert receive_until(ws, "error")["message"] == "chatting too fast"


def test_join_with_bad_token_is_rejected(client: TestClient) -> None:
    with client.websocket_connect("/ws") as ws:
        ws.send_json({"type": "join", "room": PLAZA, "token": "nope"})
        assert ws.receive_json() == {"type": "error", "message": "invalid token"}
        with pytest.raises(WebSocketDisconnect):
            ws.receive_json()


def test_player_id_is_the_account_id(client: TestClient) -> None:
    token = signup(client, "alice")
    me = client.get("/me", headers={"Authorization": f"Bearer {token}"}).json()
    with client.websocket_connect("/ws") as ws:
        welcome = join(ws, token)
    assert welcome["id"] == me["id"].replace("-", "")
    assert welcome["players"][0]["name"] == "alice"


def test_same_account_joining_again_kicks_the_old_session(client: TestClient) -> None:
    token = signup(client, "alice")
    with client.websocket_connect("/ws") as old, client.websocket_connect("/ws") as other:
        join(old, token)
        join(other, signup(client, "bob"))
        with client.websocket_connect("/ws") as new:
            welcome = join(new, token)
            assert receive_until(old, "error")["message"] == "logged in from another session"
            with pytest.raises(WebSocketDisconnect):
                receive_until(old, "never")
            # Bob sees alice leave and come back, and the room still has exactly one alice.
            assert receive_until(other, "player_left")["id"] == welcome["id"]
            assert receive_until(other, "player_joined")["player"]["id"] == welcome["id"]
            assert sorted(p["name"] for p in welcome["players"]) == ["alice", "bob"]


@pytest.mark.parametrize("room", ["0" * 32, "lobby"])
def test_join_unknown_channel_is_rejected(client: TestClient, room: str) -> None:
    with client.websocket_connect("/ws") as ws:
        ws.send_json({"type": "join", "room": room, "token": signup(client, "alice")})
        assert ws.receive_json() == {"type": "error", "message": "unknown channel"}
        with pytest.raises(WebSocketDisconnect):
            ws.receive_json()


def test_maps_of_a_channel_are_separate_rooms(client: TestClient) -> None:
    alice, bob = signup(client, "alice"), signup(client, "bob")
    with client.websocket_connect("/ws") as a, client.websocket_connect("/ws") as b:
        join(a, alice)  # no map = the office
        welcome_b = join(b, bob, map="outdoor", x=0.0, z=6.0)
        assert [p["name"] for p in welcome_b["players"]] == ["bob"]
        assert (welcome_b["players"][0]["x"], welcome_b["players"][0]["z"]) == (0.0, 6.0)
        # both count for the channel
        channels = client.get("/channels", headers={"Authorization": f"Bearer {alice}"}).json()
        assert channels["channels"][0]["playerCount"] == 2
        a.send_json({"type": "chat", "text": "anyone outside?"})
        assert receive_until(a, "chat")["text"] == "anyone outside?"
        b.send_json({"type": "chat", "text": "only me"})
        assert receive_until(b, "chat")["text"] == "only me"  # alice's line never came


def test_welcome_carries_the_map(client: TestClient) -> None:
    with client.websocket_connect("/ws") as ws:
        welcome = join(ws, signup(client, "alice"))
        assert welcome["map"]["id"] == "office"
        assert welcome["map"]["layout"] and welcome["map"]["portals"]


def test_travel_through_a_portal_moves_the_player_to_the_other_map(client: TestClient) -> None:
    alice, bob, carol = signup(client, "alice"), signup(client, "bob"), signup(client, "carol")
    with (
        client.websocket_connect("/ws") as a,
        client.websocket_connect("/ws") as inside,
        client.websocket_connect("/ws") as outside,
    ):
        me = join(a, alice)["id"]
        join(inside, bob)
        join(outside, carol, map="outdoor")
        # the office's south exit: x -3.5..1.5, z -6..-5
        a.send_json({"type": "move", "x": -1.0, "z": -5.4, "facing": "down", "moving": True})
        a.send_json({"type": "travel", "to": "outdoor"})
        welcome = receive_until(a, "welcome")
        assert welcome["map"]["id"] == "outdoor"
        mine = next(p for p in welcome["players"] if p["id"] == me)
        assert (mine["x"], mine["z"]) == (0.0, 6.0)  # the portal's spawn point
        assert sorted(p["name"] for p in welcome["players"]) == ["alice", "carol"]
        assert receive_until(inside, "player_left")["id"] == me
        assert receive_until(outside, "player_joined")["player"]["id"] == me


def test_travel_away_from_a_portal_is_refused(client: TestClient) -> None:
    with client.websocket_connect("/ws") as ws:
        join(ws, signup(client, "alice"))  # at the origin, far from the exit
        ws.send_json({"type": "travel", "to": "outdoor"})
        assert receive_until(ws, "error")["message"] == "not at a portal"
        ws.send_json({"type": "chat", "text": "still here"})
        assert receive_until(ws, "chat")["text"] == "still here"


def test_join_unknown_map_is_rejected(client: TestClient) -> None:
    with client.websocket_connect("/ws") as ws:
        ws.send_json(
            {"type": "join", "room": PLAZA, "token": signup(client, "alice"), "map": "moon"}
        )
        assert ws.receive_json() == {"type": "error", "message": "unknown map"}
        with pytest.raises(WebSocketDisconnect):
            ws.receive_json()


@pytest.fixture
def saves(monkeypatch: pytest.MonkeyPatch) -> threading.Semaphore:
    """Released once per finished positions.write (the server saves after the socket closes)."""
    done = threading.Semaphore(0)
    original = positions.write

    async def save_and_signal(*args, **kwargs) -> None:
        await original(*args, **kwargs)
        done.release()

    monkeypatch.setattr(positions, "write", save_and_signal)
    return done


def leave(ws, saves: threading.Semaphore) -> None:
    """Disconnect like a real client and wait until the server saved where the player was.

    Don't poll the database instead: tests share one SQLite connection, and a reader's rollback
    would undo the server's unfinished insert.
    """
    ws.close()
    assert saves.acquire(timeout=2), "position was not saved"


def position_of(welcome: dict) -> tuple[str, float, float]:
    mine = next(p for p in welcome["players"] if p["id"] == welcome["id"])
    return welcome["map"]["id"], mine["x"], mine["z"]


def test_rejoining_starts_where_the_player_left(
    client: TestClient, saves: threading.Semaphore
) -> None:
    token = signup(client, "alice")
    with client.websocket_connect("/ws") as ws:
        assert position_of(join(ws, token)) == ("office", 0.0, 0.0)  # first visit
        ws.send_json({"type": "move", "x": 3.0, "z": -2.0, "facing": "down", "moving": False})
        leave(ws, saves)
    with client.websocket_connect("/ws") as ws:
        assert position_of(join(ws, token)) == ("office", 3.0, -2.0)


def test_rejoining_after_a_portal_starts_on_the_other_map(
    client: TestClient, saves: threading.Semaphore
) -> None:
    token = signup(client, "alice")
    with client.websocket_connect("/ws") as ws:
        join(ws, token)
        ws.send_json({"type": "move", "x": 0.0, "z": -6.5, "facing": "down", "moving": True})
        ws.send_json({"type": "travel", "to": "outdoor"})
        assert receive_until(ws, "welcome")["map"]["id"] == "outdoor"
        leave(ws, saves)
    with client.websocket_connect("/ws") as ws:
        assert position_of(join(ws, token)) == ("outdoor", 0.0, 6.0)


def test_leaving_while_seated_resumes_standing_next_to_the_seat(
    client: TestClient, saves: threading.Semaphore
) -> None:
    token = signup(client, "alice")
    with client.websocket_connect("/ws") as ws:
        join(ws, token)
        ws.send_json({"type": "move", "x": 5.0, "z": 1.5, "facing": "up", "moving": False})
        ws.send_json({"type": "sit", "seat": "sofa"})
        assert any(p["seat"] == "sofa" for p in receive_until(ws, "state")["players"])
        leave(ws, saves)
    with client.websocket_connect("/ws") as ws:
        # not on the sofa itself: that is inside its footprint and the player would be stuck
        assert position_of(join(ws, token)) == ("office", 5.0, 1.5)


def test_a_saved_spot_off_the_floor_falls_back_to_the_start(
    client: TestClient, saves: threading.Semaphore
) -> None:
    token = signup(client, "alice")
    with client.websocket_connect("/ws") as ws:
        join(ws, token)
        # e.g. the map layout shrank since the player left
        ws.send_json({"type": "move", "x": 100.0, "z": 0.0, "facing": "down", "moving": False})
        leave(ws, saves)
    with client.websocket_connect("/ws") as ws:
        assert position_of(join(ws, token)) == ("office", 0.0, 0.0)


def test_each_channel_remembers_its_own_spot(
    client: TestClient, saves: threading.Semaphore
) -> None:
    token = signup(client, "alice")
    other = create_channel(client, token, "garden club")
    with client.websocket_connect("/ws") as ws:
        join(ws, token)
        ws.send_json({"type": "move", "x": 3.0, "z": -2.0, "facing": "down", "moving": False})
        leave(ws, saves)
    with client.websocket_connect("/ws") as ws:
        assert position_of(join(ws, token, room=other)) == ("office", 0.0, 0.0)


def test_an_explicit_map_overrides_the_saved_spot(
    client: TestClient, saves: threading.Semaphore
) -> None:
    token = signup(client, "alice")
    with client.websocket_connect("/ws") as ws:
        join(ws, token)
        ws.send_json({"type": "move", "x": 3.0, "z": -2.0, "facing": "down", "moving": False})
        leave(ws, saves)
    with client.websocket_connect("/ws") as ws:
        welcome = join(ws, token, map="outdoor", x=0.0, z=6.0)
        assert position_of(welcome) == ("outdoor", 0.0, 6.0)
