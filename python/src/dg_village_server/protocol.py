"""JSON message schema shared with the Unity client.

Keys are camelCase on the wire (Unity convention), snake_case in Python.
"""

from typing import Annotated, Literal

from pydantic import BaseModel, ConfigDict, Field, TypeAdapter, field_validator
from pydantic.alias_generators import to_camel

# Diagonals added 2026-10-22 (8-direction characters). Clients built before that map
# unknown values to "down", so new values never break them.
Facing = Literal["down", "up", "left", "right", "downleft", "downright", "upleft", "upright"]

CHAT_MAX_LENGTH = 200
DISPLAY_NAME_MAX_LENGTH = 16
CHANNEL_NAME_MAX_LENGTH = 24


def _normalize_name(value: str, max_length: int) -> str:
    """Collapse whitespace; 1-max_length characters after that."""
    value = " ".join(value.split())
    if not value:
        raise ValueError("empty name")
    if len(value) > max_length:
        raise ValueError(f"name longer than {max_length} characters")
    return value


def normalize_display_name(value: str) -> str:
    return _normalize_name(value, DISPLAY_NAME_MAX_LENGTH)


def normalize_channel_name(value: str) -> str:
    return _normalize_name(value, CHANNEL_NAME_MAX_LENGTH)


class CamelModel(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)

    def to_json(self) -> str:
        return self.model_dump_json(by_alias=True)


# ----- client -> server -----


class JoinMessage(CamelModel):
    """Name and character come from the account (PATCH /me), not from this message."""

    type: Literal["join"]
    # Channel id as 32 hex digits (GET /channels). Unknown ids are rejected.
    room: str = Field(min_length=1, max_length=32)
    # Access token from POST /auth/login or /auth/anonymous.
    token: str = Field(min_length=1, max_length=2048)
    # Map inside the channel (maps.MAP_IDS). None = where the player last left this channel
    # (db.SavedPosition), or the office at the origin the first time.
    map: str | None = Field(default=None, min_length=1, max_length=32)
    # Where the player appears on `map` (ignored without `map`); the first move takes over.
    x: float = Field(default=0.0, allow_inf_nan=False)
    z: float = Field(default=0.0, allow_inf_nan=False)


class MoveMessage(CamelModel):
    type: Literal["move"]
    x: float = Field(allow_inf_nan=False)
    z: float = Field(allow_inf_nan=False)
    facing: Facing
    moving: bool


class ChatMessage(CamelModel):
    type: Literal["chat"]
    text: str = Field(max_length=CHAT_MAX_LENGTH)

    @field_validator("text")
    @classmethod
    def _normalize_whitespace(cls, value: str) -> str:
        # Newlines/tabs/repeated spaces collapse to single spaces so a bubble can't be stretched.
        value = " ".join(value.split())
        if not value:
            raise ValueError("empty chat message")
        return value


class SitMessage(CamelModel):
    """Sit on a seat of the map (maps/<id>.json). Must be near it and it must be free."""

    type: Literal["sit"]
    seat: str = Field(min_length=1, max_length=32)


class StandMessage(CamelModel):
    """Get up. Moving away from the seat also stands up."""

    type: Literal["stand"]


class TravelMessage(CamelModel):
    """Walk through a portal of the current map to the map `to`. Answered by a new `welcome`."""

    type: Literal["travel"]
    to: str = Field(min_length=1, max_length=32)


ClientMessage = Annotated[
    JoinMessage | MoveMessage | ChatMessage | SitMessage | StandMessage | TravelMessage,
    Field(discriminator="type"),
]
client_message_adapter: TypeAdapter[ClientMessage] = TypeAdapter(ClientMessage)


# ----- server -> client -----


class PlayerState(CamelModel):
    id: str
    name: str
    character_id: str
    x: float = 0.0
    z: float = 0.0
    facing: Facing = "down"
    moving: bool = False
    # Seat id while sitting, else None.
    seat: str | None = None


class PetState(CamelModel):
    """An animal wandering the map on its own (pets.py). Drawn like a player, without a name."""

    id: str
    character_id: str
    x: float = 0.0
    z: float = 0.0
    facing: Facing = "down"
    moving: bool = False


class WelcomeMessage(CamelModel):
    """Sent on join and after each travel: own id, everyone in the room (incl. self) and the map."""

    type: Literal["welcome"] = "welcome"
    id: str
    players: list[PlayerState]
    pets: list[PetState] = []
    # maps/<id>.json as-is; None for a room without a map (tests)
    map: dict | None = None


class PlayerJoinedMessage(CamelModel):
    type: Literal["player_joined"] = "player_joined"
    player: PlayerState


class PlayerLeftMessage(CamelModel):
    type: Literal["player_left"] = "player_left"
    id: str


class StateMessage(CamelModel):
    """Periodic snapshot of every player in the room (sent at the tick rate)."""

    type: Literal["state"] = "state"
    players: list[PlayerState]
    # Old clients ignore this (JsonUtility skips unknown fields)
    pets: list[PetState] = []


class ChatBroadcastMessage(CamelModel):
    """A chat line, sent to everyone in the room including the sender."""

    type: Literal["chat"] = "chat"
    id: str
    name: str
    text: str


class ErrorMessage(CamelModel):
    type: Literal["error"] = "error"
    message: str
