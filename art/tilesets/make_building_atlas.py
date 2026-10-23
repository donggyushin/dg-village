"""Building atlas for Unity's BuildingMesh (outdoor buildings: stone walls + clay-tile gable roof).

Run from the repo root:
    uv run --no-project --with pillow python art/tilesets/make_building_atlas.py

256×128, eight 32px columns (1 unit wide each); wall columns are 128px = 4 units tall like the
office walls (same texel density, walls lean back like sprites so texels stay 1:1 on screen):
    0     wall face: stone blocks, 4px ledge on top, 16px darker stone plinth at the bottom
    1–2   wall face with an arched window (draw_window from make_floor_atlas.py, left / right half)
    3–4   wall face with an arched double door (left / right half)
    5     roof: rows 0–31 = clay tiles (repeat), 32–63 = eave row (fascia at the bottom),
          64–95 = ridge row (ridge cap at the top)
    6     gable: stone blocks only (no ledge / plinth), bottom-up from the top of the wall
Drawn in code like the office walls: seamless repeats are trivial here and the colors come
straight from the map palette (stone ramp for walls, brick ramp for the roof, wood for doors).
Also writes building-preview.png next to this script (atlas ×3 + grayscale).
"""

import random
from pathlib import Path

from make_floor_atlas import TILE, WALL_HEIGHT, draw_window, palette_rows
from PIL import Image

HERE = Path(__file__).parent
ROOT = HERE.parents[1]
OUT = ROOT / "unity/Assets/_Project/Sprites/Environment/Buildings/building_stone.png"

Rgb = tuple[int, int, int]
PLINTH = 16
LEDGE = 4


def stone_blocks(im: Image.Image, left: int, top: int, bottom: int, stone: list[Rgb], seed: int) -> None:
    """16×12 blocks, every other row offset by 8, wrapping inside the 32px column (seamless sideways)."""
    rng = random.Random(seed)
    block_w, block_h = 16, 12
    for row, y0 in enumerate(range(top, bottom, block_h)):
        offset = (row % 2) * (block_w // 2)
        for b in range(TILE // block_w):
            x0 = b * block_w + offset
            fill = rng.choice([1, 2, 2, 3])
            for y in range(y0, min(y0 + block_h, bottom)):
                for x in range(x0, x0 + block_w):
                    if y == y0 or x == x0:
                        c = stone[0]  # mortar
                    elif y == y0 + 1 or x == x0 + 1:
                        c = stone[min(5, fill + 1)]  # lit top/left edge
                    elif y == y0 + block_h - 1:
                        c = stone[max(0, fill - 1)]  # shaded bottom
                    elif rng.random() < 0.06:
                        c = stone[max(0, fill - 1)]  # speckles
                    else:
                        c = stone[fill]
                    im.putpixel((left + x % TILE, y), (*c, 255))


def wall_face(im: Image.Image, left: int, stone: list[Rgb]) -> None:
    stone_blocks(im, left, LEDGE, WALL_HEIGHT - PLINTH, stone, seed=7)
    for x in range(TILE):
        for y in range(LEDGE):
            im.putpixel((left + x, y), (*stone[[4, 3, 3, 1][y]], 255))
        # plinth: one row of long dark blocks with a lit top edge
        for i, y in enumerate(range(WALL_HEIGHT - PLINTH, WALL_HEIGHT)):
            c = stone[3] if i == 0 else stone[0] if i in (1, PLINTH - 1) or x == 0 else stone[1]
            im.putpixel((left + x, y), (*c, 255))


def draw_door(im: Image.Image, left: int, stone: list[Rgb], wood: list[Rgb]) -> None:
    """Arched double door over a 2-tile wall face: planks, iron bands, ring handles, stone arch, step."""
    x0, x1, radius = 12, 51, 20  # opening, door-local px (inclusive)
    bottom = WALL_HEIGHT - 4  # the step is the last 4 rows
    arch_y = bottom - 62
    cx = (x0 + x1) / 2
    iron = stone[0]

    def opening(x: int, y: int) -> bool:
        if not (x0 <= x <= x1 and arch_y - radius <= y <= bottom):
            return False
        return y >= arch_y or (x - cx) ** 2 + (y - arch_y) ** 2 <= radius**2

    def ring(x: int, y: int) -> int:
        for d in (1, 2, 3, 4):
            if any(opening(x + dx, y + dy) for dx in range(-d, d + 1) for dy in range(-d, d + 1)):
                return d
        return 0

    for y in range(WALL_HEIGHT):
        for x in range(TILE * 2):
            if opening(x, y):
                plank = (x - x0) % 5
                c = wood[1] if plank == 0 else wood[3] if plank == 1 else wood[2]
                if x in (31, 32):
                    c = wood[0]  # gap between the two leaves
                if y in (arch_y + 6, arch_y + 7, bottom - 14, bottom - 13):
                    c = iron if y % 2 == 0 else stone[1]  # iron bands
                im.putpixel((left + x, y), (*c, 255))
            elif r := ring(x, y):
                # arch stones: alternating light / dark voussoirs, dark outline outside
                c = stone[0] if r == 4 else stone[4] if (x // 4 + y // 4) % 2 else stone[3]
                im.putpixel((left + x, y), (*c, 255))
    for hx in (27, 36):  # ring handles
        for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1)):
            im.putpixel((left + hx + dx, arch_y + 30 + dy), (*stone[4], 255))
    for x in range(x0 - 6, x1 + 7):  # step
        for i, y in enumerate(range(bottom + 1, WALL_HEIGHT)):
            im.putpixel((left + x, y), (*stone[[5, 4, 3][min(i, 2)]], 255))


def roof_tiles(im: Image.Image, left: int, top: int, brick: list[Rgb], wood: list[Rgb], kind: str) -> None:
    """32×32 clay tiles: rows of 8px shingles, staggered by 4, rounded dark lower lip, lit upper edge.

    Image row 0 = up the slope (towards the ridge); the roof quad maps it that way.
    """
    rng = random.Random(31)
    shades = {(r, s): rng.choice([2, 3, 3, 4]) for r in range(4) for s in range(4)}
    for y in range(TILE):
        r, fy = y // 8, y % 8
        for x in range(TILE):
            s = ((x + (r % 2) * 4) // 8) % 4
            fx = (x + (r % 2) * 4) % 8
            fill = shades[(r, s)]
            if fy == 7 or (fy == 6 and fx in (0, 1, 7)):
                c = brick[0]  # lip shadow, rounded corners
            elif fx == 0 and fy >= 3:
                c = brick[max(0, fill - 2)]  # gap between shingles (the row above covers its top)
            elif fy == 0:
                c = brick[min(5, fill + 1)]
            else:
                c = brick[fill]
            im.putpixel((left + x, top + y), (*c, 255))
    if kind == "eave":  # wooden fascia board under the last row
        for y in range(TILE - 5, TILE):
            for x in range(TILE):
                im.putpixel((left + x, top + y), (*wood[[3, 2, 2, 1, 0][y - (TILE - 5)]], 255))
    if kind == "ridge":  # half-round ridge cap along the top
        for y in range(6):
            for x in range(TILE):
                c = brick[[0, 4, 3, 3, 2, 0][y]] if x % 16 else brick[0]
                im.putpixel((left + x, top + y), (*c, 255))


def main() -> None:
    rows = palette_rows()
    stone, wood, brick = rows[2], rows[4], rows[6]
    im = Image.new("RGBA", (TILE * 8, WALL_HEIGHT))
    for col in range(5):
        wall_face(im, TILE * col, stone)
    draw_window(im, TILE * 1, rows)
    draw_door(im, TILE * 3, stone, wood)
    for i, kind in enumerate(("tile", "eave", "ridge")):
        roof_tiles(im, TILE * 5, TILE * i, brick, wood, kind)
    stone_blocks(im, TILE * 6, 0, WALL_HEIGHT, stone, seed=11)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    im.save(OUT)

    big = im.resize((im.width * 3, im.height * 3), Image.NEAREST)
    preview = Image.new("RGB", (big.width, big.height * 2 + 8), "#1b211e")
    preview.paste(big, (0, 0), big)
    preview.paste(big.convert("L").convert("RGB"), (0, big.height + 8), big)
    preview.save(HERE / "building-preview.png")
    print(f"{OUT.relative_to(ROOT)} + {(HERE / 'building-preview.png').relative_to(ROOT)}")


if __name__ == "__main__":
    main()
