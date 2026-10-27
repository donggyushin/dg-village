"""Database models and engine setup (SQLAlchemy 2, async).

Schema changes go through Alembic (python/migrations); never edit the tables by hand.
"""

import uuid
from datetime import datetime

from sqlalchemy import DateTime, ForeignKey, String, UniqueConstraint, func
from sqlalchemy.ext.asyncio import AsyncEngine, async_sessionmaker, create_async_engine
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column


class Base(DeclarativeBase):
    pass


class Server(Base):
    """A game server players can pick. Seeded with "square" by the first migration."""

    __tablename__ = "server"

    id: Mapped[int] = mapped_column(primary_key=True)
    name: Mapped[str] = mapped_column(String(64), unique=True)


class Character(Base):
    """A player's character on one server.

    There are no accounts yet: account_id is a random id the client generates once and
    keeps, so anyone holding it owns its characters.
    """

    __tablename__ = "character"
    __table_args__ = (UniqueConstraint("server_id", "nickname"),)

    id: Mapped[int] = mapped_column(primary_key=True)
    server_id: Mapped[int] = mapped_column(ForeignKey("server.id"))
    account_id: Mapped[uuid.UUID] = mapped_column(index=True)
    nickname: Mapped[str] = mapped_column(String(12))
    # CharacterSpriteSet id on the client, e.g. "lavender_girl". The server doesn't know
    # the client's catalog, so it only checks the format.
    sprite_set_id: Mapped[str] = mapped_column(String(64))
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())


def create_engine(database_url: str) -> AsyncEngine:
    return create_async_engine(database_url, pool_pre_ping=True)


def create_sessionmaker(engine: AsyncEngine) -> async_sessionmaker:
    return async_sessionmaker(engine, expire_on_commit=False)
