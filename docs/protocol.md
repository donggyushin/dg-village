# WebSocket 프로토콜

로비(캐릭터 목록·생성)는 REST, 게임 안은 WebSocket을 쓴다. REST 문서는 서버의 `/redoc`, WebSocket은 이 문서가 기준이다.

- 메시지 모델 코드: `python/src/dg_village_server/protocol.py` (Pydantic). 메시지를 바꾸면 이 문서도 같이 고친다.
- 주소: `ws://127.0.0.1:8000/ws` (로컬), `wss://13-54-176-157.sslip.io/ws` (운영)
- 모든 메시지는 JSON 텍스트 프레임이고 `type` 필드로 종류를 구분한다.

## 흐름

```
클라이언트                                서버
   │── WebSocket 연결 (/ws) ──────────────▶│
   │── join ──────────────────────────────▶│  10초 안에 보내야 함
   │◀──────────────────────────── welcome ─│  성공 → SquareScene으로 이동
   │                 또는                    │
   │◀──────────── error + 연결 종료(1008) ─│  실패
```

- 연결하자마자 첫 메시지로 `join`을 보낸다. 10초 안에 오지 않으면 `join_timeout` 에러를 보내고 연결을 끊는다.
- `welcome` 전에 난 에러는 항상 연결 종료(close code **1008** Policy Violation)로 이어진다.
- `welcome` 뒤에 잘못된 메시지를 보내면 `error`만 오고 연결은 유지된다.
- 연결이 끊기면 서버는 그 캐릭터를 게임에서 뺀다. 같은 캐릭터로 다시 `join`하면 된다.

## 클라이언트 → 서버

### `join`

내 캐릭터 하나로 게임에 들어간다.

```json
{"type": "join", "account_id": "86be7e4a-7651-4ac7-94c6-5fbacd099ecb", "character_id": 3}
```

| 필드 | 타입 | 설명 |
|---|---|---|
| `account_id` | UUID 문자열 | REST의 `X-Account-Id` 헤더와 같은 값 (`Session.AccountId`) |
| `character_id` | int | 내 캐릭터 id (`GET /servers/{id}/characters` 결과의 `id`) |

## 서버 → 클라이언트

### `welcome`

`join` 성공. 이 연결로 게임이 시작된다. 이후 `character.id`가 플레이어 id다.

```json
{
  "type": "welcome",
  "character": {
    "id": 3,
    "server_id": 1,
    "nickname": "Developer",
    "sprite_set_id": "lavender_girl",
    "last_visited_scene": "SquareScene",
    "created_at": "2026-10-27T16:46:11.840108Z"
  }
}
```

`character`는 REST의 `CharacterOut`과 같은 모양이다. 클라이언트는 welcome을 받으면 `last_visited_scene` 씬을 로드한다 (새 캐릭터는 `SquareScene`).

### `error`

```json
{"type": "error", "code": "character_not_found", "message": "No such character on this account"}
```

`message`는 로그·디버깅용이다. 플레이어에게 보여줄 문구는 클라이언트가 `code`를 보고 정한다.

| `code` | 언제 | 연결 |
|---|---|---|
| `invalid_message` | JSON이 아니거나, 모르는 `type`이거나, 필드가 빠짐 | `welcome` 전이면 종료 |
| `join_timeout` | 연결 후 10초 안에 `join`이 없음 | 종료 |
| `character_not_found` | 없는 캐릭터이거나 다른 계정의 캐릭터 | 종료 |
| `already_connected` | 그 캐릭터가 이미 다른 연결로 게임 안에 있음 | 종료 |
| `already_joined` | `welcome`을 받은 연결에서 `join`을 또 보냄 | 유지 |
