"""WebSocket messages (JSON). The flow is documented in docs/protocol.md at the repo root.

Every message is an object with a "type" field. ClientMessage is everything the client
may send; the server's messages are the *Out models.
"""

import uuid
from typing import Annotated, Literal

from pydantic import BaseModel, Field, StringConstraints, TypeAdapter

from .schemas import CharacterOut, SceneName


class JoinIn(BaseModel):
    """First message after connecting: enter the game as one of my characters."""

    type: Literal["join"]
    account_id: uuid.UUID = Field(description="Same id as the X-Account-Id header in REST")
    character_id: int = Field(description="One of the caller's characters", examples=[1])


class ChangeSceneIn(BaseModel):
    """The player moves to another map scene, at this position. Saved right away."""

    type: Literal["change_scene"]
    scene: SceneName = Field(description="Unity scene to move to", examples=["SquareScene"])
    x: float = Field(allow_inf_nan=False, examples=[1.5])
    y: float = Field(allow_inf_nan=False, examples=[0.0])
    z: float = Field(allow_inf_nan=False, examples=[-3.25])


class MoveIn(BaseModel):
    """The player's current position in its scene. Sent ~10 times a second while moving."""

    type: Literal["move"]
    x: float = Field(allow_inf_nan=False, examples=[1.5])
    y: float = Field(allow_inf_nan=False, examples=[0.0])
    z: float = Field(allow_inf_nan=False, examples=[-3.25])


# A platform is something players stand on that moves by itself (a ferry, later a lift): its
# position is shared by everyone, so a rider's position is sent relative to it. Ids are fixed
# strings known to both sides; the server also checks them against PLATFORMS (platforms.py).
PlatformId = Annotated[str, StringConstraints(pattern=r"^[a-z0-9_]{1,40}$")]


class RideMoveIn(BaseModel):
    """The player is standing on a platform: its position in that platform's local
    coordinates. Sent instead of move while on board; the next move means it got off."""

    type: Literal["ride_move"]
    platform: PlatformId = Field(description="Platform id", examples=["ferry_willow"])
    x: float = Field(allow_inf_nan=False, examples=[1.2])
    y: float = Field(allow_inf_nan=False, examples=[0.0])
    z: float = Field(allow_inf_nan=False, examples=[-3.0])


# Control characters are refused: a line break would fake extra chat lines.
ChatText = Annotated[
    str,
    StringConstraints(
        strip_whitespace=True, min_length=1, max_length=200, pattern=r"^[^\x00-\x1f\x7f]+$"
    ),
]


class ChatIn(BaseModel):
    """The player says something to everyone in its scene. Not stored anywhere."""

    type: Literal["chat"]
    text: ChatText = Field(description="1-200 characters, no control characters")


ClientMessage = TypeAdapter(
    Annotated[JoinIn | ChangeSceneIn | MoveIn | RideMoveIn | ChatIn, Field(discriminator="type")]
)


class WelcomeOut(BaseModel):
    """Reply to a valid join. The character's id is the player's id from now on."""

    type: Literal["welcome"] = "welcome"
    character: CharacterOut


class SceneChangedOut(BaseModel):
    """Reply to change_scene: the server now has the player in this scene. Load it and
    spawn at this position."""

    type: Literal["scene_changed"] = "scene_changed"
    scene: SceneName
    x: float = Field(examples=[1.5])
    y: float = Field(examples=[0.0])
    z: float = Field(examples=[-3.25])


class RidingOut(BaseModel):
    """Where a player stands on a platform, in the platform's local coordinates."""

    platform: str = Field(examples=["ferry_willow"])
    x: float = Field(examples=[1.2])
    y: float = Field(examples=[0.0])
    z: float = Field(examples=[-3.0])


class PlayerOut(BaseModel):
    """Another player as the players in the same scene see it."""

    id: int = Field(description="Character id", examples=[3])
    nickname: str = Field(examples=["라벤더"])
    sprite_set_id: str = Field(
        description="Client CharacterSpriteSet id", examples=["lavender_girl"]
    )
    x: float = Field(examples=[1.5])
    y: float = Field(examples=[0.0])
    z: float = Field(examples=[-3.25])
    riding: RidingOut | None = Field(
        default=None,
        description="Set while on a platform; then x, y, z are the last world position "
        "before boarding and should be ignored",
    )


class ScenePlayersOut(BaseModel):
    """Sent when I enter a scene (after welcome or scene_changed): who is already there."""

    type: Literal["scene_players"] = "scene_players"
    players: list[PlayerOut] = Field(description="Everyone else in the scene; may be empty")


class PlayerJoinedOut(BaseModel):
    """Someone entered my scene."""

    type: Literal["player_joined"] = "player_joined"
    player: PlayerOut


class PlayerMovedOut(BaseModel):
    """Someone in my scene sent a move. Relayed as is, ~10 times a second per moving player."""

    type: Literal["player_moved"] = "player_moved"
    id: int = Field(description="Character id", examples=[3])
    x: float = Field(examples=[1.5])
    y: float = Field(examples=[0.0])
    z: float = Field(examples=[-3.25])


class PlayerRideMovedOut(BaseModel):
    """Someone in my scene sent a ride_move. Relayed as is, like player_moved."""

    type: Literal["player_ride_moved"] = "player_ride_moved"
    id: int = Field(description="Character id", examples=[3])
    platform: str = Field(examples=["ferry_willow"])
    x: float = Field(examples=[1.2])
    y: float = Field(examples=[0.0])
    z: float = Field(examples=[-3.0])


class PlayerLeftOut(BaseModel):
    """Someone left my scene: changed scene or disconnected."""

    type: Literal["player_left"] = "player_left"
    id: int = Field(description="Character id", examples=[3])


class ChatMessageOut(BaseModel):
    """Someone in my scene said something. I get my own messages too. The server fills in
    the speaker, so it can't be faked."""

    type: Literal["chat_message"] = "chat_message"
    id: int = Field(description="Character id of the speaker", examples=[3])
    nickname: str = Field(examples=["라벤더"])
    text: str = Field(examples=["안녕하세요"])


ErrorCode = Literal[
    "invalid_message",  # Not JSON, unknown type or missing fields
    "join_timeout",  # No join within JOIN_TIMEOUT_SECONDS of connecting
    "not_joined",  # The first message wasn't join
    "already_joined",  # A second join on the same connection
    "character_not_found",  # No such character, or it belongs to another account
    "already_connected",  # The character is already in the game on another connection
    "chat_too_fast",  # Chat sent within CHAT_MIN_INTERVAL_SECONDS of the previous one
    "unknown_platform",  # ride_move with a platform id that isn't in the player's scene
]


class ErrorOut(BaseModel):
    """Something the client did wrong. Errors before welcome close the connection."""

    type: Literal["error"] = "error"
    code: ErrorCode
    message: str = Field(description="For logs and debugging; not meant for players")
