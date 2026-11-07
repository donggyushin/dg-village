# Credits

dg-village 에 쓰인 외부 에셋과 라이선스. 새 에셋을 넣을 때마다 여기에 한 줄 추가한다.

## 3D 모델

| 에셋 | 제작자 | 라이선스 | 출처 | 사용 위치 |
|---|---|---|---|---|
| Floating Island of the Potion Brewer | [vilmariina](https://sketchfab.com/vilmariina) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) | [Sketchfab](https://sketchfab.com/3d-models/floating-island-of-the-potion-brewer-555992efd5c74cf299f5a3ab4acd0a3b) | `unity/Assets/_Project/Models/PotionBrewerIsland/` (CharacterLobby 섬) |
| Beach kit (파라솔만 사용) | [jungle_jim](https://sketchfab.com/jungle_jim) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) | [Sketchfab](https://sketchfab.com/3d-models/beach-kit-6e0e5cc33a1542679f8383e9e260eb90) | `unity/Assets/_Project/Models/City/BeachKit/` (SquareScene 도시) |
| Market Props | [shonesplash3](https://sketchfab.com/shonesplash3) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) | [Sketchfab](https://sketchfab.com/3d-models/market-props-224de833ac6441f095d04df0074c1135) | `unity/Assets/_Project/Models/City/MarketProps/` (SquareScene 도시) |

수정 사항: 풀·잎·빨래·연기 텍스처를 알파 채널 PNG 로 합쳐 Unity 재질을 새로 만듦 (`Models/PotionBrewerIsland/Alpha/`).

## 폰트

| 에셋 | 제작자 | 라이선스 | 출처 | 사용 위치 |
|---|---|---|---|---|
| Galmuri11, Galmuri11 Bold | Lee Minseo (quiple) | [SIL OFL 1.1](https://openfontlicense.org) | [GitHub](https://github.com/quiple/galmuri) | `unity/Assets/_Project/Fonts/` (전문: `Galmuri-LICENSE.txt`) |

## 텍스처 (테스트용)

| 에셋 | 라이선스 | 비고 |
|---|---|---|
| `Texture/RockFace03_diff_1k.png` | 출처 확인 필요 | 테스트용. 출시 전 교체하거나 출처 기록 |
| `Texture/grasstitle_for_test.png` | 출처 확인 필요 | 테스트용. 출시 전 교체하거나 출처 기록 |

## 자체 제작

- SquareScene 광장 구성 `Art/Square/`, `Editor/SquareArtDirector.cs` — 기존 자체 제작 건물·정원 에셋을 재배치하고 모듈형 포장·계단·화단, 반복 가능한 잔디 표면, 물방울·꽃가루 효과와 색보정을 제작, 2026-11-07. `MeadowSurface.asset`은 주기적 다중 크기 노이즈와 풀잎 패턴으로 생성한 자체 텍스처. Witchbrook은 색감과 공간 구성 참고이며 해당 게임의 이미지·에셋은 포함하지 않음.
- 캐릭터 스프라이트 `Sprites/Characters/LavenderGirl` — PixelLab 으로 생성 (프롬프트: `pixellab-metadata.json`).
- 바닥·벽 텍스처 `Texture/PixelLab/` (Grass, Cobblestone, Dirt, StoneWall, 128×128) — PixelLab Create tiles (Square top-down, 128px, no outline) 로 생성, 2026-11-01. Grass·Dirt 는 이음새 제거 후처리(반 오프셋 + 디더 마스크 합성). StoneWall 은 cobblestone 프롬프트 결과 중 엇갈린 벽돌 타일.
- 나무 스프라이트 `Sprites/Environment/Tree/FantasyTree_01.png` (256×256) — PixelLab Create Object (Pro, 1 direction, low top-down) 로 생성, 2026-11-03. 프롬프트: "a large beautiful fantasy tree, single tree, full tree from roots to crown, thick twisted brown trunk, lush round layered canopy of soft green leaves with warm sunlit highlights, classic 2000s Korean MMORPG sprite style, soft cel shading, soft warm palette, transparent background, no ground, no shadow".
- 잔디 타일 `Texture/PixelLab/LawnOlive.png` (128×128) — PixelLab Create tiles (Square top-down, 128px, no outline) 로 생성한 6장 중 1장에 이음새 제거 후처리(반 오프셋 + 노이즈 마스크 합성), 2026-11-03. 프롬프트: "seamless flat lawn ground texture, soft mossy olive green, very low contrast, large soft blotches, no individual grass blades, no flowers, no stones, no outline".
- 잔디 이미지 `Texture/PixelLab/LawnBig_01.png` (512×512) — PixelLab Create image (Pixen, Highly detailed, Lineless) 로 생성, 2026-11-03. 프롬프트: "flat mossy lawn ground texture seen from directly above, filling the entire image edge to edge, muted olive green, very low contrast, soft irregular patches of slightly darker and lighter moss, tiny subtle leaf clumps, no flowers, no stones, no path, no objects, no horizon".
- SquareScene 정원 소품 `Sprites/Environment/Garden/` (27종) — PixelLab Create Object (Pro, top-down, 128px 2×2 묶음 또는 256px) 로 생성, 2026-11-04. 나무·집·가로등은 Scale2x 로 2배 확대, 전부 여백 제거 후 사용. 집 3종과 가로등 2종, `tree_oak` 은 기존 PixelLab 생성물 재사용. 프롬프트는 PixelLab Objects 갤러리에 남아 있음.
- SquareScene 도시 건물·분수 그림 `Sprites/Environment/City/*.png` (6장) — ChatGPT 이미지 생성으로 만듦, 2026-11-07. 기존 Garden 소품·LavenderGirl 을 화풍 참고 이미지로 넣음. 가장자리 붉은 테두리는 Python 으로 1px 정리.
- SquareScene 도시 바닥·벽 `Texture/City/*.png` — Python 스크립트로 직접 그림(외부 에셋 없음), 2026-11-07.
- SquareScene 바닥 `Texture/Garden/GardenGround.png`, `Texture/Garden/ForestFloor.png` — Python 스크립트로 직접 그림(외부 에셋 없음), 2026-11-04.
- 바닥 무늬 `Texture/Decals/FloorEmblem_01.png` (256×256) — PixelLab Create Object (Pro, 1 direction, top-down) 로 생성, 2026-11-03. 프롬프트: "ornate symmetrical floor inlay emblem, flat 2D pattern seen from directly above, diamond and quatrefoil shapes, warm brown and cream wood tones, thin clean lines, transparent background, no shadow, no perspective".
