"""Request/response models shared by the REST API and the WebSocket protocol."""

import uuid
from datetime import datetime
from typing import Annotated, Literal

from pydantic import BaseModel, ConfigDict, Field, StringConstraints

from .weather import Weather

# Hangul syllables, ASCII letters and digits. No spaces, so names can't look alike by spacing.
Nickname = Annotated[
    str, StringConstraints(strip_whitespace=True, pattern=r"^[0-9A-Za-z가-힣]{2,12}$")
]
SpriteSetId = Annotated[str, StringConstraints(pattern=r"^[a-z0-9_]{1,64}$")]
# Unity map scenes a character can be in. Add a scene here when the client gets a new map.
SceneName = Literal["SquareScene"]


# Lowercased after the check (pydantic checks the pattern first), so "Dong" signs in as "dong".
Username = Annotated[
    str,
    StringConstraints(strip_whitespace=True, to_lower=True, pattern=r"^[A-Za-z0-9_]{3,20}$"),
]
Password = Annotated[str, StringConstraints(min_length=4, max_length=128)]


class CredentialsIn(BaseModel):
    """Username and password, for both signup and login."""

    username: Username = Field(
        description="3-20 ASCII letters, digits or _; case-insensitive", examples=["dong"]
    )
    password: Password = Field(description="4-128 characters", examples=["hunter2"])


class AccountOut(BaseModel):
    account_id: uuid.UUID = Field(
        description="Send as X-Account-Id in REST and account_id in the WebSocket join"
    )


class HealthOut(BaseModel):
    status: Literal["ok"] = Field(description='Always "ok" while the server is up')


class ServerOut(BaseModel):
    """A server as clients see it."""

    model_config = ConfigDict(from_attributes=True)

    id: int = Field(description="Server id", examples=[1])
    name: str = Field(description="Server name shown to players", examples=["square"])


class WeatherOut(BaseModel):
    """The weather of a scene for the current period. Same answer for every client."""

    scene: SceneName = Field(description="Scene the weather is for", examples=["SquareScene"])
    weather: Weather = Field(
        description="`sunny` (햇빛), `rain` (비) or `snow` (눈)", examples=["rain"]
    )
    starts_at: datetime = Field(description="When this weather started (UTC)")
    ends_at: datetime = Field(
        description="When the next weather starts (UTC). Ask again after this time"
    )


class CharacterIn(BaseModel):
    """A character to create."""

    nickname: Nickname = Field(
        description="2-12 Hangul syllables, ASCII letters or digits; unique per server",
        examples=["라벤더"],
    )
    sprite_set_id: SpriteSetId = Field(
        description="Client CharacterSpriteSet id", examples=["lavender_girl"]
    )


class CharacterOut(BaseModel):
    """A character as its owner sees it."""

    model_config = ConfigDict(from_attributes=True)

    id: int = Field(description="Character id", examples=[1])
    server_id: int = Field(description="Server the character lives on", examples=[1])
    nickname: str = Field(description="Nickname, unique per server", examples=["라벤더"])
    sprite_set_id: str = Field(
        description="Client CharacterSpriteSet id", examples=["lavender_girl"]
    )
    last_visited_scene: str = Field(
        description="Unity scene the character was last in; load it after welcome",
        examples=["SquareScene"],
    )
    last_x: float = Field(description="Position in last_visited_scene", examples=[1.5])
    last_y: float = Field(description="Position in last_visited_scene", examples=[0.0])
    last_z: float = Field(description="Position in last_visited_scene", examples=[-3.25])
    created_at: datetime = Field(description="When the character was created")
