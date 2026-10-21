import pytest
from fastapi.testclient import TestClient
from starlette.websockets import WebSocketDisconnect

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


def test_walking_through_a_portal_moves_the_player_to_the_other_room(client: TestClient) -> None:
    alice, bob = signup(client, "alice"), signup(client, "bob")
    with client.websocket_connect("/ws") as other:
        join(other, bob, map="outdoor")
        with client.websocket_connect("/ws") as office:
            welcome = join(office, alice)
            # the client opens a new session on the target map; the office one gets replaced
            with client.websocket_connect("/ws") as outdoor:
                welcome_out = join(outdoor, alice, map="outdoor", x=0.0, z=6.0)
                assert sorted(p["name"] for p in welcome_out["players"]) == ["alice", "bob"]
                assert receive_until(other, "player_joined")["player"]["id"] == welcome["id"]


def test_join_unknown_map_is_rejected(client: TestClient) -> None:
    with client.websocket_connect("/ws") as ws:
        ws.send_json(
            {"type": "join", "room": PLAZA, "token": signup(client, "alice"), "map": "moon"}
        )
        assert ws.receive_json() == {"type": "error", "message": "unknown map"}
        with pytest.raises(WebSocketDisconnect):
            ws.receive_json()
