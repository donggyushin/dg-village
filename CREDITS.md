# Credits

dg-village 에 쓰인 외부 에셋과 라이선스. 새 에셋을 넣을 때마다 여기에 한 줄 추가한다.

## 3D 모델

| 에셋 | 제작자 | 라이선스 | 출처 | 사용 위치 |
|---|---|---|---|---|
| Floating Island of the Potion Brewer | [vilmariina](https://sketchfab.com/vilmariina) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) | [Sketchfab](https://sketchfab.com/3d-models/floating-island-of-the-potion-brewer-555992efd5c74cf299f5a3ab4acd0a3b) | `unity/Assets/_Project/Models/PotionBrewerIsland/` (CharacterLobby 섬) |

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

- 캐릭터 스프라이트 `Sprites/Characters/LavenderGirl` — PixelLab 으로 생성 (프롬프트: `pixellab-metadata.json`).
- 바닥·벽 텍스처 `Texture/PixelLab/` (Grass, Cobblestone, Dirt, StoneWall, 128×128) — PixelLab Create tiles (Square top-down, 128px, no outline) 로 생성, 2026-11-01. Grass·Dirt 는 이음새 제거 후처리(반 오프셋 + 디더 마스크 합성). StoneWall 은 cobblestone 프롬프트 결과 중 엇갈린 벽돌 타일.
- 나무 스프라이트 `Sprites/Environment/Tree/FantasyTree_01.png` (256×256) — PixelLab Create Object (Pro, 1 direction, low top-down) 로 생성, 2026-11-03. 프롬프트: "a large beautiful fantasy tree, single tree, full tree from roots to crown, thick twisted brown trunk, lush round layered canopy of soft green leaves with warm sunlit highlights, classic 2000s Korean MMORPG sprite style, soft cel shading, soft warm palette, transparent background, no ground, no shadow".
