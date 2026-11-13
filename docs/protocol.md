# WebSocket 프로토콜

로비(캐릭터 목록·생성)는 REST, 게임 안은 WebSocket을 쓴다. REST 문서는 서버의 `/redoc`, WebSocket은 이 문서가 기준이다.

계정: `POST /signup` 또는 `POST /login` (body `{"username", "password"}`)이 `{"account_id"}`를 돌려준다. 이 값을 REST의 `X-Account-Id` 헤더와 `join`의 `account_id`로 쓴다. 아이디는 대소문자를 구분하지 않는다 (서버가 소문자로 저장). 서버는 아직 `account_id`가 실제 계정인지 확인하지 않는다.

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
   │── ride_move ─────────────────────────▶│  발판(배 등) 위 내 좌표 (나에겐 답 없음)
   │── chat ──────────────────────────────▶│  채팅 (저장 안 함)
   │◀────────────────────── chat_message ─│  나를 포함한 같은 씬 전원에게
   │                                       │
   │◀───────────────────── player_joined ─│  같은 씬의 다른 사람이
   │◀────────────────────── player_moved ─│  들어오고 / 움직이고 /
   │◀───────────────── player_ride_moved ─│  발판 위에서 움직이고 /
   │◀─────────────────────── player_left ─│  나갈 때
```

- 연결하자마자 첫 메시지로 `join`을 보낸다. 다른 메시지가 먼저 오면 `not_joined` 에러 후 연결을 끊는다. 10초 안에 오지 않으면 `join_timeout` 에러를 보내고 연결을 끊는다.
- `welcome` 전에 난 에러는 항상 연결 종료(close code **1008** Policy Violation)로 이어진다.
- `welcome` 뒤에 잘못된 메시지를 보내면 `error`만 오고 연결은 유지된다.
- 연결이 끊기면 서버는 그 캐릭터를 게임에서 빼고 같은 서버, 같은 씬 사람들에게 `player_left`를 보낸다. 같은 캐릭터로 다시 `join`하면 된다.
- 다른 플레이어 정보는 **같은 서버의 같은 씬** 사람끼리만 주고받는다. 이 문서에서 "내 씬"과 "같은 씬"은 모두 이 범위를 뜻한다. `join`에는 서버를 보내지 않는다: 서버는 `character_id`로 찾은 캐릭터의 `server_id`를 쓴다.
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

다른 맵 씬의 지정한 좌표로 이동한다. 서버는 바로 씬과 좌표를 `last_visited_scene`, `last_x`, `last_y`, `last_z`에 저장하고 `scene_changed`로 답한다. 클라이언트는 `scene_changed`를 받은 뒤 씬을 로드하고 그 좌표에 스폰한다. 게임에 처음 들어올 때는 보내지 않는다 (서버가 `join`에서 이미 저장된 씬과 좌표를 안다).

`change_scene`을 보낸 뒤에는 새 씬 로드가 끝날 때까지 `move`를 보내지 않는다. 늦게 도착한 `move`는 이전 씬의 좌표를 새 씬의 좌표로 덮어쓴다.

```json
{"type": "change_scene", "scene": "SquareScene", "x": 1.5, "y": 0.0, "z": -3.25}
```

| 필드 | 타입 | 설명 |
|---|---|---|
| `scene` | string | Unity 씬 이름. 서버의 `SceneName`(`schemas.py`)에 있는 씬만 허용, 아니면 `invalid_message` |
| `x`, `y`, `z` | float | 새 씬에서 시작할 Unity 월드 좌표 (m). `NaN`/`Infinity`는 `invalid_message` |

### `move`

내 캐릭터의 현재 좌표(현재 씬 기준 월드 좌표). 서버는 나에게 답하지 않고, 메모리에 저장하고 같은 씬 사람들에게 `player_moved`로 그대로 전달한다. DB에는 `move`마다 저장하지 않고, 연결이 끊길 때 마지막 좌표를 저장한다. 서버가 비정상 종료되면 마지막 저장 시점의 좌표로 돌아간다. 씬은 보내지 않는다: 서버가 `join`/`change_scene`으로 이미 안다. 움직이는 동안만 약 10Hz로 보낸다.

```json
{"type": "move", "x": 1.5, "y": 0.0, "z": -3.25}
```

| 필드 | 타입 | 설명 |
|---|---|---|
| `x`, `y`, `z` | float | Unity 월드 좌표 (m). `NaN`/`Infinity`는 `invalid_message` |

### `ride_move`

내가 발판(platform) 위에 서 있을 때 `move` 대신 보낸다. 발판은 스스로 움직이고 플레이어가 그 위를 걸어 다니는 것(배, 나중에 엘리베이터 등)이다. 좌표는 **발판 기준 로컬 좌표**다 (Unity `platform.transform.InverseTransformPoint(월드 좌표)`). 서버는 나에게 답하지 않고, 같은 씬 사람들에게 `player_ride_moved`로 그대로 전달한다. 설계 배경: [`design/platform-riding.md`](design/platform-riding.md).

- 발판에서 내린 뒤 보내는 첫 `move`가 "내렸다"는 뜻이다. 내리는 메시지는 따로 없다.
- 서버는 `ride_move`로 월드 좌표를 바꾸지 않는다. 발판 위에서 연결이 끊기면 DB에는 타기 직전의 마지막 `move` 좌표가 저장된다.
- `change_scene`을 하면 탑승 상태는 풀린다.

```json
{"type": "ride_move", "platform": "ferry_willow", "x": 1.2, "y": 0.0, "z": -3.0}
```

| 필드 | 타입 | 설명 |
|---|---|---|
| `platform` | string | 발판 id. 소문자·숫자·`_` 1~40자, 아니면 `invalid_message`. 형식은 맞지만 지금 씬에 없는 id면 `unknown_platform` |
| `x`, `y`, `z` | float | 발판 기준 로컬 좌표 (m). `NaN`/`Infinity`는 `invalid_message` |

발판 id 목록은 서버 `python/src/dg_village_server/platforms.py`의 `PLATFORMS`이고, 클라이언트의 발판 오브젝트에 같은 id를 적는다.

| 씬 | id | 무엇 |
|---|---|---|
| `SquareScene` | `ferry_willow` | 정원 선착장 ↔ Sacred isle 여객선 |

### `chat`

같은 씬 사람들에게 채팅을 보낸다. 서버는 채팅을 저장하지 않으므로, 씬에 들어오기 전에 오간 채팅은 볼 수 없다. 보낸 사람도 `chat_message`를 받으므로, 클라이언트는 보낸 순간 직접 표시하지 말고 `chat_message`를 받았을 때 표시한다.

```json
{"type": "chat", "text": "안녕하세요"}
```

| 필드 | 타입 | 설명 |
|---|---|---|
| `text` | string | 앞뒤 공백을 뺀 1~200자. 비어 있거나, 더 길거나, 줄바꿈 같은 제어 문자가 있으면 `invalid_message` |

이전 채팅을 보낸 뒤 0.5초가 지나지 않았으면 `chat_too_fast` 에러가 오고 그 채팅은 전달되지 않는다.

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
    "last_x": 1.5,
    "last_y": 0.0,
    "last_z": -3.25,
    "created_at": "2026-10-27T16:46:11.840108Z"
  }
}
```

`character`는 REST의 `CharacterOut`과 같은 모양이다. 클라이언트는 welcome을 받으면 `last_visited_scene` 씬을 로드하고 (`last_x`, `last_y`, `last_z`)에 스폰한다 (새 캐릭터는 `SquareScene`의 `(0, 0, 0)`).

### `scene_changed`

`change_scene` 성공. 서버 기준으로 플레이어는 이제 이 씬의 이 좌표에 있다. 클라이언트는 이 씬을 로드하고 이 좌표에 스폰한다.

```json
{"type": "scene_changed", "scene": "SquareScene", "x": 1.5, "y": 0.0, "z": -3.25}
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
| `x`, `y`, `z` | float | 마지막 `move` 좌표. 씬에 들어온 뒤 아직 안 움직였으면 시작 좌표 (`welcome`의 저장된 좌표 또는 `change_scene`의 좌표) |
| `riding` | object 또는 `null` | 발판 위에 있으면 `{"platform", "x", "y", "z"}` (마지막 `ride_move`의 로컬 좌표). 이때 `x`, `y`, `z`는 타기 전 좌표이므로 무시하고 `riding`으로 위치를 정한다. 땅 위면 `null` |

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

### `player_ride_moved`

내 씬의 다른 플레이어가 `ride_move`를 보냈다. 클라이언트는 그 id의 발판을 찾아 `platform.transform.TransformPoint(x, y, z)`로 월드 위치를 구한다. 모르는 발판 id면 무시한다. 다음에 그 플레이어의 `player_moved`가 오면 발판에서 내린 것이다.

```json
{"type": "player_ride_moved", "id": 8, "platform": "ferry_willow", "x": 1.2, "y": 0.0, "z": -3.0}
```

### `player_left`

다른 플레이어가 내 씬을 떠났다 (다른 씬으로 이동 또는 연결 종료). 그 캐릭터를 지운다.

```json
{"type": "player_left", "id": 8}
```

### `chat_message`

내 씬의 누군가가 `chat`을 보냈다. 내가 보낸 채팅도 온다. 닉네임은 서버가 채운다. 씬 로드 중에도 올 수 있다.

```json
{"type": "chat_message", "id": 8, "nickname": "민트", "text": "안녕하세요"}
```

| 필드 | 타입 | 설명 |
|---|---|---|
| `id` | int | 보낸 사람의 캐릭터 id |
| `nickname` | string | 보낸 사람의 닉네임 |
| `text` | string | 채팅 내용. 다른 사람이 쓴 글이므로 클라이언트는 Rich Text로 해석하지 않는다 |

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
| `chat_too_fast` | 이전 채팅 후 0.5초 안에 `chat`을 또 보냄 | 유지 |
| `unknown_platform` | `ride_move`의 `platform`이 지금 씬의 발판 id가 아님 | 유지 |
