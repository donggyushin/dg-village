"""WebSocket messages (JSON). The flow is documented in docs/protocol.md at the repo root.

Every message is an object with a "type" field. ClientMessage is everything the client
may send; the server's messages are the *Out models.
"""

import uuid
from typing import Annotated, Literal

from pydantic import BaseModel, Field, TypeAdapter

from .schemas import CharacterOut, SceneName


class JoinIn(BaseModel):
    """First message after connecting: enter the game as one of my characters."""

    type: Literal["join"]
    account_id: uuid.UUID = Field(description="Same id as the X-Account-Id header in REST")
    character_id: int = Field(description="One of the caller's characters", examples=[1])


class ChangeSceneIn(BaseModel):
    """The player moves to another map scene. Saved right away as last_visited_scene."""

    type: Literal["change_scene"]
    scene: SceneName = Field(description="Unity scene to move to", examples=["SquareScene"])


class MoveIn(BaseModel):
    """The player's current position in its scene. Sent ~10 times a second while moving."""

    type: Literal["move"]
    x: float = Field(allow_inf_nan=False, examples=[1.5])
    y: float = Field(allow_inf_nan=False, examples=[0.0])
    z: float = Field(allow_inf_nan=False, examples=[-3.25])


ClientMessage = TypeAdapter(Annotated[JoinIn | ChangeSceneIn | MoveIn, Field(discriminator="type")])


class WelcomeOut(BaseModel):
    """Reply to a valid join. The character's id is the player's id from now on."""

    type: Literal["welcome"] = "welcome"
    character: CharacterOut


class SceneChangedOut(BaseModel):
    """Reply to change_scene: the server now has the player in this scene. Load it."""

    type: Literal["scene_changed"] = "scene_changed"
    scene: SceneName


ErrorCode = Literal[
    "invalid_message",  # Not JSON, unknown type or missing fields
    "join_timeout",  # No join within JOIN_TIMEOUT_SECONDS of connecting
    "not_joined",  # The first message wasn't join
    "already_joined",  # A second join on the same connection
    "character_not_found",  # No such character, or it belongs to another account
    "already_connected",  # The character is already in the game on another connection
]


class ErrorOut(BaseModel):
    """Something the client did wrong. Errors before welcome close the connection."""

    type: Literal["error"] = "error"
    code: ErrorCode
    message: str = Field(description="For logs and debugging; not meant for players")
