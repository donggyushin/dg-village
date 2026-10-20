from collections.abc import Iterator

import pytest
from fastapi.testclient import TestClient

from dg_village_server.config import Settings
from dg_village_server.main import create_app

TEST_SETTINGS = Settings(
    database_url="sqlite+aiosqlite:///:memory:",
    jwt_secret="test-secret-at-least-32-bytes-long!!",
)


@pytest.fixture
def client() -> Iterator[TestClient]:
    with TestClient(create_app(TEST_SETTINGS, create_tables=True)) as c:
        yield c


def signup(client: TestClient, name: str, character_id: str = "girl01") -> str:
    """New anonymous account with a profile; returns its access token."""
    token = client.post("/auth/anonymous").json()["accessToken"]
    response = client.patch(
        "/me",
        json={"displayName": name, "characterId": character_id},
        headers={"Authorization": f"Bearer {token}"},
    )
    assert response.status_code == 200, response.text
    return token
