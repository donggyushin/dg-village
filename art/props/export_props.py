"""Copy picked PixelLab props into Unity, trimmed so the sprite's bottom edge = where it stands.

Run from the repo root:
    uv run --no-project --with pillow python art/props/export_props.py

- PixelLab objects have 5–10 empty rows under the object; the importer puts the pivot at
  bottom center, so those rows would make props float. Only the bottom is trimmed (width
  stays, so the object stays centered on its pivot).
- Also writes blob_shadow.png: a soft-edged pixel ellipse laid flat under props.
- Rotated furniture (PixelLab "Expand to 8" on the front-view object, 2026-10-22):
  pixellab-2026-10-22-rotations/<name>/<direction>.png → <name>_<direction>.png for the
  east-side + back directions (west-side = flipped in the map JSON, `"flip": true`).
  The front view keeps using the original pick above (same art; the 8-rotation canvas is 68px).
  Expand ignores the palette the front view was made with (rotations come out redder), so every
  rotation is snapped to the colors of its own front view → it reads as the same piece.
  Expand sometimes turns a direction the wrong way (bookshelf_potion's south-east faces
  down-left); those images are listed in MIRRORED and flipped back here.
"""

from pathlib import Path

from PIL import Image, ImageOps

HERE = Path(__file__).parent
ROOT = HERE.parents[1]
OUT = ROOT / "unity/Assets/_Project/Sprites/Environment/Props"

# source (relative to this folder, no .png) → Unity name
PICKS = {
    "pixellab-2026-10-21/bench_03": "bench_plain",
    "pixellab-2026-10-21/bench_06": "bench_double",
    "pixellab-2026-10-21/bench_07": "bench_iron",
    "pixellab-2026-10-21/bench_14": "bench_arch",
    "pixellab-2026-10-21/tree_07": "tree_round",
    "pixellab-2026-10-21/tree_12": "tree_blossom",
    "pixellab-2026-10-21/tree_15": "tree_round_b",
    "pixellab-2026-10-21/tree_16": "tree_twin",
    # wizard guild office (first indoor map); seats + desks are front-view (-front) so a
    # character facing the camera reads as sitting on them
    "pixellab-2026-10-21-office-front/wooden_writing_desk": "desk_quill",
    "pixellab-2026-10-21-office-front/dark_wooden_desk_wit": "desk_spellbook",
    "pixellab-2026-10-21-office-front/oak_desk_with_green": "desk_crystal",
    "pixellab-2026-10-21-office-front/simple_wooden_chair": "chair_cushion",
    "pixellab-2026-10-21-office-front/high_backed_carved_w": "chair_high",
    "pixellab-2026-10-21-office/tall_wooden_bookshelf_full_of": "bookshelf",
    "pixellab-2026-10-21-office/bookshelf_with_potion_bottles": "bookshelf_potion",
    "pixellab-2026-10-21-office/stone_fireplace_with_warm_glow": "fireplace",
    "pixellab-2026-10-21-office-front/cozy_burgundy_velvet": "sofa",
    "pixellab-2026-10-21-office-front/plump_brown_leather": "armchair",
    "pixellab-2026-10-21-office-front/small_round_wooden_t": "tea_table",
    "pixellab-2026-10-21-office/large_potted_leafy_plant_in_cl": "plant_pot",
    "pixellab-2026-10-21-office/tall_iron_standing_candelabra": "candelabra",
    "pixellab-2026-10-21-office/wooden_owl_perch_stand_with_a": "owl_perch",
    "pixellab-2026-10-21-office/brass_globe_on_wooden_stand": "globe",
    "pixellab-2026-10-21-office/wooden_notice_board_with_pinne": "notice_board",
    # outdoor street lamps (Create Object Pro, 128px, no style ref/palette; 2026-10-23). Flame ≈ 2.7 units up
    "pixellab-2026-10-23-lamps/street_lamp_iron": "street_lamp",
    "pixellab-2026-10-23-lamps/street_lamp_vine": "street_lamp_vine",
}

ROTATIONS_DIR = "pixellab-2026-10-22-rotations"
# PixelLab direction → our suffix (the way the furniture faces = the way someone sitting on it looks)
ROTATION_SUFFIXES = {"south-east": "southeast", "east": "east", "north-east": "northeast", "north": "north"}
# (name, PixelLab direction) images that PixelLab drew facing the mirrored way
MIRRORED = {("bookshelf_potion", "south-east")}

SHADOW_SIZE = (32, 16)  # px → 1 × 0.5 units at 32 PPU
SHADOW_COLOR = (0x07, 0x04, 0x0C)  # palette outline color
SHADOW_ALPHA = (110, 70)  # inner ellipse, outer ring


def blob_shadow() -> Image.Image:
    w, h = SHADOW_SIZE
    im = Image.new("RGBA", (w, h))
    for y in range(h):
        for x in range(w):
            # normalized distance from the center of the ellipse (pixel centers)
            d = ((x + 0.5 - w / 2) / (w / 2)) ** 2 + ((y + 0.5 - h / 2) / (h / 2)) ** 2
            if d <= 0.55:
                im.putpixel((x, y), (*SHADOW_COLOR, SHADOW_ALPHA[0]))
            elif d <= 1.0:
                im.putpixel((x, y), (*SHADOW_COLOR, SHADOW_ALPHA[1]))
    return im


def snap_to_colors(im: Image.Image, reference: Image.Image) -> Image.Image:
    """Each opaque pixel → the nearest (weighted RGB) color used in reference."""
    colors = sorted({c for _, c in reference.getcolors(1 << 16) if c[3] > 0})
    cache: dict[tuple, tuple] = {}

    def nearest(c: tuple) -> tuple:
        if c not in cache:
            cache[c] = min(
                colors,
                key=lambda p: 2 * (p[0] - c[0]) ** 2 + 4 * (p[1] - c[1]) ** 2 + 3 * (p[2] - c[2]) ** 2,
            )
        return cache[c]

    out = im.copy()
    pixels = out.load()
    for y in range(out.height):
        for x in range(out.width):
            c = pixels[x, y]
            if c[3] > 0:
                pixels[x, y] = (*nearest(c)[:3], 255)
    return out


def export(src: Path, name: str, reference: Path | None = None, mirror: bool = False) -> None:
    im = Image.open(src).convert("RGBA")
    if mirror:
        im = ImageOps.mirror(im)
    if reference is not None:
        im = snap_to_colors(im, Image.open(reference).convert("RGBA"))
    bbox = im.getchannel("A").getbbox()
    if bbox is None:
        raise SystemExit(f"{src} is empty")
    im.crop((0, 0, im.width, bbox[3])).save(OUT / f"{name}.png")


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    for src, name in PICKS.items():
        export(HERE / f"{src}.png", name)
    rotated = 0
    for folder in sorted((HERE / ROTATIONS_DIR).iterdir()):
        for direction, suffix in ROTATION_SUFFIXES.items():
            path = folder / f"{direction}.png"
            if path.exists():
                mirror = (folder.name, direction) in MIRRORED
                export(path, f"{folder.name}_{suffix}", reference=folder / "south.png", mirror=mirror)
                rotated += 1
    blob_shadow().save(OUT / "blob_shadow.png")
    print(f"{len(PICKS)} props + {rotated} rotated + blob_shadow → {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
