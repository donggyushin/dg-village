# Canal Landing

SquareScene의 광장 남쪽 수로에 장식용 나룻배 한 척과 목조 선착장 하나를 추가했다.

- Scene 위치: `Willow Court/Canal Landing`, world (-8.8, -0.65, -19.3).
- 선착장: 개별 널판, 지지 말뚝, 밧줄, 석축 사다리, 작은 랜턴.
- 배: 열린 청록색 선체, 나무 테두리, 좌석, 바닥판, 노 두 개, 계류 밧줄.
- 정적인 장식이다. 승선·이동·상호작용 기능이나 보행 Collider는 없다. 기존 수변 안전 경계를 유지한다.
- Mesh와 URP Lit Material은 Codex가 직접 제작했다. 외부 에셋이나 유료 서비스는 사용하지 않았다.
- Editor 생성기는 한 번만 실행되며 Scene에 결과가 저장되어 있다. 런타임 생성은 없다.
- 모든 신규 에셋은 이 폴더에 모았다. README용 AI 일러스트의 생성 기록은 `docs/images/willow-court-key-art.md`에 있다.

검증: 실제 Scene의 Camera 렌더로 수면과 선체, 석축과 선착장의 배치를 확인했다. 신규 Mesh·Material 누락 0, 신규 Collider 0. 검토 이미지는 `docs/visual-reviews/canal-landing/landing.png`.
