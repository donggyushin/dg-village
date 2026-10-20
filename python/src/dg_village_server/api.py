"""HTTP endpoints for accounts. The game itself runs over the WebSocket in main.py.

POST  /auth/anonymous  new account + device credential (client stores it) + access token
POST  /auth/login      {provider, credential} -> access token
GET   /me              own profile                      (Authorization: Bearer <token>)
PATCH /me              change displayName / characterId (Authorization: Bearer <token>)
"""

import random
import uuid
from datetime import UTC, datetime
from typing import Annotated

from fastapi import APIRouter, Depends, HTTPException, Request, status
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from pydantic import Field, field_validator
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from .auth import PROVIDERS, AnonymousProvider, AuthError, issue_access_token, read_access_token
from .config import Settings
from .db import CHARACTER_ID_MAX_LENGTH, AuthIdentity, User
from .protocol import DISPLAY_NAME_MAX_LENGTH, CamelModel, normalize_display_name

router = APIRouter()


# ----- schemas -----


class SignupResponse(CamelModel):
    user_id: uuid.UUID
    # Shown exactly once. The client must keep it: it is the only way back into this account.
    credential: str
    access_token: str


class LoginRequest(CamelModel):
    provider: str = Field(max_length=32)
    credential: str = Field(max_length=4096)


class LoginResponse(CamelModel):
    user_id: uuid.UUID
    access_token: str


class Profile(CamelModel):
    id: uuid.UUID
    display_name: str
    character_id: str


class ProfileUpdate(CamelModel):
    display_name: str | None = Field(default=None, max_length=64)
    character_id: str | None = Field(default=None, min_length=1, max_length=CHARACTER_ID_MAX_LENGTH)

    @field_validator("display_name")
    @classmethod
    def _normalize(cls, value: str | None) -> str | None:
        return None if value is None else normalize_display_name(value)


# ----- dependencies -----


async def get_session(request: Request):
    async with request.app.state.sessionmaker() as session:
        yield session


def get_settings(request: Request) -> Settings:
    return request.app.state.settings


Session = Annotated[AsyncSession, Depends(get_session)]
AppSettings = Annotated[Settings, Depends(get_settings)]

_bearer = HTTPBearer(auto_error=False)


async def get_current_user(
    session: Session,
    settings: AppSettings,
    credentials: Annotated[HTTPAuthorizationCredentials | None, Depends(_bearer)],
) -> User:
    if credentials is None:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "missing token")
    try:
        user_id = read_access_token(credentials.credentials, settings)
    except AuthError as e:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, str(e)) from e
    user = await session.get(User, user_id)
    if user is None:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "unknown user")
    return user


CurrentUser = Annotated[User, Depends(get_current_user)]


# ----- endpoints -----


@router.post("/auth/anonymous", response_model=SignupResponse)
async def signup_anonymous(session: Session, settings: AppSettings) -> SignupResponse:
    provider = AnonymousProvider()
    credential = provider.new_credential()
    user = User(display_name=random_guest_name())
    session.add(user)
    await session.flush()  # assigns user.id
    session.add(
        AuthIdentity(
            user_id=user.id,
            provider=provider.name,
            subject=await provider.subject_for(credential),
        )
    )
    await session.commit()
    return SignupResponse(
        user_id=user.id,
        credential=credential,
        access_token=issue_access_token(user.id, settings),
    )


@router.post("/auth/login", response_model=LoginResponse)
async def login(body: LoginRequest, session: Session, settings: AppSettings) -> LoginResponse:
    provider = PROVIDERS.get(body.provider)
    if provider is None:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "unknown provider")
    try:
        subject = await provider.subject_for(body.credential)
    except AuthError as e:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, str(e)) from e

    identity = await session.scalar(
        select(AuthIdentity).where(
            AuthIdentity.provider == provider.name, AuthIdentity.subject == subject
        )
    )
    if identity is None:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "unknown credential")

    user = await session.get(User, identity.user_id)
    user.last_login_at = datetime.now(UTC)
    await session.commit()
    return LoginResponse(user_id=user.id, access_token=issue_access_token(user.id, settings))


@router.get("/me", response_model=Profile)
async def get_me(user: CurrentUser) -> Profile:
    return to_profile(user)


@router.patch("/me", response_model=Profile)
async def update_me(body: ProfileUpdate, user: CurrentUser, session: Session) -> Profile:
    if body.display_name is not None:
        user.display_name = body.display_name
    if body.character_id is not None:
        user.character_id = body.character_id
    await session.commit()
    return to_profile(user)


def to_profile(user: User) -> Profile:
    return Profile(id=user.id, display_name=user.display_name, character_id=user.character_id)


def random_guest_name() -> str:
    name = f"guest{random.randint(1000, 9999)}"
    assert len(name) <= DISPLAY_NAME_MAX_LENGTH
    return name
