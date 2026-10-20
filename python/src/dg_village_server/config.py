"""Settings read from environment variables. Defaults are for local development only."""

import logging
import os
from dataclasses import dataclass

logger = logging.getLogger(__name__)

# Matches compose.yaml at the repo root.
DEFAULT_DATABASE_URL = "postgresql+asyncpg://dg_village:dg_village@localhost:5432/dg_village"
DEV_JWT_SECRET = "dev-only-insecure-jwt-secret-do-not-deploy"


@dataclass(frozen=True)
class Settings:
    database_url: str
    jwt_secret: str
    access_token_ttl_seconds: int = 24 * 60 * 60

    @classmethod
    def from_env(cls) -> "Settings":
        secret = os.environ.get("DG_JWT_SECRET", DEV_JWT_SECRET)
        if secret == DEV_JWT_SECRET:
            logger.warning("DG_JWT_SECRET is not set; using the insecure development secret")
        return cls(
            database_url=os.environ.get("DG_DATABASE_URL", DEFAULT_DATABASE_URL),
            jwt_secret=secret,
        )
