"""Request/response models shared by the REST API and the WebSocket protocol."""

from datetime import datetime
from typing import Annotated, Literal

from pydantic import BaseModel, ConfigDict, Field, StringConstraints

# Hangul syllables, ASCII letters and digits. No spaces, so names can't look alike by spacing.
Nickname = Annotated[
    str, StringConstraints(strip_whitespace=True, pattern=r"^[0-9A-Za-z가-힣]{2,12}$")
]
SpriteSetId = Annotated[str, StringConstraints(pattern=r"^[a-z0-9_]{1,64}$")]


class HealthOut(BaseModel):
    status: Literal["ok"] = Field(description='Always "ok" while the server is up')


class ServerOut(BaseModel):
    """A server as clients see it."""

    model_config = ConfigDict(from_attributes=True)

    id: int = Field(description="Server id", examples=[1])
    name: str = Field(description="Server name shown to players", examples=["square"])


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
    created_at: datetime = Field(description="When the character was created")
