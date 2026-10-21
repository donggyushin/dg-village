"""Generate the map palette (dg-village-map.gpl / .hex / preview.png).

Run from the repo root:
    uv run --no-project --with pillow python art/palettes/make_map_palette.py

Rules (docs/research/2026-10-21-map-art/report.md):
- Environment ramps are a step calmer than the characters (character saturation:
  median 0.53, p90 0.78 → ramps peak at ~0.5) so characters read on top.
- Hue-shifted ramps: shadows lean cool (toward blue), highlights warm (toward yellow),
  at most ~20° each end; saturation peaks mid-ramp and drops in the highlights.
- Anchors shared with the characters: outline black, skin, near-white.
"""

import colorsys
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).parent
CHARACTERS = HERE.parents[1] / "unity/Assets/_Project/Sprites/Characters"

COOL, WARM = 240.0, 60.0  # hue targets for shadows / highlights
STEPS = 6

# name: (base hue, peak saturation, darkest value, lightest value, max hue shift)
RAMPS = {
    "grass": (95, 0.50, 0.22, 0.84, 20),
    "foliage": (130, 0.50, 0.16, 0.72, 20),
    "stone": (35, 0.12, 0.26, 0.90, 25),
    "dirt": (30, 0.45, 0.20, 0.84, 15),
    "wood": (22, 0.55, 0.18, 0.76, 15),
    "water": (200, 0.50, 0.26, 0.92, 20),
    "brick": (8, 0.50, 0.20, 0.80, 15),
}

# taken from girl01/boy01 so props and characters share the same extremes
ANCHORS = {
    "outline": "#07040c",
    "skin": "#f9d6ba",
    "white": "#f6eded",
    "flower pink": "#ef7c95",
    "flower pink light": "#f5a4b4",
    "flower yellow": "#f2c94c",
}


def shift_hue(hue: float, target: float, amount: float) -> float:
    """Move hue toward target along the shorter way round the wheel, by at most amount."""
    diff = (target - hue + 180) % 360 - 180
    step = max(-amount, min(amount, diff))
    return (hue + step) % 360


def ramp(hue: float, peak: float, v_lo: float, v_hi: float, max_shift: float) -> list[str]:
    colors = []
    for i in range(STEPS):
        t = i / (STEPS - 1)  # 0 = darkest, 1 = lightest
        if t < 0.5:
            h = shift_hue(hue, COOL, max_shift * (0.5 - t) * 2)
        else:
            h = shift_hue(hue, WARM, max_shift * (t - 0.5) * 2)
        # saturation peaks a bit below the middle, highlights get the least
        s = peak * max(0.35, 1 - 2.2 * (t - 0.4) ** 2)
        v = v_lo + (v_hi - v_lo) * t
        r, g, b = colorsys.hsv_to_rgb(h / 360, s, v)
        colors.append(f"#{round(r * 255):02x}{round(g * 255):02x}{round(b * 255):02x}")
    return colors


def rgb(hex_color: str) -> tuple[int, int, int]:
    return int(hex_color[1:3], 16), int(hex_color[3:5], 16), int(hex_color[5:7], 16)


def main() -> None:
    rows = {name: ramp(*params) for name, params in RAMPS.items()}
    rows["anchors"] = list(ANCHORS.values())
    palette = [c for row in rows.values() for c in row]

    (HERE / "dg-village-map.hex").write_text("\n".join(c[1:] for c in palette) + "\n")
    gpl = ["GIMP Palette", "Name: dg-village map", "Columns: 6", "#"]
    for name, row in rows.items():
        labels = list(ANCHORS) if name == "anchors" else [f"{name} {i}" for i in range(len(row))]
        gpl += [f"{r:3d} {g:3d} {b:3d}\t{label}" for c, label in zip(row, labels)
                for r, g, b in [rgb(c)]]
    (HERE / "dg-village-map.gpl").write_text("\n".join(gpl) + "\n")

    # preview: swatches, then characters standing on each ground ramp's mid tone,
    # then the same sheet in grayscale (value-contrast check)
    sw, scale = 40, 3
    sprites = [Image.open(CHARACTERS / n / f"{n}_idle_south.png").convert("RGBA") for n in ("girl01", "boy01")]
    sprite_size = sprites[0].width * scale
    grounds = [rows["grass"][3], rows["stone"][3], rows["dirt"][3], rows["grass"][2]]
    width = max(sw * STEPS + 120, len(grounds) * (sprite_size * 2 + 16))
    height = sw * len(rows) + sprite_size + 16
    sheet = Image.new("RGB", (width, height), "#1b211e")
    draw = ImageDraw.Draw(sheet)
    for y, (name, row) in enumerate(rows.items()):
        draw.text((4, y * sw + 14), name, fill="#e3e9e5")
        for x, c in enumerate(row):
            draw.rectangle([120 + x * sw, y * sw, 120 + (x + 1) * sw - 2, (y + 1) * sw - 2], fill=c)
    top = sw * len(rows) + 8
    for i, ground in enumerate(grounds):
        x0 = i * (sprite_size * 2 + 16)
        draw.rectangle([x0, top, x0 + sprite_size * 2 + 8, top + sprite_size], fill=ground)
        for j, sprite in enumerate(sprites):
            big = sprite.resize((sprite_size, sprite_size), Image.NEAREST)
            sheet.paste(big, (x0 + j * sprite_size, top), big)
    gray = sheet.convert("L").convert("RGB")
    preview = Image.new("RGB", (width, height * 2 + 8), "#1b211e")
    preview.paste(sheet, (0, 0))
    preview.paste(gray, (0, height + 8))
    preview.save(HERE / "preview.png")
    print(f"{len(palette)} colors → {HERE}")


if __name__ == "__main__":
    main()
