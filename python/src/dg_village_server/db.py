"""Database models and engine setup (SQLAlchemy 2, async).

Schema changes go through Alembic (python/migrations); never edit the tables by hand.
"""

import uuid
from datetime import UTC, datetime

from sqlalchemy import DateTime, ForeignKey, String, UniqueConstraint
from sqlalchemy.ext.asyncio import AsyncEngine, async_sessionmaker, create_async_engine
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column
from sqlalchemy.pool import StaticPool

from .protocol import CHANNEL_NAME_MAX_LENGTH, DISPLAY_NAME_MAX_LENGTH

CHARACTER_ID_MAX_LENGTH = 32

# Fixed id so every server and client agrees on where "just play" goes.
DEFAULT_CHANNEL_ID = uuid.UUID("00000000-0000-0000-0000-000000000001")
DEFAULT_CHANNEL_NAME = "광장"


def _now() -> datetime:
    return datetime.now(UTC)


class Base(DeclarativeBase):
    pass


class User(Base):
    """A player account. Holds only the profile; how someone logs in lives in AuthIdentity."""

    __tablename__ = "users"

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    # Display only: not unique and never used to identify anyone.
    display_name: Mapped[str] = mapped_column(String(DISPLAY_NAME_MAX_LENGTH))
    # Empty until the player picks one; clients fall back to their default character.
    character_id: Mapped[str] = mapped_column(String(CHARACTER_ID_MAX_LENGTH), default="")
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=_now)
    last_login_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=_now)


class AuthIdentity(Base):
    """One way to log in to a user: (provider, subject) is unique.

    anonymous: subject = SHA-256 of the client's device credential.
    Future providers (steam, discord, ...) add rows here pointing at the same user.
    """

    __tablename__ = "auth_identities"
    __table_args__ = (UniqueConstraint("provider", "subject"),)

    id: Mapped[int] = mapped_column(primary_key=True)
    user_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("users.id", ondelete="CASCADE"), index=True
    )
    provider: Mapped[str] = mapped_column(String(32))
    subject: Mapped[str] = mapped_column(String(255))
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=_now)


class Channel(Base):
    """A place players can join. The game room for a channel is keyed by its id (hex).

    Who is inside is not stored here: that lives in memory (room.RoomManager).
    """

    __tablename__ = "channels"

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    name: Mapped[str] = mapped_column(String(CHANNEL_NAME_MAX_LENGTH))
    # NULL = system channel (the default plaza). Deleting the owner keeps the channel.
    owner_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("users.id", ondelete="SET NULL"), index=True
    )
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=_now)


async def ensure_default_channel(sessionmaker: async_sessionmaker) -> None:
    """The plaza always exists, so a fresh database still has somewhere to go."""
    async with sessionmaker() as session:
        if await session.get(Channel, DEFAULT_CHANNEL_ID) is None:
            session.add(Channel(id=DEFAULT_CHANNEL_ID, name=DEFAULT_CHANNEL_NAME, owner_id=None))
            await session.commit()


def create_engine(database_url: str) -> AsyncEngine:
    if database_url.startswith("sqlite") and ":memory:" in database_url:
        # Tests: one shared connection, otherwise every session sees a fresh empty database.
        return create_async_engine(
            database_url, poolclass=StaticPool, connect_args={"check_same_thread": False}
        )
    return create_async_engine(database_url, pool_pre_ping=True)


def create_sessionmaker(engine: AsyncEngine) -> async_sessionmaker:
    return async_sessionmaker(engine, expire_on_commit=False)
