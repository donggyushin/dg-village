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
    # wizard guild office (first indoor map)
    "pixellab-2026-10-21-office/wooden_writing_desk_with_parch": "desk_quill",
    "pixellab-2026-10-21-office/wizard_study_desk_with_open_sp": "desk_spellbook",
    "pixellab-2026-10-21-office/oak_work_desk_with_stacked_boo": "desk_crystal",
    "pixellab-2026-10-21-office/wooden_chair_with_red_cushion": "chair_cushion",
    "pixellab-2026-10-21-office/high-backed_carved_wooden_chai": "chair_high",
    "pixellab-2026-10-21-office/tall_wooden_bookshelf_full_of": "bookshelf",
    "pixellab-2026-10-21-office/bookshelf_with_potion_bottles": "bookshelf_potion",
    "pixellab-2026-10-21-office/stone_fireplace_with_warm_glow": "fireplace",
    "pixellab-2026-10-21-office/cozy_burgundy_velvet_sofa": "sofa",
    "pixellab-2026-10-21-office/plump_leather_armchair": "armchair",
    "pixellab-2026-10-21-office/small_round_tea_table_with_tea": "tea_table",
    "pixellab-2026-10-21-office/large_potted_leafy_plant_in_cl": "plant_pot",
    "pixellab-2026-10-21-office/tall_iron_standing_candelabra": "candelabra",
    "pixellab-2026-10-21-office/wooden_owl_perch_stand_with_a": "owl_perch",
    "pixellab-2026-10-21-office/brass_globe_on_wooden_stand": "globe",
    "pixellab-2026-10-21-office/wooden_notice_board_with_pinne": "notice_board",
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
        im = Image.open(HERE / f"{src}.png").convert("RGBA")
        bbox = im.getchannel("A").getbbox()
        if bbox is None:
            raise SystemExit(f"{src}.png is empty")
        im.crop((0, 0, im.width, bbox[3])).save(OUT / f"{name}.png")
    blob_shadow().save(OUT / "blob_shadow.png")
    print(f"{len(PICKS)} props + blob_shadow → {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
