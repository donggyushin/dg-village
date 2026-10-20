# dg-village

Gather.town-style social space game: users create channels, walk around as characters, and chat (text first, voice later). Differentiator is the art — cute 2.5D pixel style inspired by old Korean RPGs (Ragnarok, Tales Weaver, To the Moon).

Solo hobby project (weekends + spare time). Owner is strong on Python/server, new to Unity and art — **explain Unity concepts when introducing them**.

## Communication

- Always respond in Korean. Keep technical terms and code identifiers in original form.

## Layout

```
dg-village/
├── unity/    # Game client (Unity 6, URP, Universal 3D template)
└── python/   # WebSocket server (FastAPI, uv)
```

Single git repo at the root, one `.gitignore` covering both.

## Stack & decisions

| Area | Decision |
|---|---|
| Client | Unity `6000.6.6f1` (Apple Silicon), URP 17, new Input System, NativeWebSocket 2.0.7 (`upm-2` branch pinned by commit hash in `manifest.json`) |
| Server | Python 3.13 — FastAPI WebSocket + uvicorn, pydantic schemas, managed with `uv`. Hand-rolled; **no** Netcode for GameObjects / Mirror |
| Protocol | JSON over WebSocket (rooms, position broadcast ~10Hz, chat) |
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
4. ~~Player character selection.~~ **Done 2026-10-20** (CharacterSelect scene + `characterId` sync). Second character `boy01` (black hoodie + laptop) in progress.
5. **Next**: Accounts + DB (see below).
6. Create/join channels.
7. Deploy (Seoul VPS) for external testing.
8. Social login (Google/Discord OAuth).
9. Art pass (map tiles, buildings — the floor currently looks flat/2D because it's a plain ortho plane).
10. Voice.

Accounts plan (owner request, 2026-10-20: "eventually log in, create an account, then enter channels"):
- Start right before channels — channels need an owner (account id), and Postgres + Alembic arrive at the same time (`users` + `channels` designed together). Must exist before public deploy. Stay guest-only until then; login would just slow down testing.
- Phase 1 = **anonymous accounts**: on first launch the server issues an account + token; client stores the token locally and reuses it, so nickname/character choice persist without a signup screen.
- Phase 2 = link **social login** (Google/Discord OAuth) to the anonymous account. Desktop Unity OAuth needs a browser round-trip, so it comes last. No email/password (hashing, resets, email sending not worth it).
- Auth provider (self-issued tokens vs Supabase Auth etc.) decided then; the game server only verifies a token either way.
- Keep now: `name` is display-only (not unique, never an identity) — identify by server-issued `id`. Entry stays a single `join` message; accounts will just add a `token` field to it.

Character system: each character is a `CharacterDefinition` asset (`Assets/_Project/Characters/{id}.asset`, id = sprite folder name) listed in `CharacterRegistry.asset` (first entry = default/fallback). The server only syncs `characterId`; each client resolves it via the registry. Selection is passed between scenes by `PlayerSession` (PlayerPrefs-backed — moves server-side with accounts).

## Current state (as of 2026-10-20)

Scenes (Build Profiles → Scene List, order matters): `0 Assets/Scenes/CharacterSelect.unity` (start screen) → `1 Assets/Scenes/SampleScene.unity` (village). Playing SampleScene directly still works — it joins with the last saved selection.

`CharacterSelect.unity` (built by **DgVillage → Create Character Select Scene**): Main Camera (solid bg), EventSystem, `SelectCanvas` + `CharacterSelect` (Title, Preview, Previous/Next, CharacterName, NameInput, EnterButton).

Scene `Assets/Scenes/SampleScene.unity`:

```
Main Camera   Orthographic, Size 5, pos (0, 8, -11.5), rot X 35  + CameraFollow(target = Player)
Ground        Plane, scale (2,1,2) → 20x20 units. No bounds/collision yet (player can walk off)
Player        empty root, movement only               + PlayerMovement
└── Visual    SpriteRenderer, rot X 35 (faces camera) + DirectionalSpriteAnimator(character = girl01, replaced at runtime)
Network       empty at origin                         + NetworkClient(localPlayer = Player, characters = registry) + RemotePlayerSpawner(prefab = RemotePlayer, characters = registry)
ChatCanvas    Screen Space Overlay, 1280x720 scaler   + ChatBubbleSpawner(prefab = ChatBubble, root = Bubbles)
├── Bubbles   full-stretch container (first child → drawn under the input)
└── ChatInput TMP_InputField, bottom center           + ChatInput
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
| `PlayerSession` | Static, PlayerPrefs-backed `PlayerName` / `CharacterId` handed from select scene to village |
| `UI/CharacterSelect` | Select scene: ◀▶ / ←→ / A·D cycle, animated down-walk preview, nickname (≤16), Enter → `PlayerSession.Save` → `LoadScene(SampleScene)` |
| `Network/NetworkMessages` | JsonUtility DTOs mirroring `protocol.py` (camelCase public fields, exception to naming rule) + `FacingWire` |
| `Network/NetworkClient` | On Start: applies `PlayerSession` character (via registry) + name (random `guestNNNN` if empty, ≤16) to the local player, then NativeWebSocket connect → `join` → sends `move` at 10Hz only when changed; `SendChat`; raises C# events per server message. Sets `runInBackground` |
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
| `Editor/DynamicFontCleaner` | Clears dynamic TMP font atlases in `Fonts/` on returning to Edit mode (also menu **DgVillage → Clear Dynamic Font Data**) |

## Server (`python/`)

- `uv sync` → `uv run uvicorn dg_village_server.main:app --reload` (port 8000, WS at `/ws`, `GET /health`). Tests: `uv run pytest`, lint: `uv run ruff check .`
- `protocol.py` (pydantic message schemas, camelCase on the wire), `room.py` (in-memory `RoomManager`/`Room`, 10Hz tick broadcasts `state` only for rooms that changed), `main.py` (app factory + `/ws` handler)
- Protocol: client sends `join {room, name, characterId}` first, then `move {x, z, facing, moving}` (`facing` = `down|up|left|right`) and `chat {text}` (whitespace collapsed, ≤200 chars, 0.5s rate limit). Server sends `welcome {id, players}`, `player_joined`, `player_left`, `state {players}`, `chat {id, name, text}` (to whole room incl. sender), `error`.
- No DB yet — all state in memory. Postgres arrives with accounts (roadmap 5), as a separate container in the same docker compose.
- Deploy plan: single VPS in **Seoul region** (Lightsail/Vultr, ~2GB RAM) + Caddy (TLS, `wss://` needed for WebGL). Buy only when external testing starts.
- Local multiplayer test: server running + macOS build (Build And Run into `unity/Builds/`, gitignored, windowed mode) + editor Play.
- Test gotcha: Starlette `TestClient` cancels the server task when a `websocket_connect` block exits; call `ws.close()` inside the block to test disconnect handling.

## Character art pipeline (PixelLab)

Reference character: `Sprites/Characters/girl01/` (pink-haired chibi girl). Future characters must match this style and size.

- PixelLab → Characters → Create from Text: Humanoid, **v3**, **Low Top-Down**, **48×48**, Detail "Highly detailed". Prompt style: "cute chibi, 2.5 heads tall, classic Korean MMORPG style like Ragnarok Online, bright saturated colors, clean dark outline, soft cel shading".
- Walk: Add Animation → Template motion **Skeleton v3 → Walking → Walk** (6 frames). Template motion keeps outfit/colors identical per frame. Generate South, East, North only (West = flipped East).
- Export → **ZIP (all frames)**. Idle rotations are 48×48, animation frames are **64×64** (centered padding). Pad idle to 64×64 centered (`sips -p 64 64`) so feet line up — both end up with 10px empty below the feet.
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
- Don't use **Create → …** (CreateAssetMenu) for `CharacterDefinition` — it makes an empty asset; use the folder right-click menu.
- `ProjectSettings.asset` `preloadedAssets` (Input System actions) flips on/off around builds — noise, commit whichever state. The file itself must stay tracked.
- Unity editor log is `unity/Logs/Editor.log` (not `~/Library/Logs/Unity`). Grep `error CS` there to check compile results.
- Editing `Packages/manifest.json` while the editor is open is **not** picked up automatically — install via Package Manager "Add package from git URL" (or restart the editor).
- **Import race**: when a new/changed `AssetPostprocessor` and new assets land in the same refresh, assets may import before the script recompiles. Commit the importer first or fix `.meta` (`alignment: 9`, `spritePivot`) afterwards — reimport alone won't re-apply because of the first-import-only guard.
- Mesh Collider warning on Ground ("missing pre-baked triangle collision") is harmless for now.

## Unity conventions

- Put our code under `unity/Assets/_Project/` (Scripts, Sprites, Materials, Prefabs, Scenes) to keep it apart from template/third-party assets.
- Always commit `.meta` files together with their assets. Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`.
- Do not edit Unity-generated YAML (`.unity`, `.prefab`, `.asset`) by hand unless necessary; prefer giving editor steps to the owner.
- C#: PascalCase for types/methods, `_camelCase` for private fields, `[SerializeField] private` over public fields.
