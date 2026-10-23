"""Copy picked PixelLab buildings into Unity as one-sprite buildings (BuildingSprite shader).

Run from the repo root:
    uv run --no-project --with pillow python art/buildings/export_buildings.py

PixelLab draws buildings on a little isometric ground slab whose dirt sides would float on our
grass. For every pixel column the dirt side (DIRT colors) is erased from the bottom up until the
first other color, so the building sits on the slab's top surface (its grass / paving stays as a
yard); stray pixels under the yard's 2:1 lower edges are cleared too. Then empty bottom rows are trimmed: the importer's bottom-center pivot = the lowest point
of the yard diamond, which is what Obstacle.InitQuarterView expects (the footprint goes in
OfficeRoomBuilder.Footprints, in units: width along world X = the lower-right side, depth along Z
= the lower-left side; 1 unit = 22.6 screen px along a 2:1 edge).
"""

from pathlib import Path

from PIL import Image

HERE = Path(__file__).parent
ROOT = HERE.parents[1]
OUT = ROOT / "unity/Assets/_Project/Sprites/Environment/Buildings"

# source (relative to this folder) → Unity name
PICKS = {
    "pixellab-2026-10-23/guild_hall.png": "guild_hall",
    "pixellab-2026-10-23/library.png": "library",
    "pixellab-2026-10-23/shop.png": "shop",
}

# slab side: outline, dirt shades, the dark seam between the top and the side
DIRT = {0x040102, 0x442A29, 0x744D3D, 0x60463B, 0x2A1619, 0x22201F,
        # library / shop slabs use a few more shades
        0x623E35, 0x180A08, 0x7D4D34, 0x784840, 0x21141B, 0x3E2324, 0x1E0F0F, 0x55352F, 0x0E0806}


def strip_slab(im: Image.Image) -> Image.Image:
    im = im.copy()
    for x in range(im.width):
        for y in range(im.height - 1, -1, -1):
            r, g, b, a = im.getpixel((x, y))
            if a < 128:
                continue
            if (r << 16 | g << 8 | b) not in DIRT:
                break
            im.putpixel((x, y), (0, 0, 0, 0))
    return im


def cut_below_diamond(im: Image.Image) -> Image.Image:
    """Clear stray pixels hanging under the yard's two lower 2:1 edges (columns where the slab side
    had a non-dirt pixel). The lowest remaining pixel is the diamond's bottom vertex."""
    im = im.copy()
    bottom = im.getbbox()[3] - 1
    alpha = im.getchannel("A")
    xs = [x for x in range(im.width) if alpha.getpixel((x, bottom)) >= 128]
    vertex = sum(xs) / len(xs)
    for x in range(im.width):
        edge = bottom - abs(x - vertex) / 2
        for y in range(int(edge) + 1, im.height):
            im.putpixel((x, y), (0, 0, 0, 0))
    return im


def trim_bottom(im: Image.Image) -> Image.Image:
    bottom = im.getbbox()[3]
    return im.crop((0, 0, im.width, bottom))


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    for source, name in PICKS.items():
        im = Image.open(HERE / source).convert("RGBA")
        trim_bottom(cut_below_diamond(strip_slab(im))).save(OUT / f"{name}.png")
        print(f"{source} → {(OUT / name).relative_to(ROOT)}.png")


if __name__ == "__main__":
    main()
