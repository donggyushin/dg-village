# dg-village

게더타운처럼 **채널을 만들고, 들어온 사람끼리 캐릭터로 돌아다니며 채팅하는** 소셜 공간 게임.

라그나로크 · 테일즈위버 · 투더문 같은 옛날 한국 RPG 느낌의 **귀여운 2.5D 도트 그래픽**이 특징이다. 일하면서 켜 두는 게임 — 각자 할 일을 하면서, 판타지 마법사 길드 사무실과 정원에서 같은 시간을 보내는 사람들과 채팅한다.

## 다운로드

[**최신 릴리스**](https://github.com/donggyushin/dg-village/releases/latest)에서 받는다.

- **Windows** — `dg-village-windows.zip` 압축을 풀고 `dg-village.exe` 실행. "Windows의 PC 보호" 창이 뜨면 **추가 정보 → 실행**.
- **macOS** (Apple Silicon) — `dg-village-mac.zip` 압축을 풀고 `dg-village.app` 실행 (Apple 공증 완료).

회원가입 없이 바로 들어간다 (익명 계정이 자동으로 만들어짐).

## 구성

```
dg-village/
├── unity/         # 게임 클라이언트 (Unity 6 · URP)
├── python/        # 게임 서버 (FastAPI) — 계정 HTTP API + WebSocket
├── deploy/        # 운영 서버 구성 (Caddy + 서버 + Postgres) — deploy/README.md, macOS 서명 설정
├── art/           # Unity 밖 아트 원본 (팔레트 · 타일셋 · 소품 · 건물) + 변환 스크립트
├── docs/          # 리서치 보고서
└── compose.yaml   # 로컬 PostgreSQL
```

| 영역 | 기술 |
|---|---|
| 클라이언트 | Unity `6000.6.6f1` · Universal Render Pipeline · Input System |
| 서버 | Python 3.13 · FastAPI (HTTP + WebSocket) · uv |
| DB | PostgreSQL 17 (docker compose) · SQLAlchemy · Alembic |
| 계정 | 회원가입 없는 익명 계정 (나중에 Steam 등 로그인 연결) |
| 통신 | 계정은 HTTP, 게임은 WebSocket — 둘 다 JSON |
| 그래픽 | 3D 맵 위에 2D 도트 캐릭터 · 소품, 고정 쿼터뷰 카메라 |
| 플랫폼 | Mac · Windows 데스크톱 우선, 웹(WebGL)은 나중에 |
| 배포 | AWS Lightsail (시드니) · Docker Compose · GitHub Actions 자동 배포, 클라이언트는 GitHub Releases |

## 시작하기

### 클라이언트
1. [Unity Hub](https://unity.com/download) 설치 (`brew install --cask unity-hub`)
2. Hub에서 에디터 `6000.6.6f1` 설치 — 모듈: Mac Build Support, Windows Build Support (Mono)
3. Hub → Projects → Add → `unity/` 폴더 선택 후 열기

처음 열 때 `Library/` 를 새로 만드느라 몇 분 걸린다.

### 서버
1. [uv](https://docs.astral.sh/uv/) · [Docker Desktop](https://www.docker.com/products/docker-desktop/) 설치 (`brew install uv`, `brew install --cask docker`)
2. DB 띄우기 (저장소 루트에서)
   ```bash
   docker compose up -d          # PostgreSQL (localhost:5432)
   ```
3. 서버 실행
   ```bash
   cd python
   uv sync
   uv run alembic upgrade head   # DB 테이블 만들기 · 갱신
   uv run uvicorn dg_village_server.main:app --reload --reload-include '*.json'   # http://localhost:8000, ws://localhost:8000/ws
   ```
4. 테스트: `uv run pytest` (Docker 없이 돌아간다)

### 여럿이 접속해 보기 (로컬)
1. DB + 서버 실행
2. Unity → File → Build Profiles → macOS → Build And Run (저장 위치 `unity/Builds/`)
3. 에디터에서도 `Assets/Scenes/CharacterSelect` 씬을 열고 ▶ Play
4. 각 창에서 캐릭터(◀ ▶)와 닉네임을 고르고 Enter → 마을에서 서로의 캐릭터가 보이고, Enter 로 채팅할 수 있다

처음 실행하면 회원가입 없이 익명 계정이 자동으로 만들어지고, 고른 캐릭터 · 닉네임은 서버에 저장된다. 에디터와 빌드는 서로 다른 계정이 된다. 마을 씬(`SampleScene`)을 바로 Play 해도 저장된 설정으로 들어간다.

## 로드맵

- [x] 기준 캐릭터 만들기 → Unity에서 캐릭터 이동 (2026-10-20, girl01 3방향 걷기)
- [x] 서버 연결 — 한 방에서 여러 명이 서로 움직이는 게 보이기 (2026-10-20)
- [x] 이름표 + 텍스트 채팅 + 말풍선, 한글 지원 (2026-10-20)
- [x] 캐릭터 선택 화면 + 캐릭터별 모습 동기화 (2026-10-20)
- [x] 익명 계정 + DB — 회원가입 없이 자동 로그인 (2026-10-20)
- [x] 채널 만들기 / 입장 (2026-10-20)
- [x] 서버 배포 + push 시 자동 배포 (2026-10-21)
- [x] 테스트용 클라이언트 배포 — GitHub Releases, macOS 공증 (2026-10-23, v0.1.0)
- [ ] 아트 입히기 — 진행 중: 마법사 길드 사무실, 야외 정원(쿼터뷰), 길드 건물, 조명, 앉기, 포탈, 맵 에디터
- [ ] Steam · 소셜 로그인 연결
- ~~음성 채팅~~ — 컨셉 변경으로 제외 (2026-10-22)

## 크레딧

- 한글 폰트: [Galmuri](https://github.com/quiple/galmuri) by Lee Minseo — SIL Open Font License 1.1 (`unity/Assets/_Project/Fonts/Galmuri-OFL.txt`)
