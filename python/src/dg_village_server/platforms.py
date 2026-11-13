"""Platforms (moving things players can stand on) in each scene, by id.

The client has the same ids on its platform objects. Adding a platform = add its id here; no
new message or database change is needed.
"""

PLATFORMS: dict[str, frozenset[str]] = {
    "SquareScene": frozenset({"ferry_willow"}),  # Garden pier <-> Sacred isle ferry
}


def is_platform(scene: str, platform: str) -> bool:
    return platform in PLATFORMS.get(scene, frozenset())
