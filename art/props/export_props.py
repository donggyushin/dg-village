"""Copy picked PixelLab props into Unity, trimmed so the sprite's bottom edge = where it stands.

Run from the repo root:
    uv run --no-project --with pillow python art/props/export_props.py

- PixelLab objects have 5–10 empty rows under the object; the importer puts the pivot at
  bottom center, so those rows would make props float. Only the bottom is trimmed (width
  stays, so the object stays centered on its pivot).
- Also writes blob_shadow.png: a soft-edged pixel ellipse laid flat under props.
"""

from pathlib import Path

from PIL import Image

HERE = Path(__file__).parent
ROOT = HERE.parents[1]
SOURCE = HERE / "pixellab-2026-10-21"
OUT = ROOT / "unity/Assets/_Project/Sprites/Environment/Props"

PICKS = {
    "bench_03": "bench_plain",
    "bench_06": "bench_double",
    "bench_07": "bench_iron",
    "bench_14": "bench_arch",
    "tree_07": "tree_round",
    "tree_12": "tree_blossom",
    "tree_15": "tree_round_b",
    "tree_16": "tree_twin",
}

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


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    for src, name in PICKS.items():
        im = Image.open(SOURCE / f"{src}.png").convert("RGBA")
        bbox = im.getchannel("A").getbbox()
        if bbox is None:
            raise SystemExit(f"{src}.png is empty")
        im.crop((0, 0, im.width, bbox[3])).save(OUT / f"{name}.png")
    blob_shadow().save(OUT / "blob_shadow.png")
    print(f"{len(PICKS)} props + blob_shadow → {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
