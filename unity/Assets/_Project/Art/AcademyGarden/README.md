# Academy north garden

SquareScene의 `The waterside academy` 북쪽에 기존 테라스와 이어지는 자연형 정원을 추가했다. 결과는 Scene에 저장되어 있으며 생성기를 실행할 필요가 없다.

- Hierarchy: `Willow Court/18 • Academy north garden`.
- 지면: y=2.74, 기존 테라스 북쪽 z≈21.06에서 최대 z≈45.62까지 약 24.6m 확장. 동서 폭 약 37m. 작은 만과 곶을 가진 비대칭 곡선 윤곽과 바위 해안이다.
- academy와 기존 건물의 위치·크기는 그대로 유지했다. 건물 뒤쪽 바닥 장식까지 새 땅이 받쳐준다.
- 나무 울타리: 해안선에서 약 0.7m 안쪽, 기둥·두 단 가로대와 연속적인 BoxCollider 50개. 양끝은 기존 석재 난간에 연결된다.
- 기존 북쪽 석재 난간과 북쪽 invisible boundary는 삭제하지 않고 비활성화했다. 기존 동서 district boundary는 북쪽 끝을 줄여 정원을 가로막지 않게 했다.
- 잔디 위에 나무 8그루, 꽃·관목·벤치·램프를 배치했다. academy 양옆으로 진입할 수 있다. (분홍 디딤돌 산책로는 2026-11-10 오너 요청으로 삭제했다.)
- 새 지면 MeshCollider를 `SquareSpawnPlacement._surfaces`에 추가해 정원에서의 저장 위치 복원도 지원한다. 런타임 C# 코드는 변경하지 않았다.
- 수변 거리맵과 물 Material은 이 폴더에 별도 복제·확장했고 Scene의 물에 연결했다. 원본 수변 에셋은 덮어쓰지 않았다.

## 제작 정보

지형 Mesh와 울타리는 Codex가 Unity Editor 코드로 제작했다. 잔디·바위·식재·벤치 등은 저장소의 WillowCourt, WaterfrontUpgrade, Square 에셋을 재사용했고 나무색 Material은 CanalLanding에서 복제했다. 외부 다운로드, AI 이미지 생성, 유료 서비스는 사용하지 않았다. 재사용 에셋의 출처는 각 원본 폴더 기록을 따른다.

## 검증

- 기존 테라스 접합부 하향 Raycast 1,360회: 바닥 누락 0.
- 울타리 개별 구간 Raycast 550회: 누락 0.
- 별도 Preview PhysicsScene에서 실제 울타리 Collider와 접합부 Collider를 복제하고, 반지름 0.28m·높이 1.6m Rigidbody 캡슐을 4m/s로 각 구간 중앙·양끝을 향해 1.8초 이동: 총 150회, 이탈 0.
- 정원 중앙 (-2, 31)의 저장 위치: y=2.86으로 복원 확인.
- 신규 Missing Script 0. 기존 serialized object 삭제 0.
- 실제 게임 Camera 각도 (30°, 45°)로 시각 검토. 검토 이미지는 `docs/visual-reviews/academy-garden/overview.png`.
- 온라인 로그인·멀티플레이 및 새 배포 빌드는 이번 작업 범위에 포함하지 않았다.

`AcademyGardenBuilder.Build()`는 중복 실행을 막는다. `AcademyGardenValidation.Run()`은 임시 PhysicsScene을 만들고 시험 후 닫으며 실제 Scene의 Rigidbody를 움직이지 않는다.

## Academy terrace 자연 지형 (2026-11-10)

`Academy terrace`(x -20.25..17.01, z 4.86..21.06, 윗면 y 2.74)를 북쪽 정원처럼 자연 지형으로 바꿨다. 생성기는 `Editor/AcademyTerraceCoast.cs`이고, 메뉴는 **DG Village/Waterfront/Academy terrace cliffs**이다. 다시 실행하면 그룹 `19 • Academy terrace cliffs`를 새로 만든다.

- 바닥: 분홍 `Terrace paving`을 `Terrace lawn`(Moss)으로 바꿨다. 메시 `Meshes/Academy terrace lawn`은 원래 박스와 모양이 같고, UV만 월드 좌표로 옮겨 북쪽 잔디와 무늬를 맞췄다. 콜라이더는 그대로다.
- 계단 위 분홍 돌 포장(apron)과 디딤돌을 시험했다. 오너가 "없는 게 더 예쁘다"고 해서 모두 지웠다. 테라스와 북쪽 정원의 디딤돌도 함께 지웠다. 바닥은 잔디만 남는다.
- 서쪽 가장자리: 직선 석축 위 돌 난간을 삭제했다. 그 자리에 폭 0.8–3.8 m의 풀밭 가장자리와 물속(y -2.9)까지 내려가는 바위 절벽(`Cliff rim`)을 만들었다. East heights의 `Cliff rim`과 같은 방식이다. 가장자리 폭은 양 끝에서 0으로 줄어든다. 남쪽 끝은 Lower court 석축과, 북쪽 끝은 북쪽 정원 바위 해안과 이어진다.
- `Cliff rim` MeshCollider를 `SquareSpawnPlacement._surfaces`에 추가했다. 서쪽 `Invisible district boundary`는 z 5.61에서 끝나게 줄였다. 그 북쪽은 가장자리 0.45 m 안쪽의 투명 벽 12개가 막는다.
- 풀·야생화·고사리 6개와 절벽 덩굴 3개를 배치했다. 텍스처는 WaterfrontUpgrade의 Cliff*.png를 재사용했다. `Textures/NorthShoreDistance.png`에 새 절벽 발치를 기존 값과의 min으로 구웠다.
- 동쪽 난간, Lower court, 플랜터, 건물은 그대로다.

주의: `Willow Court` 루트는 y 1.2에 있다. 그래서 생성기는 그룹을 월드 원점에 둔다. 첫 버전(5052f0f)은 이것을 빠뜨려 `Cliff rim`과 apron이 1.2 m 떠 있었다.

검증: 경계 안쪽 하향 Raycast 2,969회에서 2,533회가 y 2.74 바닥에 닿았다. 나머지 436회는 화단 위에 닿았고, 바닥 누락은 0이다. 서쪽으로 향하는 수평 Raycast 162회에서 투명 벽 누락 0. 게임 카메라 각도와 낮은 원근 시점으로 렌더해 확인했다(`docs/visual-reviews/academy-garden/terrace-cliffs.png`). Play 모드 걷기 테스트는 하지 않았다.
