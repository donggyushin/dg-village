from collections.abc import Iterator

import pytest
from fastapi.testclient import TestClient
from starlette.websockets import WebSocketDisconnect

from dg_village_server.main import create_app


@pytest.fixture
def client() -> Iterator[TestClient]:
    with TestClient(create_app()) as c:
        yield c


def join(ws, name: str, room: str = "lobby") -> dict:
    ws.send_json({"type": "join", "room": room, "name": name, "characterId": "girl01"})
    welcome = ws.receive_json()
    assert welcome["type"] == "welcome"
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
        welcome_a = join(a, "alice")
        assert [p["id"] for p in welcome_a["players"]] == [welcome_a["id"]]

        with client.websocket_connect("/ws") as b:
            welcome_b = join(b, "bob")
            names = {p["name"] for p in welcome_b["players"]}
            assert names == {"alice", "bob"}
            assert welcome_b["players"][0]["characterId"] == "girl01"

            joined = receive_until(a, "player_joined")
            assert joined["player"]["id"] == welcome_b["id"]


def test_move_is_broadcast_in_state(client: TestClient) -> None:
    with client.websocket_connect("/ws") as a, client.websocket_connect("/ws") as b:
        join(a, "alice")
        welcome_b = join(b, "bob")

        b.send_json({"type": "move", "x": 1.5, "z": -2.0, "facing": "left", "moving": True})
        state = receive_until(a, "state")
        bob = next(p for p in state["players"] if p["id"] == welcome_b["id"])
        assert (bob["x"], bob["z"], bob["facing"], bob["moving"]) == (1.5, -2.0, "left", True)


def test_leave_is_broadcast(client: TestClient) -> None:
    with client.websocket_connect("/ws") as a:
        join(a, "alice")
        with client.websocket_connect("/ws") as b:
            welcome_b = join(b, "bob")
            # Close explicitly: leaving the `with` block cancels the server task
            # instead of delivering a disconnect like a real client would.
            b.close()
            left = receive_until(a, "player_left")
        assert left["id"] == welcome_b["id"]


def test_rooms_are_isolated(client: TestClient) -> None:
    with client.websocket_connect("/ws") as a, client.websocket_connect("/ws") as b:
        join(a, "alice", room="r1")
        welcome_b = join(b, "bob", room="r2")
        assert len(welcome_b["players"]) == 1


def test_first_message_must_be_join(client: TestClient) -> None:
    with client.websocket_connect("/ws") as ws:
        ws.send_json({"type": "move", "x": 0, "z": 0, "facing": "down", "moving": False})
        assert ws.receive_json()["type"] == "error"
        with pytest.raises(WebSocketDisconnect):
            ws.receive_json()


def test_invalid_message_after_join_keeps_connection(client: TestClient) -> None:
    with client.websocket_connect("/ws") as ws:
        join(ws, "alice")
        ws.send_text("not json")
        assert receive_until(ws, "error")["message"].startswith("invalid message")
        ws.send_json({"type": "move", "x": 1, "z": 1, "facing": "up", "moving": False})
        assert receive_until(ws, "state")["type"] == "state"


def test_chat_is_broadcast_to_everyone_including_sender(client: TestClient) -> None:
    with client.websocket_connect("/ws") as a, client.websocket_connect("/ws") as b:
        welcome_a = join(a, "alice")
        join(b, "bob")

        a.send_json({"type": "chat", "text": "  hello\n\n  world  "})
        expected = {"type": "chat", "id": welcome_a["id"], "name": "alice", "text": "hello world"}
        for ws in (a, b):
            assert receive_until(ws, "chat") == expected


@pytest.mark.parametrize("text", ["", "   \n ", "x" * 201])
def test_invalid_chat_is_rejected(client: TestClient, text: str) -> None:
    with client.websocket_connect("/ws") as ws:
        join(ws, "alice")
        ws.send_json({"type": "chat", "text": text})
        assert receive_until(ws, "error")["message"].startswith("invalid message")


def test_chat_rate_limit(client: TestClient) -> None:
    with client.websocket_connect("/ws") as ws:
        join(ws, "alice")
        ws.send_json({"type": "chat", "text": "one"})
        ws.send_json({"type": "chat", "text": "two"})
        assert receive_until(ws, "chat")["text"] == "one"
        assert receive_until(ws, "error")["message"] == "chatting too fast"
