"""character last_visited_scene

Revision ID: 4d3af0380f75
Revises: dfdc692db230
Create Date: 2026-10-28 18:12:14.507077

"""

from collections.abc import Sequence

import sqlalchemy as sa
from alembic import op

# revision identifiers, used by Alembic.
revision: str = "4d3af0380f75"
down_revision: str | Sequence[str] | None = "dfdc692db230"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def upgrade() -> None:
    """Upgrade schema."""
    op.add_column(
        "character",
        sa.Column(
            "last_visited_scene", sa.String(length=64), server_default="SquareScene", nullable=False
        ),
    )


def downgrade() -> None:
    """Downgrade schema."""
    op.drop_column("character", "last_visited_scene")
