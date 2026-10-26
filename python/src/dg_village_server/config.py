"""Settings read from environment variables. Defaults are for local development only."""

import os
from dataclasses import dataclass

# Matches compose.yaml at the repo root.
DEFAULT_DATABASE_URL = "postgresql+asyncpg://dg_village:dg_village@localhost:5432/dg_village"


@dataclass(frozen=True)
class Settings:
    database_url: str

    @classmethod
    def from_env(cls) -> "Settings":
        return cls(database_url=os.environ.get("DG_DATABASE_URL", DEFAULT_DATABASE_URL))
