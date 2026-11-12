# Ferry (여객선)

2026-11-12에 만들었다. 정원 해변의 `Garden pier`와 신성한 섬의 `Isle pier` 사이를 오갈 배다. 아직 움직이는 코드는 없다. 지금은 `Garden pier` 북동쪽(화면 위쪽)에 정박해 있다.

처음에는 12 m 외돛 범선이었다. 같은 날 오너가 "더 크고 웅장했으면" 해서 20 m 2돛대 스쿠너로 다시 만들었다.

- Prefab: `Art/Ferry/Ferry.prefab`. 씬에는 `Willow Court/21 • Sacred isle/Ferry`로 놓여 있다.
- 생성기: `Editor/FerryBuilder.cs`. 메뉴 **DG Village/Waterfront/Ferry prefab**은 Prefab만 다시 만든다. `SacredIsleBuilder`(메뉴 **DG Village/Waterfront/Sacred isle**)도 Prefab을 다시 만든 뒤 정박 자리에 놓는다.
- 그림이 아닌 3D 메시로 만들었다. 플레이어가 갑판 위를 걸어 다녀야 하므로, 갑판 앞뒤의 가림 순서가 깊이로 맞아야 하기 때문이다. 선수상·선미 갤러리·돛·깃발에는 그림 텍스처를 붙였다.

## 크기와 좌표 (Prefab 로컬)

- +Z = 뱃머리, +X = 오른쪽(우현), y 0 = 주갑판 윗면. 씬에서 주갑판은 선착장 윗면보다 0.65 m 높다(월드 y 0.35).
- 길이 20 m(선미 z -9.5 ~ 뱃머리 z 10.5, 기움대 끝까지 약 24 m), 최대 폭 6.4 m, 흘수선 y -1.6(월드 물 높이 -1.25), 용골 y -3.0. 돛대 꼭대기는 갑판 위 12.8 m(앞)와 14.6 m(뒤)다.
- 걸을 수 있는 곳: `Deck` MeshCollider. 둘레에는 `Invisible deck walls`(양면 MeshCollider, 높이 2.6 m)가 있다. 선미루(stern castle) 위 갑판은 장식이라 올라갈 수 없다.
- 출입구: 왼쪽(좌현, -X) z -1.2 ~ 1.2. 이 구간만 뱃전과 벽이 없다.
  - `Gangway`: 출입구에서 선착장 가운데 너머까지 내려오는 경사 발판이다(약 17°, 콜라이더·밧줄 손잡이 포함). 정박 중에는 켜 둔다.
  - `Boarding gate`: 출입구를 막는 밧줄 차단봉과 투명 벽이다. 처음에는 **꺼져 있다**. 배가 떠날 때 켜고, 도착하면 끈다.
- 두 정박 자리: `21 • Sacred isle/Ferry route/Garden berth`, `Isle berth`. 둘 다 배의 위치·회전과 같다. 두 자리는 직선으로 46.4 m 떨어져 있다. 배는 방향을 바꾸지 않고 앞(섬 쪽)이나 뒤로 평행 이동하면 된다. 두 자리 모두 출입구가 선착장 쪽을 향한다.

## 구성

- 선체: 단면을 이어 만든 메시(`Meshes/Ferry hull`). 바깥은 `FerryHull.png`(위에서부터 참나무 뱃전, 청록 띠와 금색 덩굴무늬, 꿀색 겹붙임 판자, 물 아래 적갈색)다. 흘수선이 적갈색 띠 바로 위에 오도록 v좌표를 맞췄다. 금박 띠(`Gilded wale`, `Gilded rail`)와 양쪽 둥근 창 22개(빛남)가 있다.
- 갑판 `FerryDeck.png`, 뱃전 안쪽은 `Honey oak`, 난간 윗면은 `Oak end grain`, 금 장식은 `Ferry gilt`(금속성)다.
- 뱃머리: 금박 사슴 정령 선수상(`FerryFigurehead.png`, 배 중심면에 세운 그림판), 기움대, 상자 더미 2개, 뱃머리 등불.
- 돛: 앞·뒤 돛대에 개프 돛(`FerrySail.png`, 신성한 나무 문양)과 그 위 삼각 탑세일, 앞쪽에 지브·스테이세일. 모두 배의 중심면에 있어서 게임 카메라에서는 정면으로 보인다. 돛마다 `Occluder`를 붙여, 돛 뒤의 캐릭터를 가리면 반투명해진다.
- 밧줄: 앞 지삭 2개, 돛대 사이 지삭, 뒷 지삭, 돛대마다 양쪽 3줄씩 옆 지삭, 피크 할야드. 기움대 → 앞 돛대 → 뒤 돛대 → 선미 깃대로 이어지는 등불 줄.
- 선미루: 좌·우 벽에 빛나는 아치 창 4개짜리 갤러리(`FerryStern.png`, 창문만 빛나게 `FerrySternGlow.png`를 Emission 맵으로 씀), 금박 모서리, 앞쪽 문과 창, 위에는 금박 난간, 조타륜, 큰 등불 4개, 선미 깃발.
- 벤치 5개(등받이·콜라이더 있음), 난간 등불 6개(빛만, Light 없음), 왼쪽 밧줄 방현재 4개.
- Point Light: 뱃머리 1, 선미루 앞 1, 선미 1, 갤러리 바깥 1, 갑판 가운데 매단 등불 1 (그림자 없음).

## 나중에 배를 움직일 때 할 일 (아직 안 함)

1. 배 루트에 `Rigidbody`(Is Kinematic)를 붙이고 `MovePosition`으로 움직인다. 콜라이더를 Transform으로만 옮기면 위에 선 플레이어가 따라오지 않는다.
2. 갑판 위 플레이어가 배와 함께 움직이게 해야 한다. 흔한 방법은 배의 이동량을 플레이어에게 더해 주는 것이다.
3. 출발할 때 `Boarding gate`를 켜고 `Gangway`를 끈다. 도착하면 반대로 한다.
4. 멀티플레이어에서는 서버가 배의 출발 시각을 정하고, 클라이언트는 그 시각으로 위치를 계산하면 모두 같은 위치를 본다(날씨 API와 같은 방식).

## 에셋

| 파일 | 제작 |
|---|---|
| `FerryHull.png`, `FerryDeck.png`, `FerrySail.png`, `FerryPennant.png` | Codex (`codex exec`, 내장 이미지 생성). 참고 이미지: 게임 화면(정원 해변) |
| `FerryFigurehead.png`, `FerryStern.png` | Codex. 참고 이미지: 첫 번째 배의 게임 화면 |
| `FerrySternGlow.png` | `FerryStern.png`에서 창문 네 곳만 남기고 지운 것(Python) |

Codex 프롬프트 요약: "cozy fantasy … hand-painted, warm, soft … NOT photoreal, NOT pixel art". 선체는 "SIDE of a wooden boat hull, unrolled flat … tile LEFT-RIGHT … dark oak gunwale / deep teal band with gold scroll ornaments / honey-oak clinker planks with copper rivets / dark red-brown underside"이다. 갑판은 "top-down ship deck, honey oak planks running vertically, seamless both ways"다. 돛은 "weathered ivory canvas sail filling the image … faded teal sacred tree emblem with a gold ring"이다. 깃발은 "long thin swallow-tailed pennant pointing right, teal with a gold stripe, transparent"다. 선수상은 "carved, gilded wooden ship FIGUREHEAD … exactly from the SIDE, FACING LEFT: a graceful spirit stag with leafy antlers … vertical flat edge where it attaches"다. 선미 갤러리는 "flat front-on ornate STERN GALLERY wall … 4 tall arched windows glowing warm amber … gold leaf carved trim … teal band at the top"다.

재사용한 재질: `AcademyGarden/Materials/Weathered oak`, `Oak end grain`, `CanalLanding/Materials/Honey oak`, `Painted teal hull`, `Hemp rope`, `Dark iron`.
