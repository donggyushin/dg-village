"""WebSocket messages (JSON). The flow is documented in docs/protocol.md at the repo root.

Every message is an object with a "type" field. ClientMessage is everything the client
may send; the server's messages are the *Out models.
"""

import uuid
from typing import Literal

from pydantic import BaseModel, Field, TypeAdapter

from .schemas import CharacterOut


class JoinIn(BaseModel):
    """First message after connecting: enter the game as one of my characters."""

    type: Literal["join"]
    account_id: uuid.UUID = Field(description="Same id as the X-Account-Id header in REST")
    character_id: int = Field(description="One of the caller's characters", examples=[1])


# Only "join" for now. Later messages (move, chat, ...) turn this into a union:
# TypeAdapter(Annotated[JoinIn | MoveIn, Field(discriminator="type")])
ClientMessage = TypeAdapter(JoinIn)


class WelcomeOut(BaseModel):
    """Reply to a valid join. The character's id is the player's id from now on."""

    type: Literal["welcome"] = "welcome"
    character: CharacterOut


ErrorCode = Literal[
    "invalid_message",  # Not JSON, unknown type or missing fields
    "join_timeout",  # No join within JOIN_TIMEOUT_SECONDS of connecting
    "already_joined",  # A second join on the same connection
    "character_not_found",  # No such character, or it belongs to another account
    "already_connected",  # The character is already in the game on another connection
]


class ErrorOut(BaseModel):
    """Something the client did wrong. Errors before welcome close the connection."""

    type: Literal["error"] = "error"
    code: ErrorCode
    message: str = Field(description="For logs and debugging; not meant for players")
