"""character last position

Revision ID: 8b7e2c41d9a3
Revises: 4d3af0380f75
Create Date: 2026-11-06 12:00:00.000000

"""

from collections.abc import Sequence

import sqlalchemy as sa
from alembic import op

# revision identifiers, used by Alembic.
revision: str = "8b7e2c41d9a3"
down_revision: str | Sequence[str] | None = "4d3af0380f75"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def upgrade() -> None:
    """Upgrade schema."""
    op.add_column("character", sa.Column("last_x", sa.Float(), server_default="0", nullable=False))
    op.add_column("character", sa.Column("last_y", sa.Float(), server_default="0", nullable=False))
    op.add_column("character", sa.Column("last_z", sa.Float(), server_default="0", nullable=False))


def downgrade() -> None:
    """Downgrade schema."""
    op.drop_column("character", "last_z")
    op.drop_column("character", "last_y")
    op.drop_column("character", "last_x")
