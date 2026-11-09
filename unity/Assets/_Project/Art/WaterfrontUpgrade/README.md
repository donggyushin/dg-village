# SquareScene 수변 개선 — 2026-11-10

현재 결과는 `Assets/_Project/Scenes/SquareScene.unity`에 저장되어 있다. Editor 생성기를 실행할 필요는 없다.

## 최종 비율

- 평면 배치와 건축 Mesh: 최초 대비 X/Z 각각 1.62배(1.35 × 1.20). 면적 약 2.62배.
- 기본 광장 상단: y=1.21. 기존 지면·건물·식재·계단은 모두 1.2m 상승.
- 물 표면: y≈-1.24 유지. 기본 광장과 약 2.45m 차이.
- 동쪽 상부 광장: y=5.74. 기존 계단 단차와 캐릭터 크기는 유지.
- Camera와 Cinemachine Orthographic Size: 8.3. 사용자 요청으로 원래 캐릭터 표시 크기를 복원했다. 미리보기 이미지는 복원 전 구도로 촬영되었다.
- 석축 하부를 연장하고 UV 밀도를 보정했다. 아치 다리는 보행 곡선을 유지하며 하부만 연장했다.

## 물과 식재

`GardenWater.shader`는 실제 석축 경계에서 구운 `BankDistance.png`를 사용한다. 얕은 곳의 자갈, 깊은 청록색, 시간에 따라 움직이는 잔물결과 하늘빛 하이라이트를 합성한다. 실제 화면 공간 반사나 물리적 굴절 시뮬레이션은 아니다. URP 주광·환경광·그림자에 반응한다.

물가 바위, 홈이 파인 수련 잎, 낮은 풀과 화단 하층 식재를 추가했다. 기존 Square/WillowCourt 에셋을 재사용했고, 새 Shader·Mesh·거리맵은 코드로 직접 제작했다. 외부 다운로드나 AI 이미지 생성은 사용하지 않았다. 원본 에셋의 출처·라이선스 기록은 기존 에셋 위치의 기록을 따른다.

## 접속 위치

`SquareSpawnPlacement`에는 보행 가능한 지면 Collider만 연결되어 있다. `PlayerSpawner`는 저장된 XZ 좌표를 새 지면 높이에 맞춘다. 저장 위치가 물 위로 바뀌었다면 광장 안전 위치로 보낸다. `PlayerMovement`의 추락 복귀도 이 배치를 사용한다. 이 컴포넌트가 없는 Scene의 위치 처리 방식은 유지한다.

## 검증

- Unity C# 컴파일 오류 및 물 Shader 컴파일 메시지 없음.
- Missing Script·Mesh·Material 검사.
- 본관 계단, 광장 다리, 동쪽 아치 다리, 동쪽 대계단, 남쪽 아치 다리: 경로별 101개 하향 Raycast에서 지면 누락 없음. 샘플 사이 최대 높이 차이 0.086m 미만.
- 예전 낮은 저장 위치의 높이 보정, 물 위 좌표의 안전 위치 복귀를 실제 Scene Collider로 확인.
- Editor Camera 렌더에서 광장·동쪽 구역·수변을 검토. 최종 미리보기는 저장소 `docs/visual-reviews/waterfront-upgrade/`에 있다. 미리보기 캐릭터는 크기 확인용 임시 Sprite이며 Scene에 추가하지 않았다.
- 로그인부터 진행하는 온라인 Play mode 검증과 대상 기기 성능 프로파일링은 수행하지 않았다.

## 보존한 원본

- `Backups/SquareScene_BeforeWaterfront.unity`: 작업 시작 당시 Scene.
- `Backups/SquareScene_BeforeRaisedTerraces.unity`: 추가 확대·높이 변경 직전 Scene.

두 파일은 비교·복구용이며 Build Settings에 추가하지 않았다. 생성기는 중복 실행을 차단한다. 원래 WillowCourt 생성기의 고정 좌표는 최초 배치 기준이므로, 현재 Scene에 예전 구역 생성기를 다시 실행하면 확대된 배치와 맞지 않는다. 최종 Scene을 직접 편집하는 것을 기준으로 한다.
