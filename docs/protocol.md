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
   │◀──────────────────────────── welcome ─│  성공 → last_visited_scene 로드
   │◀─────────────────────── scene_players ─│  그 씬에 이미 있는 사람들
   │                 또는                    │
   │◀──────────── error + 연결 종료(1008) ─│  실패
   │                                       │
   │── change_scene ──────────────────────▶│  맵 이동 (DB에 바로 저장)
   │◀────────────────────── scene_changed ─│  이걸 받고 씬 로드
   │◀─────────────────────── scene_players ─│  새 씬에 이미 있는 사람들
   │                                       │
   │── move ──────────────────────────────▶│  내 좌표 (나에겐 답 없음)
   │                                       │
   │◀───────────────────── player_joined ─│  같은 씬의 다른 사람이
   │◀────────────────────── player_moved ─│  들어오고 / 움직이고 /
   │◀─────────────────────── player_left ─│  나갈 때
```

- 연결하자마자 첫 메시지로 `join`을 보낸다. 다른 메시지가 먼저 오면 `not_joined` 에러 후 연결을 끊는다. 10초 안에 오지 않으면 `join_timeout` 에러를 보내고 연결을 끊는다.
- `welcome` 전에 난 에러는 항상 연결 종료(close code **1008** Policy Violation)로 이어진다.
- `welcome` 뒤에 잘못된 메시지를 보내면 `error`만 오고 연결은 유지된다.
- 연결이 끊기면 서버는 그 캐릭터를 게임에서 빼고 같은 씬 사람들에게 `player_left`를 보낸다. 같은 캐릭터로 다시 `join`하면 된다.
- 다른 플레이어 정보는 **같은 씬** 사람끼리만 주고받는다.
- `scene_players`는 `welcome`/`scene_changed` 바로 뒤에 오므로 씬 로드가 끝나기 전에 도착한다. 클라이언트는 씬과 상관없이 살아 있는 곳(`GameSocket`)에 저장해 두고, 씬이 뜨면 그걸 읽어 캐릭터를 만든다. `player_*` 메시지도 씬 로드 중에 올 수 있다.

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

### `change_scene`

다른 맵 씬으로 이동한다. 서버는 바로 `last_visited_scene`에 저장하고 `scene_changed`로 답한다. 클라이언트는 `scene_changed`를 받은 뒤 씬을 로드한다. 게임에 처음 들어올 때는 보내지 않는다 (서버가 `join`에서 이미 `last_visited_scene`을 안다).

```json
{"type": "change_scene", "scene": "SquareScene"}
```

| 필드 | 타입 | 설명 |
|---|---|---|
| `scene` | string | Unity 씬 이름. 서버의 `SceneName`(`schemas.py`)에 있는 씬만 허용, 아니면 `invalid_message` |

### `move`

내 캐릭터의 현재 좌표(현재 씬 기준 월드 좌표). 서버는 나에게 답하지 않고, 메모리에만 저장하고 (DB 저장 안 함) 같은 씬 사람들에게 `player_moved`로 그대로 전달한다. 씬은 보내지 않는다: 서버가 `join`/`change_scene`으로 이미 안다. 움직이는 동안만 약 10Hz로 보낸다.

```json
{"type": "move", "x": 1.5, "y": 0.0, "z": -3.25}
```

| 필드 | 타입 | 설명 |
|---|---|---|
| `x`, `y`, `z` | float | Unity 월드 좌표 (m). `NaN`/`Infinity`는 `invalid_message` |

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

### `scene_changed`

`change_scene` 성공. 서버 기준으로 플레이어는 이제 이 씬에 있다.

```json
{"type": "scene_changed", "scene": "SquareScene"}
```

### `scene_players`

내가 씬에 들어갔을 때(`welcome`, `scene_changed` 바로 뒤) 그 씬에 이미 있는 다른 플레이어 목록. 나는 빠져 있고, 아무도 없으면 빈 배열.

```json
{
  "type": "scene_players",
  "players": [
    {"id": 7, "nickname": "라벤더", "sprite_set_id": "lavender_girl", "x": 1.0, "y": 0.0, "z": 2.0}
  ]
}
```

플레이어 객체(`PlayerOut`):

| 필드 | 타입 | 설명 |
|---|---|---|
| `id` | int | 캐릭터 id |
| `nickname` | string | 닉네임 |
| `sprite_set_id` | string | 클라이언트 `CharacterSpriteSet` id |
| `x`, `y`, `z` | float | 마지막 `move` 좌표. 씬에 들어온 뒤 아직 안 움직였으면 `(0, 0, 0)` (클라이언트 spawn 지점) |

### `player_joined`

다른 플레이어가 내 씬에 들어왔다 (`join` 또는 `change_scene`). `player`는 `scene_players`와 같은 플레이어 객체다.

```json
{"type": "player_joined", "player": {"id": 8, "nickname": "민트", "sprite_set_id": "lavender_girl", "x": 0.0, "y": 0.0, "z": 0.0}}
```

### `player_moved`

내 씬의 다른 플레이어가 `move`를 보냈다. 서버는 받은 즉시 그대로 전달하므로 움직이는 사람마다 약 10Hz로 온다. 클라이언트는 받은 좌표로 순간이동하지 말고 목표로 삼아 보간한다.

```json
{"type": "player_moved", "id": 8, "x": 3.0, "y": 0.0, "z": 4.0}
```

### `player_left`

다른 플레이어가 내 씬을 떠났다 (다른 씬으로 이동 또는 연결 종료). 그 캐릭터를 지운다.

```json
{"type": "player_left", "id": 8}
```

### `error`

```json
{"type": "error", "code": "character_not_found", "message": "No such character on this account"}
```

`message`는 로그·디버깅용이다. 플레이어에게 보여줄 문구는 클라이언트가 `code`를 보고 정한다.

| `code` | 언제 | 연결 |
|---|---|---|
| `invalid_message` | JSON이 아니거나, 모르는 `type`이거나, 필드가 빠짐 | `welcome` 전이면 종료 |
| `not_joined` | 첫 메시지가 `join`이 아님 | 종료 |
| `join_timeout` | 연결 후 10초 안에 `join`이 없음 | 종료 |
| `character_not_found` | 없는 캐릭터이거나 다른 계정의 캐릭터 | 종료 |
| `already_connected` | 그 캐릭터가 이미 다른 연결로 게임 안에 있음 | 종료 |
| `already_joined` | `welcome`을 받은 연결에서 `join`을 또 보냄 | 유지 |
