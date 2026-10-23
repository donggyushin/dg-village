"""PixelLab quadruped (pet) → Unity sprites.

    uv run --no-project --with pillow python art/characters/export_pet.py <source dir> <name>

<source dir> holds PixelLab's files as published on backblaze:
  rotations/<dir>.png              48×48 idle rotations
  walk/<dir>/<n>.png               Walk (6 frames) template animation, one folder per direction
(dir = south, south-east, east, north-east, north; PixelLab names). West side = flipped east side
in Unity, so west directions are not exported.

Writes unity/Assets/_Project/Sprites/Characters/Pets/<name>/ with the character file names
(<name>_idle_<dir>.png, <name>_walk_<dir>_<n>.png), all 64×64 with the feet 10px above the bottom
like the player characters (importer pivot = 10/64). Idle and walk frames share one canvas size,
so each is centered on 64×64 and moved so the idle's feet (bbox bottom) land at y = 54; walk frames
of a direction get the same offset as its idle, so the walk cycle doesn't hop.
After exporting: right-click the folder in Unity → DgVillage → Create Character Definition
(folders under Pets/ register in PetRegistry, not in the character select).
"""

import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "unity/Assets/_Project/Sprites/Characters/Pets"
SIZE = 64
FEET_Y = SIZE - 10

DIRECTIONS = {
    "south": "south",
    "north": "north",
    "east": "east",
    "south-east": "southeast",
    "north-east": "northeast",
}


def place(image: Image.Image, dy: int) -> Image.Image:
    """Center on 64×64, then shift down by dy."""
    canvas = Image.new("RGBA", (SIZE, SIZE))
    canvas.paste(image, ((SIZE - image.width) // 2, (SIZE - image.height) // 2 + dy))
    return canvas


def main(source: Path, name: str) -> None:
    out = OUT / name
    out.mkdir(parents=True, exist_ok=True)
    for pixellab, ours in DIRECTIONS.items():
        idle_path = source / "rotations" / f"{pixellab}.png"
        frames = sorted(
            (source / "walk" / pixellab).glob("*.png"), key=lambda p: int(p.stem.split("_")[-1])
        )
        if not idle_path.exists() or not frames:
            print(f"skip {ours}: idle={idle_path.exists()} walk={len(frames)}")
            continue
        idle = Image.open(idle_path).convert("RGBA")
        walk = [Image.open(f).convert("RGBA") for f in frames]
        # walk canvases can be bigger than the idle: pad the idle to the walk size first (centered)
        if walk[0].size != idle.size:
            idle = place_on(idle, walk[0].size)
        dy = FEET_Y - (place(idle, 0).getbbox()[3])
        place(idle, dy).save(out / f"{name}_idle_{ours}.png")
        for i, frame in enumerate(walk):
            place(frame, dy).save(out / f"{name}_walk_{ours}_{i}.png")
        print(f"{ours}: idle + {len(walk)} walk frames (dy {dy})")


def place_on(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    canvas = Image.new("RGBA", size)
    canvas.paste(image, ((size[0] - image.width) // 2, (size[1] - image.height) // 2))
    return canvas


if __name__ == "__main__":
    main(Path(sys.argv[1]), sys.argv[2])
