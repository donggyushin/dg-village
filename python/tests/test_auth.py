import uuid
from dataclasses import replace

import pytest
from fastapi.testclient import TestClient

from dg_village_server.auth import issue_access_token

from conftest import TEST_SETTINGS


def auth(token: str) -> dict[str, str]:
    return {"Authorization": f"Bearer {token}"}


def test_anonymous_signup_gives_a_guest_profile(client: TestClient) -> None:
    signup = client.post("/auth/anonymous").json()
    assert len(signup["credential"]) >= 32

    me = client.get("/me", headers=auth(signup["accessToken"])).json()
    assert me["id"] == signup["userId"]
    assert me["displayName"].startswith("guest")
    assert me["characterId"] == ""


def test_login_with_credential_returns_same_account(client: TestClient) -> None:
    signup = client.post("/auth/anonymous").json()

    login = client.post(
        "/auth/login", json={"provider": "anonymous", "credential": signup["credential"]}
    )
    assert login.status_code == 200
    assert login.json()["userId"] == signup["userId"]
    me = client.get("/me", headers=auth(login.json()["accessToken"])).json()
    assert me["id"] == signup["userId"]


@pytest.mark.parametrize(
    ("provider", "credential", "status"),
    [
        ("anonymous", "x" * 43, 401),  # well-formed but never issued
        ("anonymous", "short", 401),
        ("steam", "x" * 43, 400),  # not supported yet
    ],
)
def test_login_failures(client: TestClient, provider: str, credential: str, status: int) -> None:
    response = client.post("/auth/login", json={"provider": provider, "credential": credential})
    assert response.status_code == status


def test_profile_update(client: TestClient) -> None:
    token = client.post("/auth/anonymous").json()["accessToken"]

    response = client.patch(
        "/me", json={"displayName": "  신동규 \n ", "characterId": "boy01"}, headers=auth(token)
    )
    assert response.status_code == 200
    assert response.json()["displayName"] == "신동규"
    assert response.json()["characterId"] == "boy01"

    # Partial update keeps the other field.
    client.patch("/me", json={"characterId": "girl01"}, headers=auth(token))
    me = client.get("/me", headers=auth(token)).json()
    assert (me["displayName"], me["characterId"]) == ("신동규", "girl01")


@pytest.mark.parametrize("name", ["", "   ", "x" * 17])
def test_invalid_display_name_is_rejected(client: TestClient, name: str) -> None:
    token = client.post("/auth/anonymous").json()["accessToken"]
    response = client.patch("/me", json={"displayName": name}, headers=auth(token))
    assert response.status_code == 422


def test_me_requires_a_valid_token(client: TestClient) -> None:
    assert client.get("/me").status_code == 401
    assert client.get("/me", headers=auth("garbage")).status_code == 401

    user_id = client.post("/auth/anonymous").json()["userId"]
    expired = issue_access_token(
        uuid.UUID(user_id), replace(TEST_SETTINGS, access_token_ttl_seconds=-1)
    )
    response = client.get("/me", headers=auth(expired))
    assert response.status_code == 401
    assert response.json()["detail"] == "token expired"
