"""character

Revision ID: dfdc692db230
Revises: 5a1c0e7b2f10
Create Date: 2026-10-28 00:09:35.827333

"""

from collections.abc import Sequence

import sqlalchemy as sa
from alembic import op

# revision identifiers, used by Alembic.
revision: str = "dfdc692db230"
down_revision: str | Sequence[str] | None = "5a1c0e7b2f10"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def upgrade() -> None:
    """Upgrade schema."""
    op.create_table(
        "character",
        sa.Column("id", sa.Integer(), nullable=False),
        sa.Column("server_id", sa.Integer(), nullable=False),
        sa.Column("account_id", sa.Uuid(), nullable=False),
        sa.Column("nickname", sa.String(length=12), nullable=False),
        sa.Column("sprite_set_id", sa.String(length=64), nullable=False),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            server_default=sa.text("now()"),
            nullable=False,
        ),
        sa.ForeignKeyConstraint(
            ["server_id"],
            ["server.id"],
        ),
        sa.PrimaryKeyConstraint("id"),
        sa.UniqueConstraint("server_id", "nickname"),
    )
    op.create_index(op.f("ix_character_account_id"), "character", ["account_id"], unique=False)


def downgrade() -> None:
    """Downgrade schema."""
    op.drop_index(op.f("ix_character_account_id"), table_name="character")
    op.drop_table("character")
