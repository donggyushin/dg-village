"""PixelLab character ZIP (Export → ZIP (all frames)) → Unity sprites.

    uv run --no-project --with pillow python art/characters/export_character.py <unzipped dir> <name>

Writes unity/Assets/_Project/Sprites/Characters/<name>/:
  <name>_idle_<dir>.png, <name>_walk_<dir>_<n>.png  for dir in south, north, east, southeast, northeast
  <name>_sit_<dir>.png                              for dir in south, east, north (if the ZIP has a sitting state)
West-side directions are the east-side ones flipped in Unity, so they are not exported.

All outputs are 64×64 with the feet ~10px above the bottom (importer pivot = 10/64):
- idle rotations are 48×48 → padded centered;
- walk frames are a bigger centered canvas (64 for girl01, 80 for boy01); 80 → crop (8, 10, 72, 74),
  the same box the idle gets after padding to 80, so idle and walk line up;
- sitting rotations (48×48) → bbox bottom placed at y = 54, centered.
After exporting: right-click the folder in Unity → DgVillage → Create Character Definition.
"""

import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "unity/Assets/_Project/Sprites/Characters"
SIZE = 64
FEET_Y = SIZE - 10
CROP_80 = (8, 10, 72, 74)

# PixelLab folder name → our file name
DIRECTIONS = {
    "south": "south",
    "north": "north",
    "east": "east",
    "south-east": "southeast",
    "north-east": "northeast",
}
SIT_DIRECTIONS = ("south", "east", "north")


def to_64(image: Image.Image) -> Image.Image:
    """Walk frame or idle (already on the walk canvas) → 64×64."""
    if image.size == (SIZE, SIZE):
        return image
    if image.size == (80, 80):
        return image.crop(CROP_80)
    raise ValueError(f"unexpected frame size {image.size}")


def pad(image: Image.Image, size: int) -> Image.Image:
    canvas = Image.new("RGBA", (size, size))
    offset = (size - image.width) // 2
    canvas.paste(image, (offset, offset))
    return canvas


def place_sit(image: Image.Image) -> Image.Image:
    canvas = Image.new("RGBA", (SIZE, SIZE))
    bottom = image.getbbox()[3]
    canvas.paste(image, ((SIZE - image.width) // 2, FEET_Y - bottom))
    return canvas


def main(source: Path, name: str) -> None:
    out = OUT / name
    out.mkdir(parents=True, exist_ok=True)
    idle_dir = source / "Idle"
    walk_root = idle_dir / "animations" / "Walk"
    # walk canvas size decides how idle is padded (so both line up after cropping)
    first_frame = next(walk_root.glob("*/frame_000.png"))
    canvas = Image.open(first_frame).size[0]

    for pixellab, ours in DIRECTIONS.items():
        idle = idle_dir / "rotations" / f"{pixellab}.png"
        frames = sorted((walk_root / pixellab).glob("frame_*.png"))
        if not idle.exists() or not frames:
            print(f"skip {ours}: idle={idle.exists()} walk={len(frames)}")
            continue
        to_64(pad(Image.open(idle).convert("RGBA"), canvas)).save(out / f"{name}_idle_{ours}.png")
        for i, frame in enumerate(frames):
            to_64(Image.open(frame).convert("RGBA")).save(out / f"{name}_walk_{ours}_{i}.png")
        print(f"{ours}: idle + {len(frames)} walk frames")

    sit_dirs = [d for d in source.iterdir() if d.name.startswith("sitting")]
    for sit in sit_dirs[:1]:
        for direction in SIT_DIRECTIONS:
            path = sit / "rotations" / f"{direction}.png"
            if path.exists():
                place_sit(Image.open(path).convert("RGBA")).save(out / f"{name}_sit_{direction}.png")
                print(f"sit {direction}")


if __name__ == "__main__":
    main(Path(sys.argv[1]), sys.argv[2])
