from fastapi.testclient import TestClient

from dg_village_server.api import MAX_CHANNELS_PER_USER
from dg_village_server.db import DEFAULT_CHANNEL_ID, DEFAULT_CHANNEL_NAME

from conftest import auth, create_channel, signup


def list_channels(client: TestClient, token: str) -> list[dict]:
    response = client.get("/channels", headers=auth(token))
    assert response.status_code == 200, response.text
    return response.json()["channels"]


def test_plaza_exists_on_a_fresh_database(client: TestClient) -> None:
    channels = list_channels(client, signup(client, "alice"))
    assert channels == [
        {
            "id": DEFAULT_CHANNEL_ID.hex,
            "name": DEFAULT_CHANNEL_NAME,
            "ownerId": None,
            "playerCount": 0,
        }
    ]


def test_create_channel(client: TestClient) -> None:
    token = signup(client, "alice")
    user_id = client.get("/me", headers=auth(token)).json()["id"]

    response = client.post("/channels", json={"name": "  우리   동네 "}, headers=auth(token))

    assert response.status_code == 201
    created = response.json()
    assert created["name"] == "우리 동네"
    assert created["ownerId"] == user_id
    assert created["playerCount"] == 0
    assert created in list_channels(client, token)


def test_channels_require_login(client: TestClient) -> None:
    assert client.get("/channels").status_code == 401
    assert client.post("/channels", json={"name": "x"}).status_code == 401


def test_invalid_channel_name_is_rejected(client: TestClient) -> None:
    token = signup(client, "alice")
    for name in ["   ", "x" * 25]:
        response = client.post("/channels", json={"name": name}, headers=auth(token))
        assert response.status_code == 422, name


def test_channel_limit_per_user(client: TestClient) -> None:
    alice, bob = signup(client, "alice"), signup(client, "bob")
    for i in range(MAX_CHANNELS_PER_USER):
        create_channel(client, alice, f"a{i}")

    response = client.post("/channels", json={"name": "one more"}, headers=auth(alice))
    assert response.status_code == 409
    # The limit is per owner.
    create_channel(client, bob, "b0")


def test_list_has_plaza_first_then_busiest(client: TestClient) -> None:
    alice, bob = signup(client, "alice"), signup(client, "bob")
    quiet = create_channel(client, alice, "quiet")
    busy = create_channel(client, alice, "busy")

    with client.websocket_connect("/ws") as ws:
        ws.send_json({"type": "join", "room": busy, "token": bob})
        assert ws.receive_json()["type"] == "welcome"

        channels = list_channels(client, alice)
        assert [c["id"] for c in channels] == [DEFAULT_CHANNEL_ID.hex, busy, quiet]
        assert channels[1]["playerCount"] == 1
        ws.close()
