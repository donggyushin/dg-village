# dg-village

Gather.town-style social space game: users create channels, walk around as characters, and chat (text first, voice later). Differentiator is the art — cute 2.5D pixel style inspired by old Korean RPGs (Ragnarok, Tales Weaver, To the Moon).

Solo hobby project (weekends + spare time). Owner is strong on Python/server, new to Unity and art — **explain Unity concepts when introducing them**.

## Communication

- Always respond in Korean. Keep technical terms and code identifiers in original form.

## Layout

```
dg-village/
├── unity/    # Game client (Unity 6, URP, Universal 3D template)
├── python/   # WebSocket server (FastAPI, uv) + Dockerfile
└── deploy/   # Production docker compose (Caddy + server + Postgres), README.md = runbook
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
9. Art pass (map tiles, buildings — the floor currently looks flat/2D because it's a plain ortho plane).
10. Voice.

Accounts (owner request 2026-10-20; "no signup screen like Steam games, but extensible"):
- Phase 1 (**done**) = anonymous accounts. First launch: `POST /auth/anonymous` creates a user + a random device credential; the client keeps the credential in PlayerPrefs (`dgvillage.anonymousCredential`) and logs in with it afterwards. Losing it = losing the account (until a provider is linked).
- Schema: `users` (profile only: `id` uuid, `display_name`, `character_id`, timestamps) + `auth_identities` (`provider`, `subject` unique pair → `user_id`). Anonymous subject = SHA-256 of the credential (raw credential never stored).
- Adding a login method (Steam later, maybe Discord): implement an `AuthProvider` in `auth.py` (proof → stable subject), register it in `PROVIDERS`, add a "link" endpoint that attaches a new identity to the current user. `POST /auth/login {provider, credential}` and the game server stay unchanged — the WebSocket only checks the JWT.
- Player id in rooms = account id (hex). `name` is display-only (not unique). Same account joining twice → the older session is kicked (`error` "logged in from another session", close 4000).
- Editor and standalone builds have separate PlayerPrefs → separate accounts (handy for 2-client tests). Two copies of the same build share one account and kick each other.

Character system: each character is a `CharacterDefinition` asset (`Assets/_Project/Characters/{id}.asset`, id = sprite folder name) listed in `CharacterRegistry.asset` (first entry = default/fallback). The server only syncs `characterId`; each client resolves it via the registry. The chosen character/nickname live in the account profile on the server (`PATCH /me`), so they persist across launches and scenes.

## Current state (as of 2026-10-20)

Scenes (Build Profiles → Scene List, order matters): `0 Assets/Scenes/CharacterSelect.unity` (start screen) → `1 Assets/Scenes/ChannelSelect.unity` (channel list) → `2 Assets/Scenes/SampleScene.unity` (village). Playing ChannelSelect or SampleScene directly still works — they log in themselves; SampleScene without a picked channel joins the plaza.

`ChannelSelect.unity` (built by **DgVillage → Create Channel Select Scene**): Main Camera, EventSystem, `ChannelCanvas` + `ChannelSelect` (Title, BackButton, RefreshButton, `ChannelList` ScrollView whose Content holds the inactive `ChannelItemTemplate` (`ChannelListItem`), NameInput, CreateButton, StatusText).

`CharacterSelect.unity` (built by **DgVillage → Create Character Select Scene**): Main Camera (solid bg), EventSystem, `SelectCanvas` + `CharacterSelect` (Title, Preview, Previous/Next, CharacterName, NameInput, EnterButton, StatusText).

Scene `Assets/Scenes/SampleScene.unity`:

```
Main Camera   Orthographic, Size 5, pos (0, 8, -11.5), rot X 35  + CameraFollow(target = Player)
Ground        Plane, scale (2,1,2) → 20x20 units. No bounds/collision yet (player can walk off)
Player        empty root, movement only               + PlayerMovement
└── Visual    SpriteRenderer, rot X 35 (faces camera) + DirectionalSpriteAnimator(character = girl01, replaced at runtime)
Network       empty at origin                         + NetworkClient(localPlayer = Player, characters = registry) + RemotePlayerSpawner(prefab = RemotePlayer, characters = registry)
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
| `PlayerMovement` | XZ movement from Input System `Player/Move` (project-wide actions). Exposes `Facing` (Down/Up/Left/Right), `IsMoving`, and `InputBlocked` (set by chat) |
| `CameraFollow` | Keeps the scene-authored offset to target, SmoothDamp in LateUpdate |
| `ICharacterMotion` | `Facing` + `IsMoving`. Implemented by `PlayerMovement` (local input) and `RemotePlayer` (server data) |
| `DirectionalSpriteAnimator` | Plays idle/walk sprites of its `CharacterDefinition` from parent `ICharacterMotion`. Left = Right with `flipX`. `SetCharacter()` swaps at runtime |
| `CharacterDefinition` | ScriptableObject: `Id`, `DisplayName`, per-direction `Idle` + `Walk[]`, `Get(Facing)`, `Preview` (down idle) |
| `CharacterRegistry` | ScriptableObject list of definitions; `Find(id)` falls back to first entry |
| `UI/CharacterSelect` | Select scene: on Start `AccountClient.LoginAsync` (status text while busy; failure → Enter retries), prefills from profile; ◀▶ / ←→ / A·D cycle, animated down-walk preview, nickname (≤16, empty = keep current); Enter → `PATCH /me` → `LoadScene(ChannelSelect)` |
| `UI/ChannelSelect` | Channel list scene: `GET /channels` → one `ChannelListItem` per channel (Instantiate of the inactive template into the ScrollView Content); click → `ChannelClient.Select` → village. Name input + Create (Enter) → `POST /channels` → join it (409 → "3개까지"). Refresh button; Back / Esc → CharacterSelect |
| `UI/ChannelListItem` | One list row: Button + Name + PlayerCount texts, `Bind(channel, onClick)` |
| `UI/ChannelHud` | Village: shows current channel name, Leave button → `LoadScene(ChannelSelect)` (NetworkClient's OnDestroy closes the socket → server sends `player_left`) |
| `Network/ServerConfig` | Static. Resolves the server URL once per Play: `-server <url>` command-line arg > `ServerSettings` (editor → `EditorUsesProduction`, builds → `BuildsUseProduction`) > `http://localhost:8000`. `WebSocketUrl` derived (http→ws, https→wss, + `/ws`) |
| `Network/ServerSettings` | ScriptableObject at `Assets/_Project/Resources/ServerSettings.asset` (loaded via `Resources.Load`): `LocalUrl`, `ProductionUrl` (empty until the VPS exists), two "use production" toggles. Defaults: editor = local, builds = production |
| `Network/AccountClient` | Static. `LoginAsync` (stored credential → `/auth/login`, else/401 → `/auth/anonymous`; then `GET /me`), `UpdateProfileAsync`, `AccessToken`, `Profile`, `NormalizeDisplayName`/`NormalizeName`. `internal SendAsync` = shared HTTP helper (adds the token). UnityWebRequest + `Awaitable` (WebGL-safe). Throws `AccountException` (Status 0 = unreachable) |
| `Network/ChannelClient` | Static. `ListAsync`, `CreateAsync`, `Select`/`Current` (survives scene loads), `CurrentId` (falls back to `DefaultChannelId` = plaza). Constants mirror the server (name ≤24, 3 per user) |
| `Network/NetworkMessages` | JsonUtility DTOs mirroring `protocol.py` and `api.py` (camelCase public fields, exception to naming rule) + `FacingWire`. JsonUtility sends null strings as `""`, so `ProfileUpdate` always fills both fields |
| `Network/NetworkClient` | On Start: logs in if not yet (direct Play of SampleScene), applies profile character (via registry) + name to the local player, then NativeWebSocket connect → `join {room = ChannelClient.CurrentId, token}` → sends `move` at 10Hz only when changed; `SendChat`; raises C# events per server message. Sets `runInBackground` |
| `Network/RemotePlayerSpawner` | Subscribes to `NetworkClient` events; Instantiate/ApplyState/Destroy `RemotePlayer`s, skips own id, resolves `characterId` via registry |
| `Network/RemotePlayer` | `Init(state, character)`; exponential lerp toward server position, snaps if > 5 units |
| `NameTag` | World-space TMP under `Visual` (inherits camera tilt). `Reset()` sets default look; rich text off |
| `Chat/ChatInput` | Enter opens/sends, Esc cancels; visible only while typing (CanvasGroup alpha, not SetActive — Update must keep running); sets `PlayerMovement.InputBlocked` while focused |
| `Chat/ChatBubble` | Screen-space bubble sized via `GetPreferredValues`, follows anchor (name tag) each LateUpdate, self-destroys after 3–8s |
| `Chat/ChatBubbleSpawner` | `NetworkClient.ChatReceived` → bubble over speaker (own bubble comes from the server echo) |
| `Editor/ChatUIBuilder` | Menu **DgVillage → Create Chat UI**: builds/wires canvas, input, EventSystem, ChatBubble prefab |
| `Editor/PixelSpriteImporter` | Auto pixel import settings for `Sprites/` (Point, no compression, 32 PPU). `Sprites/Characters/` gets foot pivot (0.5, 10/64). **Only applies on first import** (`importSettingsMissing`) |
| `Editor/CharacterDefinitionBuilder` | Project window: right-click a character sprite folder → **DgVillage → Create Character Definition** — creates/updates `Characters/{folder}.asset` by filename convention and registers it |
| `Editor/CharacterSelectSceneBuilder` | Menu **DgVillage → Create Character Select Scene**: builds/wires/saves `CharacterSelect.unity` (overwrites after confirm) |
| `Editor/ChannelSelectSceneBuilder` | Menu **DgVillage → Create Channel Select Scene**: same for `ChannelSelect.unity` (ScrollView + VerticalLayoutGroup + ContentSizeFitter list) |
| `Editor/SceneUIBuilder` | Shared helpers for the scene builders: camera + EventSystem, 1280x720 canvas, default UI sprites, `Place`/`CreateText`/`CreateButton` |
| `Editor/ServerSettingsMenu` | Menu **DgVillage → Server Settings** (selects/creates the asset). `IPreprocessBuildWithReport`: fails the build if builds use production but `ProductionUrl` is empty — uncheck `BuildsUseProduction` for local-only test builds |
| `Editor/DynamicFontCleaner` | Clears dynamic TMP font atlases in `Fonts/` on returning to Edit mode (also menu **DgVillage → Clear Dynamic Font Data**) |

## Server (`python/`)

- Run: `docker compose up -d` (repo root, Postgres) → `cd python && uv sync && uv run alembic upgrade head` → `uv run uvicorn dg_village_server.main:app --reload` (port 8000, WS at `/ws`, `GET /health`). Tests: `uv run pytest` (no Docker needed), lint: `uv run ruff check .`
- Env: `DG_DATABASE_URL` (default = compose Postgres), `DG_JWT_SECRET` (dev default logs a warning — **must** be set when deployed).
- `protocol.py` (pydantic WS schemas, camelCase on the wire, `normalize_display_name`), `room.py` (in-memory `RoomManager`/`Room`, 10Hz tick broadcasts `state` only for rooms that changed; one session per player id), `config.py` (`Settings.from_env`), `db.py` (models + engine), `auth.py` (`AuthProvider`s, JWT), `api.py` (HTTP routes), `main.py` (app factory `create_app(settings, create_tables=)` + `/ws` handler)
- HTTP: `POST /auth/anonymous` → `{userId, credential, accessToken}`; `POST /auth/login {provider, credential}` → `{userId, accessToken}`; `GET /me`, `PATCH /me {displayName?, characterId?}` with `Authorization: Bearer`. Errors are FastAPI `{detail}`.
- Channels: `channels` table (`id` uuid, `name` ≤24, `owner_id` → users, NULL = system, `ON DELETE SET NULL`). The plaza "광장" has the fixed id `00000000-0000-0000-0000-000000000001` and is created at startup (`ensure_default_channel`) if missing. `GET /channels` → `{channels: [{id (hex), name, ownerId, playerCount}]}` (wrapped — JsonUtility can't parse top-level arrays; plaza first, then busiest; counts come from `RoomManager`). `POST /channels {name}` → 201, max `MAX_CHANNELS_PER_USER` = 3 per owner (409). Empty channels are kept (MVP). No delete/rename/private channels yet.
- Migrations: change `db.py` → `uv run alembic revision --autogenerate -m "..."` → review the file in `migrations/versions/` → `uv run alembic upgrade head`. Generated files are auto-formatted by ruff (post-write hook). `uv run alembic check` = models vs DB drift.
- Protocol: client sends `join {room, token}` first (`room` = channel id hex; unknown → `error` "unknown channel" + close 1008) (name/character come from the account), then `move {x, z, facing, moving}` (`facing` = `down|up|left|right`) and `chat {text}` (whitespace collapsed, ≤200 chars, 0.5s rate limit). Server sends `welcome {id, players}`, `player_joined`, `player_left`, `state {players}`, `chat {id, name, text}` (to whole room incl. sender), `error`.
- Rooms/positions are in memory; accounts and channels are in Postgres. Reset local DB: `docker compose down -v` (clients then silently create new anonymous accounts).
- Deploy: AWS Lightsail instance `dg-village-origin` (Ubuntu 24.04, 2 GB, $12/mo) in **Sydney** `ap-southeast-2` — the AWS free plan ("New Frontiers" org, $120 credits until **2027-04-20**) is locked to Sydney by an SCP; unlocking other regions requires upgrading to a paid plan and deletes spend limits, so we stayed. Static IP `13.54.176.157` (`dg-village-ip`), firewall 22/80/443. SSH: `ssh -i ~/.ssh/dg-village ubuntu@13.54.176.157`; repo cloned at `~/dg-village` (public GitHub repo), `deploy/.env` generated on the server (secrets never left it). **Auto-deploy**: push to main touching `python/**`/`deploy/**` → `.github/workflows/deploy.yml` (ruff + pytest, then SSH with deploy-only key `~/.ssh/dg-village-deploy`, forced to `deploy/deploy.sh` via `restrict,command=` in `authorized_keys`; Secrets `DEPLOY_SSH_KEY`/`DEPLOY_HOST`/`DEPLOY_KNOWN_HOSTS`). `deploy.sh` = `git pull --ff-only` → `docker compose up -d --build --wait` → image prune; manual update = run it on the server. Never edit the server's working tree (except `deploy/.env`) or ff-only pull breaks. `deploy/compose.yaml` (project `dg-village-prod`): Caddy publishes 80/443 and reverse-proxies (WS included) to `server:8000`; server and Postgres are internal only. `python/Dockerfile` runs `alembic upgrade head` then a **single** uvicorn process (rooms are in memory). Secrets/host in `deploy/.env` (gitignored; template `.env.example`, `DG_DOMAIN` = `<ip-dashes>.sslip.io` for free HTTPS without a domain, or `http://localhost` to try the stack locally). Runbook: `deploy/README.md`.
- Trying the production stack locally: `deploy/` up with `DG_DOMAIN=http://localhost`, ServerSettings `ProductionUrl = http://localhost` (+ `EditorUsesProduction` for the editor). Put the settings back afterwards.
- Local multiplayer test: Postgres + server running + macOS build (Build And Run into `unity/Builds/`, gitignored, windowed mode) + editor Play.
- Tests share helpers via `tests/conftest.py` (`client` fixture, `signup(client, name)` → token); ruff isort knows `conftest` as local.
- Test gotcha: Starlette `TestClient` cancels the server task when a `websocket_connect` block exits; call `ws.close()` inside the block to test disconnect handling.

## Character art pipeline (PixelLab)

Reference character: `Sprites/Characters/girl01/` (pink-haired chibi girl). Future characters must match this style and size.

- PixelLab → Characters → Create from Text: Humanoid, **v3**, **Low Top-Down**, **48×48**, Detail "Highly detailed". Prompt style: "cute chibi, 2.5 heads tall, classic Korean MMORPG style like Ragnarok Online, bright saturated colors, clean dark outline, soft cel shading".
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
- Mesh Collider warning on Ground ("missing pre-baked triangle collision") is harmless for now.

## Unity conventions

- Put our code under `unity/Assets/_Project/` (Scripts, Sprites, Materials, Prefabs, Scenes) to keep it apart from template/third-party assets.
- Always commit `.meta` files together with their assets. Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`.
- Do not edit Unity-generated YAML (`.unity`, `.prefab`, `.asset`) by hand unless necessary; prefer giving editor steps to the owner.
- C#: PascalCase for types/methods, `_camelCase` for private fields, `[SerializeField] private` over public fields.
