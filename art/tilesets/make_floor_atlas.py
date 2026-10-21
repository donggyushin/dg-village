"""Indoor floor atlas (stone / wood / carpet) for Unity's FloorMesh.

Run from the repo root:
    uv run --no-project --with pillow python art/tilesets/make_floor_atlas.py

Why not PixelLab's Wang transitions: indoors every floor edge is straight and lies on a tile
edge, and PixelLab bleeds the two terrains' colors into each other (rusty stone, brick-like
planks). So only one clean full tile per material is taken from the PixelLab sheets, snapped
to the map palette by brightness, and the edges are drawn here:
- atlas row = material (MATERIALS order), column = edge mask
  N·1 + E·2 + S·4 + W·8 (bit set = the neighbor on that side is another material or empty);
- each set side gets a 2px trim from the material's own dark steps (carpet also a light stripe).
Also writes floor-preview.png next to this script (sample room ×3 + grayscale with girl01/boy01).
"""

from pathlib import Path

from PIL import Image

HERE = Path(__file__).parent
ROOT = HERE.parents[1]
OUT = ROOT / "unity/Assets/_Project/Sprites/Environment/Ground/office_floor.png"
PALETTE = ROOT / "art/palettes/dg-village-map.hex"
CHARACTERS = ROOT / "unity/Assets/_Project/Sprites/Characters"

TILE = 32
# PixelLab's Wang sheet always has the full-lower tile at cell (1, 1) and full-upper at (1, 3)
FULL_LOWER, FULL_UPPER = (1, 1), (1, 3)

# name: (source sheet, cell, palette ramp row, ramp steps dark→light, brightness cut points)
MATERIALS = {
    "stone": ("office/pixellab-stone-wood-v2.png", FULL_LOWER, 2, [1, 2, 3, 4], [0.15, 0.5, 0.85]),
    "wood": ("office/pixellab-stone-wood-v1.png", FULL_UPPER, 4, [2, 3, 4, 5], [0.2, 0.55, 0.92]),
    "carpet": ("office/pixellab-wood-carpet.png", FULL_UPPER, 6, [1, 2, 3, 4], [0.15, 0.55, 0.9]),
}
# FloorMesh layout characters, same order as MATERIALS
CHARS = "swc"

Rgb = tuple[int, int, int]


def palette_rows() -> list[list[Rgb]]:
    hexes = PALETTE.read_text().split()
    colors = [(int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)) for h in hexes]
    return [colors[i : i + 6] for i in range(0, len(colors), 6)]


def luminance(c: tuple[int, ...]) -> float:
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def recolor(tile: Image.Image, ramp: list[Rgb], steps: list[int], cuts: list[float]) -> Image.Image:
    """Snap every pixel to a ramp step by its brightness rank inside the tile."""
    pixels = [tile.getpixel((x, y)) for y in range(TILE) for x in range(TILE)]
    ranked = sorted(luminance(p) for p in pixels)
    thresholds = [ranked[min(len(ranked) - 1, int(c * len(ranked)))] for c in cuts]
    out = Image.new("RGBA", (TILE, TILE))
    for i, p in enumerate(pixels):
        level = sum(luminance(p) >= t for t in thresholds)
        out.putpixel((i % TILE, i // TILE), (*ramp[steps[level]], 255))
    return out


def with_trim(base: Image.Image, mask: int, ramp: list[Rgb], carpet: bool) -> Image.Image:
    tile = base.copy()
    # (distance from the edge, color) painted along every flagged side
    bands = [(0, ramp[0]), (1, ramp[1])]
    if carpet:
        bands.append((3, ramp[4]))
    for bit, side in ((1, "N"), (2, "E"), (4, "S"), (8, "W")):
        if not mask & bit:
            continue
        for d, color in bands:
            for i in range(TILE):
                x, y = {"N": (i, d), "S": (i, TILE - 1 - d), "W": (d, i), "E": (TILE - 1 - d, i)}[side]
                tile.putpixel((x, y), (*color, 255))
    return tile


def main() -> None:
    rows = palette_rows()
    atlas = Image.new("RGBA", (TILE * 16, TILE * len(MATERIALS)))
    for m, (name, (sheet, (col, row), ramp_row, steps, cuts)) in enumerate(MATERIALS.items()):
        src = Image.open(HERE / sheet).convert("RGBA")
        full = src.crop((col * TILE, row * TILE, (col + 1) * TILE, (row + 1) * TILE))
        base = recolor(full, rows[ramp_row], steps, cuts)
        for mask in range(16):
            atlas.paste(with_trim(base, mask, rows[ramp_row], name == "carpet"), (mask * TILE, m * TILE))
    OUT.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(OUT)

    # preview: desks (wood) | hallway (stone) | lounge (carpet), computed like FloorMesh
    layout = [
        "wwwwwsssssscccccc",
        "wwwwwsssssscccccc",
        "wwwwwsssssscccccc",
        "wwwwwsssssscccccc",
        "ssssssssssssccccc",
        "wwwwwsssssscccccc",
        "wwwwwssssss......",
    ]
    h, w = len(layout), len(layout[0])
    sample = Image.new("RGBA", (w * TILE, h * TILE))
    for ty in range(h):
        for tx in range(w):
            ch = layout[ty][tx]
            if ch not in CHARS:
                continue

            def differs(x: int, y: int) -> bool:
                return not (0 <= x < w and 0 <= y < h) or layout[y][x] != ch

            mask = differs(tx, ty - 1) * 1 + differs(tx + 1, ty) * 2 + differs(tx, ty + 1) * 4 + differs(tx - 1, ty) * 8
            m = CHARS.index(ch)
            sample.paste(atlas.crop((mask * TILE, m * TILE, (mask + 1) * TILE, (m + 1) * TILE)), (tx * TILE, ty * TILE))
    for i, name in enumerate(("girl01", "boy01")):
        sprite = Image.open(CHARACTERS / name / f"{name}_idle_south.png").convert("RGBA")
        for j, x in enumerate((2, 7, 13)):
            sample.alpha_composite(sprite, (x * TILE + i * 40 - 16, (1 + j % 2) * TILE))
    sample = sample.resize((sample.width * 3, sample.height * 3), Image.NEAREST)
    preview = Image.new("RGB", (sample.width, sample.height * 2 + 8), "#1b211e")
    preview.paste(sample, (0, 0), sample)
    preview.paste(sample.convert("L").convert("RGB"), (0, sample.height + 8), sample)
    preview.save(HERE / "floor-preview.png")
    print(f"{OUT.relative_to(ROOT)} + {(HERE / 'floor-preview.png').relative_to(ROOT)}")


if __name__ == "__main__":
    main()
