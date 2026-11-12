# Ferry (여객선)

2026-11-12에 만들었다. 정원 해변의 `Garden pier`와 신성한 섬의 `Isle pier` 사이를 오갈 배다. 아직 움직이는 코드는 없다. 지금은 `Garden pier` 북동쪽(화면 위쪽)에 정박해 있다.

- Prefab: `Art/Ferry/Ferry.prefab`. 씬에는 `Willow Court/21 • Sacred isle/Ferry`로 놓여 있다.
- 생성기: `Editor/FerryBuilder.cs`. 메뉴 **DG Village/Waterfront/Ferry prefab**은 Prefab만 다시 만든다. `SacredIsleBuilder`(메뉴 **DG Village/Waterfront/Sacred isle**)도 Prefab을 다시 만든 뒤 정박 자리에 놓는다.
- 그림이 아닌 3D 메시로 만들었다. 플레이어가 갑판 위를 걸어 다녀야 하므로, 갑판 앞뒤의 가림 순서가 깊이로 맞아야 하기 때문이다.

## 크기와 좌표 (Prefab 로컬)

- +Z = 뱃머리, +X = 오른쪽(우현), y 0 = 갑판 윗면. 씬에서는 y 0이 선착장 윗면(월드 y -0.3)과 같다.
- 길이 12 m(선미 z -5.75 ~ 뱃머리 z 6.25), 최대 폭 4.3 m, 흘수선 y -0.95(월드 물 높이 -1.25), 용골 y -1.9.
- 걸을 수 있는 곳: `Deck` MeshCollider. 둘레에는 `Invisible deck walls`(양면 MeshCollider, 높이 2.6 m)가 있다.
- 출입구: 왼쪽(좌현, -X) z -1 ~ 1. 이 구간만 뱃전과 벽이 없다.
  - `Gangway`: 출입구에서 선착장 위까지 걸친 발판이다(콜라이더 포함). 정박 중에는 켜 둔다.
  - `Boarding gate`: 출입구를 막는 밧줄 차단봉과 투명 벽이다. 처음에는 **꺼져 있다**. 배가 떠날 때 켜고, 도착하면 끈다.
- 두 정박 자리: `21 • Sacred isle/Ferry route/Garden berth`, `Isle berth`. 둘 다 배의 위치·회전과 같다. 두 자리는 직선으로 56.4 m 떨어져 있다. 배는 방향을 바꾸지 않고 앞(섬 쪽)이나 뒤로 평행 이동하면 된다. 두 자리 모두 출입구가 선착장 쪽을 향한다.

## 구성

- 선체: 단면을 이어 만든 메시(`Meshes/Ferry hull`). 바깥은 `FerryHull.png`(위에서부터 참나무 뱃전, 청록 띠와 금색 덩굴무늬, 꿀색 겹붙임 판자, 물 아래 적갈색)다. 흘수선이 적갈색 띠 바로 위에 오도록 v좌표를 맞췄다.
- 갑판 `FerryDeck.png`, 뱃전 안쪽은 `Honey oak`, 난간 윗면은 `Oak end grain`이다.
- 돛대(8.6 m)와 개프 돛(`FerrySail.png`, 신성한 나무 문양)이 있다. 돛은 배의 중심면에 있어서 게임 카메라에서는 정면으로 보인다. 돛에 `Occluder`를 붙여, 돛 뒤의 캐릭터를 가리면 반투명해진다.
- 돛대 꼭대기 깃발(`FerryPennant.png`), 앞·옆·뒤 밧줄(LineRenderer), 뱃머리 기움대, 뱃머리와 돛대 사이의 등불 줄이 있다.
- 선미 선실: 청록 지붕, 왼쪽 창 3개, 문, 선실 등불이 있다. 그 앞에 조타륜이 있다. 둘 다 콜라이더가 있다.
- 벤치 4개(콜라이더 있음), 뱃머리 상자 더미(`crates` Prefab), 뱃머리 등불과 선미 등불 2개(Point Light), 왼쪽 밧줄 방현재 4개가 있다.

## 나중에 배를 움직일 때 할 일 (아직 안 함)

1. 배 루트에 `Rigidbody`(Is Kinematic)를 붙이고 `MovePosition`으로 움직인다. 콜라이더를 Transform으로만 옮기면 위에 선 플레이어가 따라오지 않는다.
2. 갑판 위 플레이어가 배와 함께 움직이게 해야 한다. 흔한 방법은 배의 이동량을 플레이어에게 더해 주는 것이다.
3. 출발할 때 `Boarding gate`를 켜고 `Gangway`를 끈다. 도착하면 반대로 한다.
4. 멀티플레이어에서는 서버가 배의 출발 시각을 정하고, 클라이언트는 그 시각으로 위치를 계산하면 모두 같은 위치를 본다(날씨 API와 같은 방식).

## 에셋

| 파일 | 제작 |
|---|---|
| `FerryHull.png`, `FerryDeck.png`, `FerrySail.png`, `FerryPennant.png` | Codex (`codex exec`, 내장 이미지 생성). 참고 이미지: 게임 화면(정원 해변) |

Codex 프롬프트 요약: "cozy fantasy … hand-painted, warm, soft … NOT photoreal, NOT pixel art". 선체는 "SIDE of a wooden boat hull, unrolled flat … tile LEFT-RIGHT … dark oak gunwale / deep teal band with gold scroll ornaments / honey-oak clinker planks with copper rivets / dark red-brown underside"이다. 갑판은 "top-down ship deck, honey oak planks running vertically, seamless both ways"다. 돛은 "weathered ivory canvas sail filling the image … faded teal sacred tree emblem with a gold ring"이다. 깃발은 "long thin swallow-tailed pennant pointing right, teal with a gold stripe, transparent"다.

재사용한 재질: `AcademyGarden/Materials/Weathered oak`, `Oak end grain`, `CanalLanding/Materials/Honey oak`, `Painted teal hull`, `Hemp rope`, `Dark iron`.
