# Fluffy lawn (2026-11-10)

잔디가 거의 단색이라 납작해 보였다. 두 가지를 바꿔 잔디가 복슬복슬해 보이게 했다.

1. **잔디 텍스처:** 공용 잔디 재질 `WillowCourt/Materials/Moss.mat`의 텍스처를 `Textures/GrassTile.png`로 바꿨다. 이 그림은 손그림 느낌의 이음새 없는 1024 px 타일이고, 4.5 m마다 반복된다. 이 재질을 쓰는 모든 잔디(북쪽 정원, Academy terrace, South bank, East heights 가장자리, 화단 흙 등)에 적용된다. 원래 텍스처 `Square/MeadowSurface.asset`은 그대로 남아 있다.
2. **풀 덤불:** 카메라를 향하는 작은 풀 덤불 스프라이트(`Textures/Tuft1–6.png`)를 큰 잔디밭 위에 뭉치 단위로 흩뿌렸다. 그룹 이름은 `Willow Court/20 • Fluffy lawn tufts`이고, 현재 926개다.
   - 대상 잔디밭: `Terrace lawn`, `North garden walkable lawn`, `South bank`, 두 `Cliff rim`.
   - 덤불은 Perlin noise로 뭉치를 만들어 배치한다. 혼자 떨어진 덤불은 점처럼 보이므로 만들지 않는다.
   - 덤불 색은 잔디 톤에 맞게 낮췄다(`TuftTint`). 그림자는 끄고 static batching을 켰다.
   - 콜라이더가 있는 건물·화단·벤치·울타리 근처와 투명 벽 바로 안쪽은 피한다.
   - 잔디 콜라이더 위에 보이는 포장·광장·장식 바닥은 충돌판이 없는 납작한 메시다. 생성기는 이런 메시에 임시 탐침 콜라이더(Ignore Raycast 레이어)를 붙여 그 자리를 건너뛰고, 끝나면 탐침을 지운다.

생성기는 `Editor/FluffyLawn.cs`이고 메뉴는 **DG Village/Waterfront/Fluffy lawn**이다. 다시 실행할 수 있다. 밀도(`PatchCut`), 크기(`TuftHeight`), 색(`TuftTint`), 타일 크기(`TileScale`)는 파일 맨 위의 public static 값으로 조절한다.

## 제작 정보

`GrassTile.png`와 덤불 원본 시트는 Claude의 요청으로 **Codex(OpenAI 이미지 생성)**가 만들었다. 이음새 처리와 마젠타 배경 제거도 Codex가 Python으로 했다. 시트를 덤불 6장으로 자르는 작업과 배치·재질 작업은 Claude가 했다.

## 검증

- 게임 카메라(정사영, 30°/45°, Size 7, 1920×1080)로 Academy 정원, South bank 광장, East heights를 렌더해 확인했다. 결과는 `docs/visual-reviews/fluffy-lawn/`에 있다.
- 포장 탐침은 메시 1,082개를 썼고, 포장 위에 놓일 뻔한 199곳을 건너뛰었다. 실행 뒤 남은 탐침은 0개다.
- Play 모드 테스트와 프레임 측정은 하지 않았다.
