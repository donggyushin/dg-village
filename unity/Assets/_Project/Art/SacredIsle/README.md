# Sacred isle (신성한 섬)

2026-11-12에 만들었다. `Academy north garden`(그룹 18)의 북서쪽, 즉 게임 화면의 왼쪽 물 위에 있는 섬이다. 정원 쪽에는 비탈길·해변·선착장을 붙였다. 나중에 배를 타고 두 선착장 사이를 오갈 예정이다. 배(`Ferry`)는 `Garden pier` 옆에 정박해 있다(설명은 `Art/Ferry/README.md`). 아직 움직이지 않는다.

- Hierarchy: `Willow Court/21 • Sacred isle`. 그룹은 월드 원점에 있다(`Willow Court` 루트는 y 1.2).
- 생성기: `Editor/SacredIsleBuilder.cs`, 메뉴 **DG Village/Waterfront/Sacred isle**. 다시 실행하면 그룹 21을 지우고 새로 만든다.
- 검증: `SacredIsleBuilder.Validate()`. 별도 Preview PhysicsScene에 실제 Collider를 복사한다. 그리고 플레이어와 같은 캡슐(반지름 0.28, 높이 1.6, 4 m/s)로 경로를 걷고 벽을 민다.

## 구성

- **섬**: 중심 (-83.5, 95.4), 반지름 약 12.5 m. 2026-11-12 같은 날 오너 요청으로 정원 해변에서 화면 왼쪽(북서)으로 88 m 떨어진 곳으로 옮겼다(처음에는 (-52, 62)). 둘레는 사인파로 구불구불하다. 윗면은 y 4.6이라 물(y -1.25)보다 약 6 m 높고, 가운데로 갈수록 0.45 m 더 솟는다. 둘레는 물속까지 내려가는 바위 절벽이다.
  - 동남쪽(본토 방향)에는 작은 만과 해변(`Sacred beach sand`)을 두고 선착장 `Isle pier`(16 m)를 붙였다.
  - 해변에서 시작하는 흙길(`Sacred trail`)은 절벽을 따라 시계 방향으로 약 97°를 돌며 오른다. 기울기는 약 15°다. 바깥쪽에는 나무 난간이 있고, 안쪽 절벽은 길을 깎아 낸 모양이다.
  - 섬 가운데에는 신성한 나무 `Sacred tree`(13.5 m)가 있다. 금줄·등불이 달려 있고 `Occluder`가 붙어 있어, 뒤에 선 캐릭터를 가리면 반투명해진다. 줄기에는 Capsule Collider가 있다.
  - 나무 둘레 반지름 5.6 m에 룬 돌 5개를 길 쪽만 비워 두고 둥글게 세웠다. 길 끝 양옆에는 석등 2개를 두었다.
  - 그 밖에 빛나는 버섯, 영혼 꽃, 데이지, 풀, 반딧불 파티클(`_FX_Mote`)을 배치했다. Point Light는 따뜻한 빛·청록 빛 5개다.
- **본토 비탈길**: 정원 가장자리 매개변수 a 1.72–1.94 구간의 참나무 울타리 20조각을 **비활성화**했다(삭제하지 않음). 그 자리에서 정원 높이(2.74)의 작은 전망대가 시작된다. 길은 정원 절벽 바깥을 따라 서쪽으로 약 14 m 내려가 해변에 닿는다. 기울기는 약 13°이고, 바깥쪽에는 나무 난간이 있다.
- **본토 해변**: 중심 (-21.3, 33.2)의 타원(8.4 × 6.2 m)이다. 가장자리는 사인파라 직사각형이 아닌 자연스러운 곡선이고, 물속으로 완만하게 들어간다. 선착장 `Garden pier`(16 m)는 섬 쪽을 향한다. 두 선착장 끝 사이의 물길은 약 39 m다.
- **배 정박 자리**: 배는 두 선착장의 북동쪽(화면 위쪽)에 댄다. 선착장 시작점에서 14 m 지점에 배의 왼쪽 출입구가 오고, 경사 발판(0.65 m 오르막)이 선착장 위로 내려온다. 그 자리의 선착장 투명 벽은 1.4 m 비워 두었고, 양옆에 계류 기둥이 있다. 두 정박 자리는 `Ferry route/Garden berth`·`Isle berth` 빈 오브젝트로 표시했다. 두 자리 사이는 직선 46.4 m다. 배 아래 물속 지면은 `BerthCarve`로 깊게 파서, 선체가 바닥에 닿지 않는다(물 위로 드러난 모래는 건드리지 않는다).
- **바다**: 섬이 멀어져서 `05 • Canal & bridge/Canal water`의 Scale을 1.8배로 키웠다. 물 셰이더는 월드 좌표를 쓰므로 무늬는 늘어나지 않는다. 해안 거리맵 범위 `_MapRect`도 (-110, -115, 230, 230)에서 (-150, -130, 300, 300)으로 넓혔고, 텍스처를 1024에서 2048로 다시 샘플링했다.

## 방식

- 지형은 0.3 m 격자의 높이 함수 메시다(`Meshes/Garden beach & ramp`, `Meshes/Sacred isle`). 삼각형마다 재질을 고른다. 경사가 크면 절벽(`Coast cliff`), 길이면 흙길, 낮으면 모래, 나머지는 잔디(`Moss`)다. 같은 메시를 MeshCollider로도 쓴다.
- 보이지 않는 벽은 "걸을 수 있음" 값(경사 < 약 39°, 물보다 0.15 m 이상 높음, 본토는 정원 절벽에서 0.8 m 바깥)의 경계선을 따라 marching squares로 만든 양면 메시 Collider다(`* walls`). 선착장 자리는 비워 두었고, 선착장에는 따로 양옆·끝 투명 벽이 있다.
- 물의 해안 거리맵 `AcademyGarden/Textures/NorthShoreDistance.png`에 새 땅까지의 거리를 기존 값과의 min으로 구웠다. 그래서 섬과 해변 둘레에 얕은 물빛이 생긴다. 섬을 옮긴 뒤에는 `git show 5052f0f:…/NorthShoreDistance.png`(섬 이전 원본, 범위 (-110,-115,230))를 새 범위로 다시 샘플링한 다음 빌더를 실행했다. 그래서 옛 섬 자리에 얕은 물빛이 남지 않는다. 빌더를 다시 돌리면 새 땅과의 min만 더한다.
- 두 지형 Collider와 두 선착장 Collider를 `SquareSpawnPlacement._surfaces`에 추가했다.

## 에셋

| 파일 | 제작 |
|---|---|
| `SacredTree.png`, `RuneStone.png`, `Driftwood.png`, `SandTile.png`, `TrailTile.png` | Codex (`codex exec`, 내장 이미지 생성). 참고 이미지: `tree_oak.png`, 게임 화면, `GrassTile.png` |
| `StoneLantern.png`, `GlowMushrooms.png`, `SpiritFlowers.png`, `BeachRocks.png` | PixelLab Create Object (Pro, top-down, 128px 2×2). 여백을 자르고 nearest 4배 확대. `SpiritFlowers`는 감마로 밝게 보정 |

갈대(`ShoreReeds`), 가로등(`lamp_iron`), 풀·데이지 Prefab, 나무 재질은 기존 에셋을 재사용했다.

Codex 프롬프트 요약: "GIANT sacred tree … mysterious but cozy and warm … pale silver-grey bark and moss, huge canopy … soft teal-green … small warm glowing paper lanterns … sacred straw rope with white paper streamers … tiny floating golden light motes", 그리고 이끼 낀 룬 돌, 모래 위 유목, 이음새 없는 모래·흙길 타일.
PixelLab 항목: "old mossy stone shrine lantern with a warm glowing light inside" / "cluster of softly glowing teal blue mushrooms on moss" / "small bush of pale white and lavender glowing spirit flowers" / "smooth grey beach rocks with seashells and a small starfish".

## 검증 (2026-11-12, 섬 이동·배 추가 후 다시 실행)

- 정원 잔디 → 전망대 → 비탈길 → 해변 → 선착장 끝 → 경사 발판 → 배 갑판(돛대 두 개를 돌아 뱃머리, 다시 선미루 문 앞): 웨이포인트 27/27에 16.5초 만에 도착했다(갑판 y 0.35).
- 섬 선착장 끝 → 해변 → 오르막길 → 나무 앞: 웨이포인트 29/29에 11.5초 만에 도착했다(y -0.37 → 4.94).
- 벽 밀기 71회(섬 길 바깥쪽, 섬 윗면 가장자리, 본토 비탈길 바깥쪽, 선착장 양옆, 본토 해변 둘레, 배 갑판 오른쪽·뱃머리·왼쪽 난간): 이탈 0회.
- 두 정박 자리 모두 선체 안쪽 바닥에 지면이 닿는 곳이 0곳이다(Raycast).
- 게임 카메라 각도(30, 45, 0) 정사영으로 렌더해 확인했다. 이미지는 `docs/visual-reviews/sacred-isle/`에 있다.
- Play 모드 걷기와 온라인 접속 확인은 하지 않았다(SquareScene은 로그인 뒤 진입).
