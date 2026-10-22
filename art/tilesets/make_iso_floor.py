"""Quarter-view floor atlas for Unity's FloorMesh (camera X 30° · Y 45°, MapTest experiment 2026-10-22).

Run from the repo root:
    uv run --no-project --with pillow python art/tilesets/make_iso_floor.py

Why a separate atlas: in the quarter view the world tile grid shows up as 2:1 diamonds, so the
flat 32px tiles of make_floor_atlas.py get rotated 45° on screen and their texels smear. Here
each material is drawn as a pattern **in screen pixels** (640×360 internal resolution) and
FloorMesh maps it with screen-aligned UVs, so one texel = one screen pixel again and every line
is a clean 2:1 pixel stair.

Pattern space: for a screen pixel (x, y) (y up) the world-axis coordinates are
    i = (x >> 1) + y   (along world +X: screen (+2, +1))
    j = y - (x >> 1)   (along world +Z: screen (-2, +1))
1 world unit = 22.6 i/j steps. The cell is CELL_W × CELL_H screen px and repeats; i and j repeat
every 128, so every grid used below must divide 128.

Atlas rows = FloorMesh materials in "swcgp" order (stone, wood, carpet, grass, path), row 0 at
the top of the image. No edge trims: material borders are just the 2:1 polygon edges.
Also writes iso-floor-preview.png next to this script (rows at ×2 + grayscale).
"""

import random
from pathlib import Path

from PIL import Image

HERE = Path(__file__).parent
ROOT = HERE.parents[1]
OUT = ROOT / "unity/Assets/_Project/Sprites/Environment/Ground/office_floor_iso.png"
PALETTE = ROOT / "art/palettes/dg-village-map.hex"
CELL_W, CELL_H = 256, 128
PERIOD = 128

Rgb = tuple[int, int, int]


def palette_rows() -> list[list[Rgb]]:
    hexes = PALETTE.read_text().split()
    colors = [(int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)) for h in hexes]
    return [colors[i : i + 6] for i in range(0, len(colors), 6)]


def ij(x: int, y: int) -> tuple[int, int]:
    """Pattern coordinates of screen pixel (x, y up), wrapped to the period."""
    return ((x >> 1) + y) % PERIOD, (y - (x >> 1)) % PERIOD


def draw(color_at) -> Image.Image:
    """Render one cell. color_at(i, j) → Rgb; image rows are flipped so y goes up."""
    im = Image.new("RGBA", (CELL_W, CELL_H))
    for y in range(CELL_H):
        for x in range(CELL_W):
            im.putpixel((x, CELL_H - 1 - y), (*color_at(*ij(x, y)), 255))
    return im


def stone(ramp: list[Rgb]) -> Image.Image:
    """Flagstones 32×32 (≈1.4 units): dark grout, lit far edges, per-stone shade, speckles."""
    size = 32
    rng = random.Random(3)
    shades = {(a, b): rng.choice([2, 3, 3, 4]) for a in range(PERIOD // size) for b in range(PERIOD // size)}
    speckle = random.Random(11)
    noise = {(i, j): speckle.random() for i in range(PERIOD) for j in range(PERIOD)}

    def at(i: int, j: int) -> Rgb:
        fi, fj = i % size, j % size
        if fi == 0 or fj == 0:
            return ramp[1]  # grout
        fill = shades[(i // size, j // size)]
        if fi == size - 1 or fj == size - 1:
            return ramp[min(5, fill + 1)]  # far (upper) edges catch the light
        if fi == 1 or fj == 1:
            return ramp[max(0, fill - 1)]  # near edges in shade
        n = noise[(i, j)]
        if n < 0.05:
            return ramp[max(0, fill - 1)]
        if n > 0.97:
            return ramp[min(5, fill + 1)]
        return ramp[fill]

    return draw(at)


def wood(ramp: list[Rgb]) -> Image.Image:
    """Planks along world X, 8 wide (≈0.35 units), staggered butt joints, grain streaks."""
    width, length = 8, 64
    rng = random.Random(5)
    rows = PERIOD // width
    offsets = [rng.randrange(0, length, 8) for _ in range(rows)]
    shades = {(r, s): rng.choice([2, 3, 3, 3]) for r in range(rows) for s in range(PERIOD // length + 1)}
    grain = random.Random(9)
    streaks = {(r, k): grain.random() for r in range(rows) for k in range(width)}

    def at(i: int, j: int) -> Rgb:
        r, k = j // width, j % width
        if k == 0:
            return ramp[1]  # seam between planks
        along = (i - offsets[r]) % PERIOD
        if along % length == 0:
            return ramp[1]  # butt joint
        fill = shades[(r, along // length)]
        if k == width - 1:
            return ramp[min(5, fill + 1)]
        if streaks[(r, k)] < 0.12 and (i // 5 + r) % 6 != 0:
            return ramp[max(0, fill - 1)]  # long grain line, broken now and then
        return ramp[fill]

    return draw(at)


def carpet(ramp: list[Rgb]) -> Image.Image:
    """Woven diamonds: 16-step lattice, darker diamond outlines and a lighter dot in each."""
    size = 16

    def at(i: int, j: int) -> Rgb:
        fi, fj = i % size, j % size
        if fi == 0 or fj == 0:
            return ramp[2]
        if fi in (7, 8) and fj in (7, 8):
            return ramp[4]
        if i % 4 == 2 and j % 4 == 2 and (i // size + j // size) % 2:
            return ramp[2]  # dotted weave on every other diamond
        return ramp[3]

    return draw(at)


def grass(ramp: list[Rgb]) -> Image.Image:
    rng = random.Random(21)
    noise = {(i, j): rng.random() for i in range(PERIOD) for j in range(PERIOD)}

    def at(i: int, j: int) -> Rgb:
        n = noise[(i, j)]
        return ramp[2] if n < 0.12 else ramp[4] if n > 0.9 else ramp[3]

    return draw(at)


def path(ramp: list[Rgb]) -> Image.Image:
    """Small cobbles 16×16."""
    size = 16
    rng = random.Random(13)
    shades = {(a, b): rng.choice([3, 3, 4]) for a in range(PERIOD // size) for b in range(PERIOD // size)}

    def at(i: int, j: int) -> Rgb:
        fi, fj = i % size, j % size
        if fi == 0 or fj == 0:
            return ramp[2]
        fill = shades[(i // size, j // size)]
        return ramp[min(5, fill + 1)] if fi == size - 1 or fj == size - 1 else ramp[fill]

    return draw(at)


def main() -> None:
    rows = palette_rows()
    # same palette ramps as make_floor_atlas.py: 2 = stone, 4 = wood, 6 = carpet, 0 = grass
    cells = [stone(rows[2]), wood(rows[4]), carpet(rows[6]), grass(rows[0]), path(rows[2])]
    atlas = Image.new("RGBA", (CELL_W, CELL_H * len(cells)))
    for r, cell in enumerate(cells):
        atlas.paste(cell, (0, r * CELL_H))
    OUT.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(OUT)

    big = atlas.resize((atlas.width * 2, atlas.height * 2), Image.NEAREST)
    preview = Image.new("RGB", (big.width * 2 + 8, big.height), "#1b211e")
    preview.paste(big, (0, 0))
    preview.paste(big.convert("L").convert("RGB"), (big.width + 8, 0))
    preview.save(HERE / "iso-floor-preview.png")
    print(f"{OUT.relative_to(ROOT)} + {(HERE / 'iso-floor-preview.png').relative_to(ROOT)}")


if __name__ == "__main__":
    main()
