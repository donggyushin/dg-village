"""PixelLab Wang tileset → recolored ground atlas for Unity.

Run from the repo root:
    uv run --no-project --with pillow python art/tilesets/make_ground_atlas.py

- PixelLab's tileset form has no palette option, so its grass comes out mustard yellow.
  Grass pixels (saturated, hue ~50°) are snapped to the map palette's grass ramp by value;
  stone pixels are kept.
- PixelLab packs the 16 Wang tiles in a 5×4 sheet in its own order. Each tile's corners are
  classified (stone or grass) and the tiles are repacked into a 4×4 atlas where
  cell index = NE·1 + SE·2 + SW·4 + NW·8 (bit set = stone), left→right, top→bottom.
  `GroundMesh` (Unity) uses the same index.
- Also writes preview.png next to this script: a sample layout at ×3 + grayscale copy.
"""

import colorsys
from pathlib import Path

from PIL import Image

HERE = Path(__file__).parent
ROOT = HERE.parents[1]
SOURCE = HERE / "plaza/pixellab-wang.png"
OUT = ROOT / "unity/Assets/_Project/Sprites/Environment/Ground/plaza_ground.png"
PALETTE = ROOT / "art/palettes/dg-village-map.hex"

TILE = 32
CORNER = 4  # px sampled at each corner to classify it

# grass ramp = palette rows 0..5. Base grass lands on BASE (large areas: index 3–4)
BASE = 4
# (min value of the source pixel, ramp offset from BASE); first match wins
VALUE_STEPS = [(0.77, +1), (0.70, 0), (0.58, -1), (0.48, -2), (0.0, -3)]


def is_grass(r: int, g: int, b: int) -> bool:
    h, s, _ = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    return s > 0.33 and 40 <= h * 360 <= 70


def recolor(im: Image.Image, grass_ramp: list[tuple[int, int, int]]) -> Image.Image:
    out = im.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a == 0 or not is_grass(r, g, b):
                continue
            v = max(r, g, b) / 255
            offset = next(o for lo, o in VALUE_STEPS if v >= lo)
            px[x, y] = (*grass_ramp[max(0, min(5, BASE + offset))], 255)
    return out


def corner_is_stone(tile: Image.Image, x0: int, y0: int) -> bool:
    pixels = [tile.getpixel((x, y)) for x in range(x0, x0 + CORNER) for y in range(y0, y0 + CORNER)]
    grass = sum(is_grass(*p[:3]) for p in pixels)
    return grass * 2 < len(pixels)


def wang_index(tile: Image.Image) -> int:
    far = TILE - CORNER
    ne = corner_is_stone(tile, far, 0)
    se = corner_is_stone(tile, far, far)
    sw = corner_is_stone(tile, 0, far)
    nw = corner_is_stone(tile, 0, 0)
    return ne * 1 + se * 2 + sw * 4 + nw * 8


def main() -> None:
    hexes = PALETTE.read_text().split()
    grass_ramp = [(int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)) for h in hexes[0:6]]

    src = Image.open(SOURCE).convert("RGBA")
    tiles: dict[int, Image.Image] = {}
    for row in range(src.height // TILE):
        for col in range(src.width // TILE):
            tile = src.crop((col * TILE, row * TILE, (col + 1) * TILE, (row + 1) * TILE))
            if tile.getchannel("A").getextrema()[1] == 0:
                continue  # empty slot
            index = wang_index(tile)
            if index in tiles:
                # PixelLab adds an extra full-terrain variant; unused until we vary tiles
                print(f"skip variant of Wang index {index} at sheet cell ({col}, {row})")
                continue
            tiles[index] = tile
    missing = sorted(set(range(16)) - set(tiles))
    if missing:
        raise SystemExit(f"missing Wang indices {missing}")

    atlas = Image.new("RGBA", (TILE * 4, TILE * 4))
    for index, tile in tiles.items():
        atlas.paste(recolor(tile, grass_ramp), ((index % 4) * TILE, (index // 4) * TILE))
    OUT.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(OUT)

    # preview: a stone path with a plaza, built from corner flags exactly like GroundMesh
    corners = [
        "..........",
        "..######..",
        "..######..",
        "..##..##..",
        "..######..",
        "####..####",
        "....##....",
    ]
    w, h = len(corners[0]) - 1, len(corners) - 1
    sample = Image.new("RGBA", (w * TILE, h * TILE))
    for ty in range(h):
        for tx in range(w):
            ne = corners[ty][tx + 1] == "#"
            se = corners[ty + 1][tx + 1] == "#"
            sw = corners[ty + 1][tx] == "#"
            nw = corners[ty][tx] == "#"
            i = ne * 1 + se * 2 + sw * 4 + nw * 8
            cell = atlas.crop(((i % 4) * TILE, (i // 4) * TILE, (i % 4 + 1) * TILE, (i // 4 + 1) * TILE))
            sample.paste(cell, (tx * TILE, ty * TILE))
    sample = sample.resize((sample.width * 3, sample.height * 3), Image.NEAREST)
    preview = Image.new("RGB", (sample.width, sample.height * 2 + 8), "#1b211e")
    preview.paste(sample, (0, 0), sample)
    preview.paste(sample.convert("L").convert("RGB"), (0, sample.height + 8))
    preview.save(HERE / "preview.png")
    print(f"{OUT.relative_to(ROOT)} + {(HERE / 'preview.png').relative_to(ROOT)}")


if __name__ == "__main__":
    main()
