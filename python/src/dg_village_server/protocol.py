"""JSON message schema shared with the Unity client.

Keys are camelCase on the wire (Unity convention), snake_case in Python.
"""

from typing import Annotated, Literal

from pydantic import BaseModel, ConfigDict, Field, TypeAdapter
from pydantic.alias_generators import to_camel

Facing = Literal["down", "up", "left", "right"]


class CamelModel(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)

    def to_json(self) -> str:
        return self.model_dump_json(by_alias=True)


# ----- client -> server -----


class JoinMessage(CamelModel):
    type: Literal["join"]
    room: str = Field(min_length=1, max_length=32)
    name: str = Field(min_length=1, max_length=16)
    character_id: str = Field(min_length=1, max_length=32)


class MoveMessage(CamelModel):
    type: Literal["move"]
    x: float = Field(allow_inf_nan=False)
    z: float = Field(allow_inf_nan=False)
    facing: Facing
    moving: bool


ClientMessage = Annotated[JoinMessage | MoveMessage, Field(discriminator="type")]
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


class WelcomeMessage(CamelModel):
    """Sent once to the joining client: its own id and everyone already in the room."""

    type: Literal["welcome"] = "welcome"
    id: str
    players: list[PlayerState]


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


class ErrorMessage(CamelModel):
    type: Literal["error"] = "error"
    message: str
