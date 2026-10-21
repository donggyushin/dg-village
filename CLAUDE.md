# dg-village

Gather.town-style social space game: users create channels, walk around as characters, and chat (text first, voice later). Differentiator is the art — cute 2.5D pixel style inspired by old Korean RPGs (Ragnarok, Tales Weaver, To the Moon).

Solo hobby project (weekends + spare time). Owner is strong on Python/server, new to Unity and art — **explain Unity concepts when introducing them**.

## Game concept (owner, 2026-10-21)

"A game you keep open while working" — like chatting on KakaoTalk with friends during work, but gamified. Players are **not** coworkers; they just work at the same time. First map = an **office** with three zone types:

1. **Desk (개인 자리)** — sitting at your desk = focus mode: you can't hear/see chat (or later voice) from others.
2. **Hallway (복도)** — everything else; proximity chat like Gather (only players within a set distance hear you).
3. **Lounge (휴게 공간)** — everyone inside the same lounge hears each other regardless of distance.

Theme: **not** a modern office — fantasy (medieval European / Harry Potter-like), e.g. a wizard guild hall / magic academy study.

## Communication

- Always respond in Korean. Keep technical terms and code identifiers in original form.

## Layout

```
dg-village/
├── unity/    # Game client (Unity 6, URP, Universal 3D template)
├── python/   # WebSocket server (FastAPI, uv) + Dockerfile
├── deploy/   # Production docker compose (Caddy + server + Postgres), README.md = runbook
├── art/      # Art sources outside Unity (palettes/, tilesets/, props/ + Python generators → Unity)
└── docs/     # Research reports (docs/research/)
```

Single git repo at the root, one `.gitignore` covering both.

## Stack & decisions

| Area | Decision |
|---|---|
| Client | Unity `6000.6.6f1` (Apple Silicon), URP 17, new Input System, NativeWebSocket 2.0.7 (`upm-2` branch pinned by commit hash in `manifest.json`) |
| Server | Python 3.13 — FastAPI WebSocket + uvicorn, pydantic schemas, managed with `uv`. Hand-rolled; **no** Netcode for GameObjects / Mirror |
| DB | Postgres 17 (docker compose, `compose.yaml` at repo root) + SQLAlchemy 2 async (asyncpg) + Alembic. Tests use in-memory SQLite |
| Auth | Self-issued: anonymous device credential → JWT access token (HS256, 24h). Steam is the intended later provider |
| Protocol | HTTP JSON for accounts (`/auth/*`, `/me`); JSON over WebSocket for the game (rooms, position broadcast ~10Hz, chat) |
| Graphics | 2.5D: 3D map + 2D pixel sprite characters, **fixed camera** |
| Platforms | Desktop first (Mac + Windows). WebGL later |
| Voice | Later, via LiveKit or Vivox — never implement voice ourselves |
| Assets | AI-generated (PixelLab for characters, Meshy/Tripo for map) + Aseprite touch-up |

**WebGL compatibility constraint**: web build is a future target, so pick client libraries that work in WebGL from day one (e.g. NativeWebSocket, not `System.Net.WebSockets`). No threads in client networking code.

## 2.5D pixel sprite rules

- Camera: fixed angle, Orthographic.
- Sprite textures: Filter Mode **Point**, Compression **None**, consistent Pixels Per Unit.
- Directional sprites (switch frame by movement direction) instead of rotating the sprite.
- Use Alpha Clip (cutout) materials for sprites in the 3D scene to avoid transparency sorting issues.

## Roadmap

1. ~~Make a reference character → get it moving in Unity.~~ **Done 2026-10-20** (girl01, 3-direction walk).
2. ~~Server + multiple clients seeing each other move in one room.~~ **Done 2026-10-20** (verified with macOS build + editor).
3. ~~Name tags → text chat + speech bubbles.~~ **Done 2026-10-20** (Korean via Galmuri fallback font).
4. ~~Player character selection.~~ **Done 2026-10-20** (CharacterSelect scene + `characterId` sync). Characters: `girl01` (default), `boy01` (black hoodie, green cargo pants, laptop).
5. ~~Accounts + DB (phase 1: anonymous accounts).~~ **Done 2026-10-20** (verified with build + editor as two accounts).
6. ~~Create/join channels.~~ **Done 2026-10-20** (ChannelSelect scene, `channels` table, leave button in the village).
7. **In progress**: Deploy for external testing. Server is **live** at `https://13-54-176-157.sslip.io` (2026-10-21). ServerSettings `ProductionUrl` points there (builds use it, editor stays local). Auto-deploy on push via GitHub Actions **done 2026-10-21**. Remaining: build clients for testers.
8. Social login (Google/Discord OAuth).
9. Art pass + office concept. **In progress**: online village = wizard-guild office room (desks / hallway / lounge), walls, furniture collision, chat zones, sitting + desk focus mode — all live 2026-10-21 (b975e6a). See **Map art direction** and **Maps & chat zones**.
   Portals + outdoor garden map **done 2026-10-21** (office south exit ↔ `outdoor`, the guild-hall front garden; rooms per channel+map). Furniture swapped to front-view sprites (sitting reads right). Runtime maps **done 2026-10-21**: one village scene, the server sends the map JSON in `welcome`, `travel` switches maps on the same socket, props come from the bundled `PropCatalog` (new art later via PNG download or Addressables).
   Planned: (c) maybe per-channel/user-made maps (big, owner still deciding). Small TODOs: ship a new Windows build to testers (their build still shows the old plaza), "N people hear you" indicator, wall end thickness / wall decor, trees at 128px for outdoor maps.
10. Voice.

Accounts (owner request 2026-10-20; "no signup screen like Steam games, but extensible"):
- Phase 1 (**done**) = anonymous accounts. First launch: `POST /auth/anonymous` creates a user + a random device credential; the client keeps the credential in PlayerPrefs (`dgvillage.anonymousCredential`) and logs in with it afterwards. Losing it = losing the account (until a provider is linked).
- Schema: `users` (profile only: `id` uuid, `display_name`, `character_id`, timestamps) + `auth_identities` (`provider`, `subject` unique pair → `user_id`). Anonymous subject = SHA-256 of the credential (raw credential never stored).
- Adding a login method (Steam later, maybe Discord): implement an `AuthProvider` in `auth.py` (proof → stable subject), register it in `PROVIDERS`, add a "link" endpoint that attaches a new identity to the current user. `POST /auth/login {provider, credential}` and the game server stay unchanged — the WebSocket only checks the JWT.
- Player id in rooms = account id (hex). `name` is display-only (not unique). Same account joining twice → the older session is kicked (`error` "logged in from another session", close 4000).
- Editor and standalone builds have separate PlayerPrefs → separate accounts (handy for 2-client tests). Two copies of the same build share one account and kick each other.

Character system: each character is a `CharacterDefinition` asset (`Assets/_Project/Characters/{id}.asset`, id = sprite folder name) listed in `CharacterRegistry.asset` (first entry = default/fallback). The server only syncs `characterId`; each client resolves it via the registry. The chosen character/nickname live in the account profile on the server (`PATCH /me`), so they persist across launches and scenes.

## Current state (as of 2026-10-20)

Scenes (Build Profiles → Scene List, order matters): `0 Assets/Scenes/Boot.unity` (loading screen: illustration while logging in, ≥2.5s, then fades into CharacterSelect) → `1 Assets/Scenes/CharacterSelect.unity` (start screen) → `2 Assets/Scenes/ChannelSelect.unity` (channel list) → `3 Assets/Scenes/SampleScene.unity` (village — **every map**: the room is laid at runtime from the server's `welcome`; the `Map` root saved in the scene is only an editor preview of `office`). Playing CharacterSelect, ChannelSelect or SampleScene directly still works — they log in themselves; SampleScene without a picked channel joins the plaza.

`ChannelSelect.unity` (built by **DgVillage → Create Channel Select Scene**): Main Camera, EventSystem, `ChannelCanvas` + `ChannelSelect` (Title, BackButton, RefreshButton, `ChannelList` ScrollView whose Content holds the inactive `ChannelItemTemplate` (`ChannelListItem`), NameInput, CreateButton, StatusText).

`CharacterSelect.unity` (built by **DgVillage → Create Character Select Scene**): Main Camera (solid bg), EventSystem, `SelectCanvas` + `CharacterSelect` (Title, Preview, Previous/Next, CharacterName, NameInput, EnterButton, StatusText).

Scene `Assets/Scenes/SampleScene.unity`:

```
Main Camera   Orthographic, Size 5.625, pos (0, 8, -11.5), rot X 35  + CameraFollow(target = Player)
Map           editor preview of maps/office.json (Floor/Props/Seats/Portals) ← DgVillage → Set Up Village Scene; replaced at runtime by MapLoader
Player        empty root, movement only               + PlayerMovement
└── Visual    SpriteRenderer, rot X 35 (faces camera) + DirectionalSpriteAnimator(character = girl01, replaced at runtime)
Network       empty at origin                         + NetworkClient(localPlayer = Player, characters = registry) + RemotePlayerSpawner(prefab = RemotePlayer, characters = registry) + MapLoader + SeatInteraction + PortalTraveler
ChatCanvas    Screen Space Overlay, 1280x720 scaler   + ChatBubbleSpawner(prefab = ChatBubble, root = Bubbles) + ChannelHud
├── Bubbles   full-stretch container (first child → drawn under the input)
├── ChatInput TMP_InputField, bottom center           + ChatInput
├── LeaveButton  top-left "채널 나가기" (hand-placed)
└── ChannelName  top-left TMP text, right of the button
EventSystem   + InputSystemUIInputModule
```

`Player/Visual/NameTag` and `RemotePlayer/Visual/NameTag`: world-space TextMeshPro + `NameTag`.

Prefabs: `RemotePlayer` (copy of Player with `RemotePlayer` instead of `PlayerMovement`, spawned under `Network`), `ChatBubble` (Image + TMP text, built by the Create Chat UI menu).

Scripts (`Assets/_Project/Scripts/`, namespace `DgVillage`):

| File | Role |
|---|---|
| `PlayerMovement` | XZ movement from Input System `Player/Move` (project-wide actions); `SitAt`/`StandUp` (seated = no movement, move keys set `WantsToStand`); stays on `FloorMesh` tiles and out of `Obstacle`s per axis (slides along walls; no floor in scene = free). Exposes `Facing` (Down/Up/Left/Right), `IsMoving`, `InputBlocked` (set by chat) and `WaitingForServer` (set by `MapLoader` until the first `welcome`, so you can't wander the preview room and get snapped back) |
| `CameraFollow` | Keeps the scene-authored offset to target (taken in Awake), SmoothDamp in LateUpdate; first frame snaps (portal spawn) |
| `ICharacterMotion` | `Facing` + `IsMoving` + `IsSeated`. Implemented by `PlayerMovement` (local input) and `RemotePlayer` (server data) |
| `DirectionalSpriteAnimator` | Plays idle/walk sprites of its `CharacterDefinition` from parent `ICharacterMotion`. Left = Right with `flipX`. While seated shows the character's `SitDown` sprite (front, feet on the floor in front of the seat, `_sitSpriteLift` 0 — behind a desk only the head shows, which the owner prefers over lifting); characters without one fall back to the down idle lifted `_seatedLift` 12px. (A legs-cropping fallback was tried 2026-10-22 and rejected as awkward.) `SetCharacter()` swaps at runtime |
| `CharacterDefinition` | ScriptableObject: `Id`, `DisplayName`, per-direction `Idle` + `Walk[]`, `Get(Facing)`, `Preview` (down idle), optional `SitDown` |
| `CharacterRegistry` | ScriptableObject list of definitions; `Find(id)` falls back to first entry |
| `UI/LoadingScreen` | Boot scene (on `LoadingCanvas`, sortingOrder 100 + CanvasGroup): `AccountClient.LoginAsync`, waits until ≥`_minimumSeconds` (2.5) since shown, then `DontDestroyOnLoad` → `LoadSceneAsync(CharacterSelect)` → smoothstep alpha fade (`_fadeSeconds` 0.8) → destroys itself. Failure → message, any key/click retries. Illustration = `Sprites/UI/loading_guild_study.png` (PixelLab Create image Pro, 344×192; source `art/illustrations/`) |
| `UI/VillageLoadingScreen` | Village scene `VillageLoadingCanvas` (same illustration/look as Boot, built by the village menu via `BootSceneBuilder.CreateLoadingCanvas`): covers the scene until the first `welcome` (≥`_minimumSeconds` 1), then fades (`_fadeSeconds` 0.6) and deactivates — hides the preview room being swapped for the server map. Disconnected before entering → message, any key/click → ChannelSelect. Portal `welcome`s are ignored |
| `UI/CharacterSelect` | Select scene: on Start uses the existing login (Boot) or `AccountClient.LoginAsync` (status text while busy; failure → Enter retries), prefills from profile; ◀▶ / ←→ / A·D cycle, animated down-walk preview, nickname (≤16, empty = keep current); Enter → `PATCH /me` → `LoadScene(ChannelSelect)` |
| `UI/ChannelSelect` | Channel list scene: `GET /channels` → one `ChannelListItem` per channel (Instantiate of the inactive template into the ScrollView Content); click → `ChannelClient.Select` → village. Name input + Create (Enter) → `POST /channels` → join it (409 → "3개까지"). Refresh button; Back / Esc → CharacterSelect |
| `UI/ChannelListItem` | One list row: Button + Name + PlayerCount texts, `Bind(channel, onClick)` |
| `UI/ChannelHud` | Village: shows current channel name, Leave button → `LoadScene(ChannelSelect)` (NetworkClient's OnDestroy closes the socket → server sends `player_left`) |
| `Network/ServerConfig` | Static. Resolves the server URL once per Play: `-server <url>` command-line arg > `ServerSettings` (editor → `EditorUsesProduction`, builds → `BuildsUseProduction`) > `http://localhost:8000`. `WebSocketUrl` derived (http→ws, https→wss, + `/ws`) |
| `Network/ServerSettings` | ScriptableObject at `Assets/_Project/Resources/ServerSettings.asset` (loaded via `Resources.Load`): `LocalUrl`, `ProductionUrl` (empty until the VPS exists), two "use production" toggles. Defaults: editor = local, builds = production |
| `Network/AccountClient` | Static. `LoginAsync` (stored credential → `/auth/login`, else/401 → `/auth/anonymous`; then `GET /me`), `UpdateProfileAsync`, `AccessToken`, `Profile`, `NormalizeDisplayName`/`NormalizeName`. `internal SendAsync` = shared HTTP helper (adds the token). UnityWebRequest + `Awaitable` (WebGL-safe). Throws `AccountException` (Status 0 = unreachable) |
| `Network/MapLoader` | On `Network`. On every `welcome` with a map: disables + destroys the current `Map` root (scene preview or previous map), `MapBuilder.Build`s the new one, moves the local player to its server position (portal spawn) and `CameraFollow.Snap()`s. Freezes the local player (`WaitingForServer`) from OnEnable until the first `welcome`. `CurrentMapId` |
| `Map/MapBuilder` | Static, runtime + editor. `GameMapData` + `PropCatalog` → root `Map` with `Floor` (FloorMesh), `Props` (upright sprites, blob shadows, `Obstacle` from the catalog footprint), `Seats`, `Portals`. `CreateSprite` helper (35° tilt) |
| `Map/MapLighting` | `[ExecuteAlways]`, added by `MapBuilder` under `Map/Lighting` from the map JSON `lighting` + `lights` (all zero/"" = no effect): point lights (≤16, `_DgLights`/`_DgLightColors` global arrays, Perlin flicker each frame) over `ambient`, additive glow sprites at flame height (generated stepped+dithered texture, `Resources/Glow.mat`), sun-shadow/cloud globals, static `SunShadowOffset`, and a runtime global URP `Volume` (ColorAdjustments, WhiteBalance, Vignette). Volume/glows live in DontSave children. Resets globals in OnDisable, destroys in OnDestroy (can't destroy while being deactivated or in OnValidate) |
| `Map/SunShadow` | On prop sprites (`MapBuilder`) and character `Visual`s (added in `DirectionalSpriteAnimator.Awake`): draws the current sprite's silhouette flat on the ground, sheared by `MapLighting.SunShadowOffset` × height (rows stay horizontal = no rotated pixels), DontSave sibling mesh with `Resources/SunShadow.mat`; hidden when the offset is zero (indoor) |
| `Map/PropCatalog` | ScriptableObject `Assets/_Project/Maps/PropCatalog.asset`: prop id (= sprite file name) → sprite + footprint, plus floor/wall/sprite materials and the shadow sprite. Generated — **DgVillage → Update Prop Catalog** (also run by the village menu). Server maps can only use props in the build's catalog (unknown → warning, skipped) |
| `Map/Portal` | Portal rectangle from the map JSON (`_to`, `_size`, `_spawn`); static registry, `Portal.At(pos)`. Cyan gizmo |
| `PortalTraveler` | On `Network` (added by the village map menu). Local player's feet inside a `Portal` → `NetworkClient.SendTravel(to)` once per entry (reset on `welcome` or leaving the portal) |
| `Network/ChannelClient` | Static. `ListAsync`, `CreateAsync`, `Select`/`Current` (survives scene loads), `CurrentId` (falls back to `DefaultChannelId` = plaza). Constants mirror the server (name ≤24, 3 per user) |
| `Network/NetworkMessages` | JsonUtility DTOs mirroring `protocol.py` and `api.py` (camelCase public fields, exception to naming rule) + `FacingWire`. JsonUtility sends null strings as `""`, so `ProfileUpdate` always fills both fields |
| `Network/NetworkClient` | `SendSit`/`SendStand`, `LocalFocused`, events `LocalSeatChanged` (own seat/focus changed in a snapshot) and `ServerError`. On Start: logs in if not yet (direct Play of SampleScene), applies profile character (via registry) + name to the local player, then NativeWebSocket connect → `join {room = ChannelClient.CurrentId, token}` → sends `move` at 10Hz only when changed (paused between `SendTravel` — which first sends the current position — and the next `welcome`/`error`); `SendChat`; raises C# events per server message. Sets `runInBackground` |
| `Network/RemotePlayerSpawner` | Subscribes to `NetworkClient` events; Instantiate/ApplyState/Destroy `RemotePlayer`s, skips own id, resolves `characterId` via registry |
| `Network/RemotePlayer` | `Init(state, character)`; exponential lerp toward server position, snaps if > 5 units |
| `NameTag` | World-space TMP under `Visual` (inherits camera tilt). `Reset()` sets default look; rich text off. `SetFocused(true)` → "name · 집중" |
| `SeatInteraction` | On `Network` (added by the village map menu). Near a `Seat` (≤1.2): E → `sit`; seated: E or a move key → `stand`. Seats/unseats the player only when the server's state says so (`NetworkClient.LocalSeatChanged`); bottom hint text `ChatCanvas/SeatHint` ("E 앉기", focus notice, seat errors) |
| `Seat` | Seat marker from the map JSON (`_id` = server seat id, `_kind` desk/lounge); `Seat.Nearest(pos, range)` |
| `Chat/ChatInput` | Enter opens/sends (not while `NetworkClient.LocalFocused`), Esc cancels; visible only while typing (CanvasGroup alpha, not SetActive — Update must keep running); sets `PlayerMovement.InputBlocked` while focused |
| `Chat/ChatBubble` | Screen-space bubble sized via `GetPreferredValues`, follows anchor (name tag) each LateUpdate, self-destroys after 3–8s |
| `Chat/ChatBubbleSpawner` | `NetworkClient.ChatReceived` → bubble over speaker (own bubble comes from the server echo) |
| `Editor/ChatUIBuilder` | Menu **DgVillage → Create Chat UI**: builds/wires canvas, input, EventSystem, ChatBubble prefab |
| `Editor/PixelSpriteImporter` | Auto pixel import settings for `Sprites/` (Point, no compression, 32 PPU). `Sprites/Characters/` gets foot pivot (0.5, 10/64). **Only applies on first import** (`importSettingsMissing`) |
| `Editor/CharacterDefinitionBuilder` | Project window: right-click a character sprite folder → **DgVillage → Create Character Definition** — creates/updates `Characters/{folder}.asset` by filename convention and registers it |
| `Editor/BootSceneBuilder` | Menu **DgVillage → Create Boot Scene**: builds/wires/saves `Boot.unity` (illustration with `AspectRatioFitter` EnvelopeParent = fills the screen, bottom shade, status text) and puts it first in the Scene List (saved immediately). `Build()` = no dialogs (Unity MCP) |
| `Editor/CharacterSelectSceneBuilder` | Menu **DgVillage → Create Character Select Scene**: builds/wires/saves `CharacterSelect.unity` (overwrites after confirm) |
| `Editor/ChannelSelectSceneBuilder` | Menu **DgVillage → Create Channel Select Scene**: same for `ChannelSelect.unity` (ScrollView + VerticalLayoutGroup + ContentSizeFitter list) |
| `Editor/SceneUIBuilder` | Shared helpers for the scene builders: camera + EventSystem, 1280x720 canvas, default UI sprites, `Place`/`CreateText`/`CreateButton` |
| `Editor/ServerSettingsMenu` | Menu **DgVillage → Server Settings** (selects/creates the asset). `IPreprocessBuildWithReport`: fails the build if builds use production but `ProductionUrl` is empty — uncheck `BuildsUseProduction` for local-only test builds |
| `GroundMesh` | Builds the ground mesh from a corner-grid text `Layout` ('#' = stone, else grass) using the 4×4 Wang atlas (index = NE·1+SE·2+SW·4+NW·8); 1 tile = 1 unit. `[ExecuteAlways]`, mesh not saved (rebuilt on enable/edit) |
| `FloorMesh` | Floor + walls: one char per tile in `Layout` (`s` stone = hallway, `w` wood = desks, `c` carpet = lounge, `g` grass, `p` garden path, else no floor; outdoor north wall = the guild hall facade); floor atlas row = material, column = edge mask N·1+E·2+S·4+W·8 (neighbor differs → trim). Open edges get walls (submesh 1, 2nd material): north = 3-unit wall tilted 35° like sprites (upright on screen, 1:1 texels), other sides = 0.5-unit wall-top strip. `FloorMesh.Current.IsWalkable(pos, radius)` = on floor tiles. `[ExecuteAlways]`, mesh not saved |
| `GameMapData` | JsonUtility DTO of the shared map JSON (`layout`, `hearingRadius`, `seats`, `portals`, `props`; camelCase fields) |
| `Obstacle` | Furniture footprint (`_size` = width × depth) from the pivot (front edge) backwards (+Z); static registry, `Obstacle.Blocks(pos, radius)`. Green gizmo when selected |
| `Editor/OfficeRoomBuilder` | Editor map helpers: loads `maps/{id}.json` from the server package, `UpdateCatalog()` (menu **DgVillage → Update Prop Catalog**: every sprite in `Sprites/Environment/Props` + the footprint table here + materials, created if missing), `CreateRoom(map)` = `MapBuilder.Build` with the catalog |
| `Editor/VillageMapBuilder` | Menu **DgVillage → Set Up Village Scene**: updates the catalog, replaces the `Map` preview in `SampleScene` with `office` (also deletes legacy `Ground`/`Floor`/`Props`/`Seats`/`Portals`), camera → dark solid background, adds/wires `MapLoader` + `SeatInteraction` + `PortalTraveler` on `Network` and `ChatCanvas/SeatHint`, recreates `VillageLoadingCanvas`, saves. Re-run after adding prop sprites (layout-only map edits need no client change). `ApplyToVillage()` = no dialogs (Unity MCP) |
| `Editor/MapTestSceneBuilder` | Menu **DgVillage → Create Map Test Scene**: `Assets/Scenes/MapTest.unity` (no network) — wizard-guild office room 19×12 (desks · hallway · lounge), girl01 player, boy01 behind a desk, furniture with blob shadows (`sortingOrder -1`) + `Obstacle` footprints (table in the builder), `Materials/Floor_Office.mat` + `Wall_Office.mat` (URP Unlit, created if missing). Floor layout + props come from the server's `maps/office.json`. `MapTestSceneBuilder.Rebuild()` = same without dialogs (for Unity MCP `execute_code`). The earlier plaza version is in git history (54b3e82) |
| `Editor/DynamicFontCleaner` | Clears dynamic TMP font atlases in `Fonts/` on returning to Edit mode (also menu **DgVillage → Clear Dynamic Font Data**) |

## Server (`python/`)

- Run: `docker compose up -d` (repo root, Postgres) → `cd python && uv sync && uv run alembic upgrade head` → `uv run uvicorn dg_village_server.main:app --reload` (port 8000, WS at `/ws`, `GET /health`). Tests: `uv run pytest` (no Docker needed), lint: `uv run ruff check .`
- Env: `DG_DATABASE_URL` (default = compose Postgres), `DG_JWT_SECRET` (dev default logs a warning — **must** be set when deployed).
- `protocol.py` (pydantic WS schemas, camelCase on the wire, `normalize_display_name`), `room.py` (in-memory `RoomManager`/`Room`, 10Hz tick broadcasts `state` only for rooms that changed; one session per player id; chat goes to `Room.hearers()`), `maps.py` (`GameMap`/`load_map`: zones + who hears whom, see **Maps & chat zones**), `config.py` (`Settings.from_env`), `db.py` (models + engine), `auth.py` (`AuthProvider`s, JWT), `api.py` (HTTP routes), `main.py` (app factory `create_app(settings, create_tables=)` + `/ws` handler)
- HTTP: `POST /auth/anonymous` → `{userId, credential, accessToken}`; `POST /auth/login {provider, credential}` → `{userId, accessToken}`; `GET /me`, `PATCH /me {displayName?, characterId?}` with `Authorization: Bearer`. Errors are FastAPI `{detail}`.
- Channels: `channels` table (`id` uuid, `name` ≤24, `owner_id` → users, NULL = system, `ON DELETE SET NULL`). The plaza "광장" has the fixed id `00000000-0000-0000-0000-000000000001` and is created at startup (`ensure_default_channel`) if missing. `GET /channels` → `{channels: [{id (hex), name, ownerId, playerCount}]}` (wrapped — JsonUtility can't parse top-level arrays; plaza first, then busiest; counts come from `RoomManager`). `POST /channels {name}` → 201, max `MAX_CHANNELS_PER_USER` = 3 per owner (409). Empty channels are kept (MVP). No delete/rename/private channels yet.
- **Maps & chat zones**: `src/dg_village_server/maps/<id>.json` is the single source of truth for a map — `layout` (one char per tile, same as Unity `FloorMesh`: `s` hallway stone, `w` desk-area wood, `c` lounge carpet; centered on the origin, first row = north), `hearingRadius`, `seats` (`desk`/`lounge`), `props` (sprite, x, z, shadow). Server: connected carpet area = one lounge (everyone inside hears each other), elsewhere = hallway (only within `hearingRadius`), lounge ↔ hallway never hear each other; speaker always gets the echo. Every channel has every map in `maps.MAP_IDS` (`office` first = default, `outdoor`); `RoomManager(load_maps())` keeps one room per `"<channel>/<map>"`, `player_count(channel)` sums the maps. `portals` (`{x, z, w, d, to, spawnX, spawnZ}`): the client sends `travel {to}` when its player steps in; the server checks the player's last position is inside a portal to `to` (± `PORTAL_SLACK` 1.0), stands them up, moves them to the other room at the spawn point (`player_left` / `player_joined` to each side) and answers with a new `welcome`; else non-fatal `error` "not at a portal". The JSON is sent to clients as-is in `welcome.map`, so **layout/prop placement changes ship with a server deploy only** (new prop art still needs a client build). Map id missing from `RoomManager.maps` = whole room hears everything (tests). Clients older than the office map (2026-10-21) still show the 20×20 plaza, so their chat looks random. Unity reads the same JSON (`GameMapData` DTO) at runtime; editor menus use the local file only for previews / MapTest.
- Migrations: change `db.py` → `uv run alembic revision --autogenerate -m "..."` → review the file in `migrations/versions/` → `uv run alembic upgrade head`. Generated files are auto-formatted by ruff (post-write hook). `uv run alembic check` = models vs DB drift.
- Protocol: client sends `join {room, token, map?, x?, z?}` first (`room` = channel id hex; unknown → `error` "unknown channel" + close 1008; `map` defaults to office, unknown → "unknown map"; `x`/`z` = start position) (name/character come from the account), then `move {x, z, facing, moving}` (`facing` = `down|up|left|right`), `chat {text}` (whitespace collapsed, ≤200 chars, 0.5s rate limit), `sit {seat}` (seat id from the map JSON; must be within `SIT_RANGE` 1.5 and free → snapped onto the seat facing down; else non-fatal `error` "unknown seat" / "seat taken" / "too far from the seat") and `stand` (also implied by a `move` > 0.05 away from the seat). `PlayerState` carries `seat` (id or null) and `focused` (sitting at a `desk` seat = focus mode: hears no chat and isn't heard; speaker still gets the echo). `travel {to}` (see Maps). Server sends `welcome {id, players, map}` (on join and after each travel), `player_joined`, `player_left`, `state {players}`, `chat {id, name, text}` (to whole room incl. sender), `error`.
- Rooms/positions are in memory; accounts and channels are in Postgres. Reset local DB: `docker compose down -v` (clients then silently create new anonymous accounts).
- Deploy: AWS Lightsail instance `dg-village-origin` (Ubuntu 24.04, 2 GB, $12/mo) in **Sydney** `ap-southeast-2` — the AWS free plan ("New Frontiers" org, $120 credits until **2027-04-20**) is locked to Sydney by an SCP; unlocking other regions requires upgrading to a paid plan and deletes spend limits, so we stayed. Static IP `13.54.176.157` (`dg-village-ip`), firewall 22/80/443. SSH: `ssh -i ~/.ssh/dg-village ubuntu@13.54.176.157`; repo cloned at `~/dg-village` (public GitHub repo), `deploy/.env` generated on the server (secrets never left it). **Auto-deploy**: push to main touching `python/**`/`deploy/**` → `.github/workflows/deploy.yml` (ruff + pytest, then SSH with deploy-only key `~/.ssh/dg-village-deploy`, forced to `deploy/deploy.sh` via `restrict,command=` in `authorized_keys`; Secrets `DEPLOY_SSH_KEY`/`DEPLOY_HOST`/`DEPLOY_KNOWN_HOSTS`). `deploy.sh` = `git pull --ff-only` → `docker compose up -d --build --wait` → image prune; manual update = run it on the server. Never edit the server's working tree (except `deploy/.env`) or ff-only pull breaks. **Backups**: `deploy/backup.sh` (pg_dump -Fc → `~/backups/` on the VPS, 14 days) via ubuntu's crontab at 18:00 UTC (server TZ = UTC) + Lightsail automatic snapshots (daily 21:00 KST, 7 kept); restore steps in `deploy/README.md`. `deploy/compose.yaml` (project `dg-village-prod`): Caddy publishes 80/443 and reverse-proxies (WS included) to `server:8000`; server and Postgres are internal only. `python/Dockerfile` runs `alembic upgrade head` then a **single** uvicorn process (rooms are in memory). Secrets/host in `deploy/.env` (gitignored; template `.env.example`, `DG_DOMAIN` = `<ip-dashes>.sslip.io` for free HTTPS without a domain, or `http://localhost` to try the stack locally). Runbook: `deploy/README.md`.
- Local server note: `uvicorn --reload` only watches `.py` — after editing `maps/*.json`, touch a `.py` file (or restart) so the server reloads the map.
- Trying the production stack locally: `deploy/` up with `DG_DOMAIN=http://localhost`, ServerSettings `ProductionUrl = http://localhost` (+ `EditorUsesProduction` for the editor). Put the settings back afterwards.
- Local multiplayer test: Postgres + server running + macOS build (Build And Run into `unity/Builds/`, gitignored, windowed mode) + editor Play.
- Tests share helpers via `tests/conftest.py` (`client` fixture, `signup(client, name)` → token); ruff isort knows `conftest` as local.
- Test gotcha: Starlette `TestClient` cancels the server task when a `websocket_connect` block exits; call `ws.close()` inside the block to test disconnect handling.

## Map art direction (research 2026-10-21, experimenting)

Full report + raw notes: `docs/research/2026-10-21-map-art/` (`report.md`, `report.html`, `notes/`). Decisions to follow:

- **Approach = "RO hybrid"** (how Ragnarok Online built maps): ground = low-poly 3D mesh with 32px tiling pixel textures (+ vertex-color variation, ProBuilder curbs/steps for height), props (trees, benches, lamps, fountain) = upright pixel sprites exactly like characters (32 PPU, Alpha Clip, X 35° tilt) + blob shadow. Rejected: one big painted 2D background. Later experiment: low-res render-texture pixelation (needs render graph; would resample the 64px characters too).
- **One pixel density everywhere**: 32 PPU sprites, 1 unit = 32 texels on meshes, integer scales only (mixels = #1 amateur tell). Camera Ortho Size = **5.625** (done 2026-10-21; = 640×360 internal → ×2/×3/×4 at 720p/1080p/1440p; 16:10 Mac screens are not integer).
- **URP**: stay on Universal (3D) renderer — 2D Lights, "Sort 3D As 2D" and Pixel Perfect Camera are 2D-renderer features. Unity 6.4 removed URP Compatibility Mode → old `cmd.Blit` renderer-feature tutorials don't work; custom passes need `RecordRenderGraph`. WebGL2: no compute → Shuriken not VFX Graph. Sprite top clipping into walls: keep distance with colliders first; proper fix = bgolus vertical-billboard depth trick in hand-written HLSL.
- **Tools**: PixelLab first (`create-tileset` / `map-objects` support `view = low top-down`, `target_palette`; feed references at native resolution). Perspective-drawn vertical objects must stand upright, never lie on the ground (double perspective). Meshy/Tripo = blockout only (no pixel textures; Meshy free = CC BY 4.0 attribution). **Don't use Mana Seed** (author: incompatible with gen-AI content). LimeZu OK with credit.
- **Art direction**: readability via value contrast — ground lineless, mid-saturation; strongest colors/outlines reserved for characters; grayscale test. Palette from girl01/boy01 → nearest Lospec palette → desaturated map subset (hue-shift ramps). Plaza ≈ 28×28 units (Gather: 20–40 tiles per concurrent player), central fountain, 3–4 conversation islands, building entrances at edges (future channel portals), edges closed by walls/bushes (also fixes walking off the map).
- First plaza plan = 10 steps over two weekends (table in the report).
- **First experiment (2026-10-21, `MapTest.unity`, screenshot `docs/research/2026-10-21-map-art/map-test-01.png`)**: works — Wang seams clean, props match characters, grayscale reads. PixelLab Apprentice ($12/mo, Map Workshop) used.
  - Ground: PixelLab tileset (Wang, 32px) → `art/tilesets/plaza/pixellab-wang.png` → `uv run --no-project --with pillow python art/tilesets/make_ground_atlas.py` (tileset form has **no palette option** → script snaps grass to palette grass ramp, repacks the 5×4 sheet into the 4×4 Wang-index atlas `Sprites/Environment/Ground/plaza_ground.png`, writes `art/tilesets/preview.png`). Extra full-stone variant tile is skipped for now. 35° tilt squashes tiles vertically (expected).
  - Props: Object Creator Pro with palette image + girl01 as style reference (locks size to 64px). 32 candidates in `art/props/pixellab-2026-10-21/`; picks + names in `art/props/export_props.py` (trims empty bottom rows so the bottom-center pivot = ground contact, also writes `blob_shadow.png`) → `Sprites/Environment/Props/`.
  - Next: trees are too small at 64px (≈ character height) → generate trees at 128px; shadows faint; camera SmoothDamp isn't pixel-snapped.
- **Office floor (2026-10-21, first indoor map)**: PixelLab Pro tilesets (Pro = palette option, 15 gen; Standard has none) still bleed the two terrains' colors into each other (pink stone, brick-like planks) → don't use their transitions indoors. `art/tilesets/make_floor_atlas.py` takes one full tile per material from `art/tilesets/office/` (Wang sheet cell (1,1) = full lower, (1,3) = full upper), snaps it to a palette ramp by brightness rank, draws edge trims → `Sprites/Environment/Ground/office_floor.png` (16 × 3 tiles) + `art/tilesets/floor-preview.png` (grayscale check). Give PixelLab one-material palettes; mixing ramps in one palette makes it mix the materials.
  - Office props: Object Creator Pro with girl01 style ref + palette, **"Describe each item" = 16 different objects in one 10-gen run** (all came out right). Sources `art/props/pixellab-2026-10-21-office/`, picks/names in `export_props.py`.
  - Walls: texture drawn in code (`make_walls` in the same script → `office_wall.png`, seamless). Done 2026-10-21 together with floor bounds + furniture collision. Next: zone data shared with the server, sitting.
- **Lighting (2026-10-22)**: fake lighting, no URP lights (sprites have no normals; pixel art). Per map in the JSON → tweak with a server deploy only. `lighting`: `ambient` (multiply color where no light reaches; empty = point lights off), `shadowX/Z` (sun-shadow shear per unit height), `shadowStrength`, `shadowColor`, `clouds`, post `exposure/contrast/saturation/temperature/colorFilter/vignette`. `lights`: `{x, z, radius, intensity, color, flicker, glow (halo diameter), glowY (flame height)}`. Shaders in `Assets/_Project/Shaders/` share `DgLighting.hlsl` (XZ distance only, positions snapped to the 1/32 texel grid, 4 brightness steps with world-space 4×4 Bayer dither): `DgVillage/Ground` (floor + wall materials; wall has `_Clouds` 0; + drifting cloud shadows), `DgVillage/Sprite` (`Materials/Sprite_Lit.mat` = PropCatalog sprite material + Player/RemotePlayer Visual; whole sprite lit by the light at its origin — needs dynamic batching off, it is), `DgVillage/Glow` (additive), `DgVillage/SunShadow` (multiply, stencil so overlaps don't double). Office = dark purple ambient + candelabras/fireplace/crystal/desk lamps (owner's reference: dark RPG room with warm light pools, 2026-10-22); outdoor = daylight sun shadows + clouds. Ideas next: windows with light shafts, time-of-day from the real clock, particles (petals, embers, dust).
- **Map palette**: `art/palettes/dg-village-map.{gpl,hex}` (48 colors: grass/foliage/stone/dirt/wood/water/brick ramps × 6 + 6 character anchors), regenerate with `uv run --no-project --with pillow python art/palettes/make_map_palette.py` (also writes `preview.png` incl. grayscale check). Large ground areas use ramp steps 4–5 (index 3–4); dark steps only for shadows/edges — boy01's black hoodie vanishes on dark grass in grayscale. Give PixelLab `target_palette` only the ramps a generation needs.

## Character art pipeline (PixelLab)

Reference character: `Sprites/Characters/girl01/` (pink-haired chibi girl). Future characters must match this style and size.

- PixelLab → Characters → Create from Text: Humanoid, **v3**, **Low Top-Down**, **48×48**, Detail "Highly detailed". Prompt style: "cute chibi, 2.5 heads tall, classic Korean MMORPG style like Ragnarok Online, bright saturated colors, clean dark outline, soft cel shading".
- Sitting: character page → **Create State** "sitting down on an invisible seat, facing the viewer, knees bent forward, hands resting on lap, no chair, no furniture", **Use color palette from reference** on, Color palette **None** (the form remembers the last object palette — reset it). 10–25 gen per character; the state is its own character page, take its `south` rotation (48×48) → `art/characters/`, paste onto 64×64 with the bbox bottom at 10px from the bottom → `Sprites/Characters/{name}/{name}_sit_south.png` → re-run Create Character Definition.
- Walk: Add Animation → Template motion **Skeleton v3 → Walking → Walk** (6 frames). Template motion keeps outfit/colors identical per frame. Generate South, East, North only (West = flipped East).
- Export → **ZIP (all frames)**. Idle rotations are 48×48; animation frames are a larger centered canvas — **64×64 for girl01, 80×80 for boy01** (varies, check). Target is always 64×64 with ~10px empty below the feet (importer pivot = 10/64). For 64 frames: pad idle centered (`sips -p 64 64`). For 80 frames: paste idle at (16,16) on 80×80, then crop everything to box (8, 10, 72, 74) (Pillow via `uv run --no-project --with pillow`). In the ZIP each direction is a separate `Idle/animations/Walk*/{dir}/frame_00N.png` folder.
- File naming: `{name}_idle_{south|north|east}.png`, `{name}_walk_{south|north|east}_{0..5}.png` → put in `Sprites/Characters/{name}/` → right-click the folder → **DgVillage → Create Character Definition** (auto-registers; shows up in the select screen).
- Free plan: 1 concurrent job ("Too many animations generating" otherwise), ~8–10 min per walk direction. Character = 2 generations, each walk direction = 3 ("Generate in Background"). Check credits/pricing on the site.
- Browser flow: Characters → type prompt in top bar → Create (opens form with prompt) → defaults already match (v3, Low Top-Down, 48px, Highly detailed). Animation: Add Animation → pick Direction radio on the left → Template motion Skeleton v3 → Walking (expand) → **Walk** → Generate.

## Working with the owner

- Owner does editor clicks for one-off setups that teach a Unity concept (placing objects, attaching components, prefabs) — give step-by-step instructions. Claude writes C#, editor tooling, and batch/repetitive work; setups with many fiddly settings get an editor menu script instead (e.g. **DgVillage → Create Chat UI**). Claude also checks compiles (`unity/Logs/Editor.log`) and runs the server.
- Owner often forgets Play mode is on — remind that changes made in Play mode are lost.
- Before committing, verify via the saved scene YAML (grep script GUIDs) that components are actually wired, rather than trusting screenshots.
- Browser automation: start with `claude --chrome` (requires claude.ai login, not API key/token). Ask before downloading files; owner handles signup/payment.
- Background/decision history lives in the owner's wiki: `~/Documents/llm_wiki/wiki/sources/social-pixel-game-feasibility-2026-10-20.md` and `.../sources/dg-village-day1-2026-10-20.md`.

## Gotchas

- Fonts: TMP default (LiberationSans) has no Hangul. `Fonts/Galmuri11 SDF` (Dynamic, multi-atlas, OFL — keep `Galmuri-OFL.txt`) is registered in **TMP Settings → Fallback Font Assets**, so Korean works everywhere without per-component changes. Latin still renders in LiberationSans. Dynamic glyphs get baked into the asset during Play (~1MB diff) — `DynamicFontCleaner` reverts it; if the font asset still shows as modified, run **DgVillage → Clear Dynamic Font Data**, don't commit it.
- Build Profiles Scene List edits live in memory until **File → Save Project** writes `ProjectSettings/EditorBuildSettings.asset`.
- Editor scripts: `EditorSceneManager.NewScene(..., Single)` unloads assets not referenced by the new scene — load assets you want to wire **after** creating the scene, or the reference becomes a fake null (field saved as None).
- Don't use **Create → …** (CreateAssetMenu) for `CharacterDefinition` — it makes an empty asset; use the folder right-click menu.
- `ProjectSettings.asset` `preloadedAssets` (Input System actions) flips on/off around builds — noise, commit whichever state. The file itself must stay tracked.
- **Domain Reload is off** (Enter Play Mode Options), so static fields survive between Play sessions. Reset statics that must start fresh with `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` (done for `ServerConfig`, `ChannelClient.Current`; `AccountClient` intentionally stays logged in).
- Unity editor log is `unity/Logs/Editor.log` (not `~/Library/Logs/Unity`). Grep `error CS` there to check compile results.
- Editing `Packages/manifest.json` while the editor is open is **not** picked up automatically — install via Package Manager "Add package from git URL" (or restart the editor).
- **Import race**: when a new/changed `AssetPostprocessor` and new assets land in the same refresh, assets may import before the script recompiles. Commit the importer first or fix `.meta` (`alignment: 9`, `spritePivot`) afterwards — reimport alone won't re-apply because of the first-import-only guard.
- `uvicorn --reload` restarting on a `db.py` change **before** `alembic upgrade head` dies at startup (missing table) and then hangs every request. Run the migration, then touch a server file (or restart) to reload.
- **Never change Player Settings `Company Name` (`DefaultCompany`) / `Product Name` (`dg-village`)** once testers have builds: PlayerPrefs location is keyed by them, so changing either silently gives every player a new anonymous account.
- Windows tester build: Build Profiles → `Windows` profile (Mono, Intel 64-bit; IL2CPP can't cross-compile from Mac) → `unity/Builds/Windows/dg-village/`, zip without the `*_BackUpThisFolder_ButDontShipItWithYourGame` folder. Unsigned → SmartScreen "More info → Run anyway".
- Mesh Collider warning on Ground ("missing pre-baked triangle collision") is harmless for now.

## Unity conventions

- Put our code under `unity/Assets/_Project/` (Scripts, Sprites, Materials, Prefabs, Scenes) to keep it apart from template/third-party assets.
- Always commit `.meta` files together with their assets. Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`.
- Do not edit Unity-generated YAML (`.unity`, `.prefab`, `.asset`) by hand unless necessary; prefer giving editor steps to the owner.
- C#: PascalCase for types/methods, `_camelCase` for private fields, `[SerializeField] private` over public fields.
