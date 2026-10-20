"""Who a player is.

Login = turn a provider-specific proof into a stable (provider, subject) pair, find the
AuthIdentity row for it, and hand out a short-lived JWT access token for that user.
The game server (WebSocket join) only ever checks the access token, so adding a login
method later (Steam session ticket, Discord OAuth, ...) means adding one AuthProvider and
an endpoint to link it — nothing in the game loop changes.

Today the only provider is "anonymous": the server generates a random device credential at
signup, the client keeps it, and the database stores only its SHA-256.
"""

import hashlib
import secrets
import uuid
from datetime import UTC, datetime, timedelta
from typing import Protocol

import jwt

from .config import Settings

_JWT_ALGORITHM = "HS256"


class AuthError(Exception):
    """Bad credential or token. The message is safe to show to the client."""


class AuthProvider(Protocol):
    name: str

    async def subject_for(self, credential: str) -> str:
        """Verify the proof and return the stable subject it identifies. Raise AuthError."""
        ...


class AnonymousProvider:
    name = "anonymous"

    @staticmethod
    def new_credential() -> str:
        return secrets.token_urlsafe(32)

    async def subject_for(self, credential: str) -> str:
        if not 32 <= len(credential) <= 128:
            raise AuthError("invalid credential")
        return hashlib.sha256(credential.encode()).hexdigest()


PROVIDERS: dict[str, AuthProvider] = {p.name: p for p in (AnonymousProvider(),)}


def issue_access_token(user_id: uuid.UUID, settings: Settings) -> str:
    now = datetime.now(UTC)
    payload = {
        "sub": str(user_id),
        "iat": now,
        "exp": now + timedelta(seconds=settings.access_token_ttl_seconds),
    }
    return jwt.encode(payload, settings.jwt_secret, algorithm=_JWT_ALGORITHM)


def read_access_token(token: str, settings: Settings) -> uuid.UUID:
    try:
        payload = jwt.decode(token, settings.jwt_secret, algorithms=[_JWT_ALGORITHM])
        return uuid.UUID(payload["sub"])
    except jwt.ExpiredSignatureError as e:
        raise AuthError("token expired") from e
    except (jwt.InvalidTokenError, KeyError, ValueError) as e:
        raise AuthError("invalid token") from e
