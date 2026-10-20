# dg-village

게더타운처럼 **채널을 만들고, 들어온 사람끼리 캐릭터로 돌아다니며 채팅하는** 소셜 공간 게임.

라그나로크 · 테일즈위버 · 투더문 같은 옛날 한국 RPG 느낌의 **귀여운 2.5D 도트 그래픽**이 특징이다. 처음엔 친구들끼리 쓰고, 잘 되면 일반 사용자로 넓힌다.

## 구성

```
dg-village/
├── unity/    # 게임 클라이언트 (Unity 6 · URP)
└── python/   # WebSocket 서버 (준비 중)
```

| 영역 | 기술 |
|---|---|
| 클라이언트 | Unity `6000.6.6f1` · Universal Render Pipeline · Input System |
| 서버 | Python WebSocket (`asyncio` + `websockets` 또는 FastAPI) |
| 통신 | WebSocket 위 JSON 메시지 |
| 그래픽 | 3D 맵 위에 2D 도트 캐릭터, 고정 카메라 |
| 플랫폼 | Mac · Windows 데스크톱 우선, 웹(WebGL)은 나중에 |
| 음성 | 나중에 LiveKit 또는 Vivox 연동 |

## 시작하기

### 클라이언트
1. [Unity Hub](https://unity.com/download) 설치 (`brew install --cask unity-hub`)
2. Hub에서 에디터 `6000.6.6f1` 설치 — 모듈: Mac Build Support, Windows Build Support (Mono)
3. Hub → Projects → Add → `unity/` 폴더 선택 후 열기

처음 열 때 `Library/` 를 새로 만드느라 몇 분 걸린다.

### 서버
아직 없음.

## 로드맵

- [ ] 기준 캐릭터 만들기 → Unity에서 캐릭터 이동
- [ ] 서버 연결 — 한 방에서 여러 명이 서로 움직이는 게 보이기
- [ ] 텍스트 채팅 + 말풍선
- [ ] 채널 만들기 / 입장
- [ ] 아트 입히기
- [ ] 음성 채팅
