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
| Server | Python — `asyncio` + `websockets` or FastAPI WebSocket. Hand-rolled; **no** Netcode for GameObjects / Mirror |
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
4. **Next**: Player character selection (see below).
5. Accounts + DB (see below).
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

Planned feature (owner request, 2026-10-20): **player chooses their character.** Plan: move sprite sets out of `DirectionalSpriteAnimator`'s inline fields into a `CharacterDefinition` ScriptableObject (id + per-direction idle/walk sprites), one asset per character, collected in a registry. The animator reads the currently selected definition at runtime. In multiplayer, the server only syncs `characterId`; each client resolves it locally. The editor-only "Fill From Character Folder" menu should then fill a `CharacterDefinition` asset instead of the component. Do this before adding a second character.

## Current state (as of 2026-10-20)

Scene `Assets/Scenes/SampleScene.unity`:

```
Main Camera   Orthographic, Size 5, pos (0, 8, -11.5), rot X 35  + CameraFollow(target = Player)
Ground        Plane, scale (2,1,2) → 20x20 units. No bounds/collision yet (player can walk off)
Player        empty root, movement only               + PlayerMovement
└── Visual    SpriteRenderer, rot X 35 (faces camera) + DirectionalSpriteAnimator(girl01)
Network       empty at origin                         + NetworkClient(localPlayer = Player) + RemotePlayerSpawner(prefab = RemotePlayer)
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
| `PlayerMovement` | XZ movement from Input System `Player/Move` (project-wide actions). Exposes `Facing` (Down/Up/Left/Right) and `IsMoving` |
| `CameraFollow` | Keeps the scene-authored offset to target, SmoothDamp in LateUpdate |
| `ICharacterMotion` | `Facing` + `IsMoving`. Implemented by `PlayerMovement` (local input) and `RemotePlayer` (server data) |
| `DirectionalSpriteAnimator` | Picks idle/walk sprites from parent `ICharacterMotion`. Left = Right with `flipX` |
| `Network/NetworkMessages` | JsonUtility DTOs mirroring `protocol.py` (camelCase public fields, exception to naming rule) + `FacingWire` |
| `Network/NetworkClient` | NativeWebSocket connect → `join` → sends `move` at 10Hz only when changed; raises C# events per server message. Sets `runInBackground` |
| `Network/RemotePlayerSpawner` | Subscribes to `NetworkClient` events; Instantiate/ApplyState/Destroy `RemotePlayer`s, skips own id |
| `Network/RemotePlayer` | Exponential lerp toward server position, snaps if > 5 units |
| `NameTag` | World-space TMP under `Visual` (inherits camera tilt). `Reset()` sets default look; rich text off |
| `Chat/ChatInput` | Enter opens/sends, Esc cancels; sets `PlayerMovement.InputBlocked` while focused |
| `Chat/ChatBubble` | Screen-space bubble sized via `GetPreferredValues`, follows anchor (name tag) each LateUpdate, self-destroys after 3–8s |
| `Chat/ChatBubbleSpawner` | `NetworkClient.ChatReceived` → bubble over speaker (own bubble comes from the server echo) |
| `Editor/ChatUIBuilder` | Menu **DgVillage → Create Chat UI**: builds/wires canvas, input, EventSystem, ChatBubble prefab |
| `Editor/PixelSpriteImporter` | Auto pixel import settings for `Sprites/` (Point, no compression, 32 PPU). `Sprites/Characters/` gets foot pivot (0.5, 10/64). **Only applies on first import** (`importSettingsMissing`) |
| `Editor/DirectionalSpriteAnimatorFiller` | Component ⋮ menu "Fill From Character Folder" — fills sprites by filename convention |

## Server (`python/`)

- `uv sync` → `uv run uvicorn dg_village_server.main:app --reload` (port 8000, WS at `/ws`, `GET /health`). Tests: `uv run pytest`, lint: `uv run ruff check .`
- `protocol.py` (pydantic message schemas, camelCase on the wire), `room.py` (in-memory `RoomManager`/`Room`, 10Hz tick broadcasts `state` only for rooms that changed), `main.py` (app factory + `/ws` handler)
- Protocol: client sends `join {room, name, characterId}` first, then `move {x, z, facing, moving}` (`facing` = `down|up|left|right`) and `chat {text}` (whitespace collapsed, ≤200 chars, 0.5s rate limit). Server sends `welcome {id, players}`, `player_joined`, `player_left`, `state {players}`, `chat {id, name, text}` (to whole room incl. sender), `error`.
- No DB yet — all state in memory. Postgres arrives with channels (roadmap 4), as a separate container in the same docker compose.
- Deploy plan: single VPS in **Seoul region** (Lightsail/Vultr, ~2GB RAM) + Caddy (TLS, `wss://` needed for WebGL). Buy only when external testing starts.
- Test gotcha: Starlette `TestClient` cancels the server task when a `websocket_connect` block exits; call `ws.close()` inside the block to test disconnect handling.

## Character art pipeline (PixelLab)

Reference character: `Sprites/Characters/girl01/` (pink-haired chibi girl). Future characters must match this style and size.

- PixelLab → Characters → Create from Text: Humanoid, **v3**, **Low Top-Down**, **48×48**, Detail "Highly detailed". Prompt style: "cute chibi, 2.5 heads tall, classic Korean MMORPG style like Ragnarok Online, bright saturated colors, clean dark outline, soft cel shading".
- Walk: Add Animation → Template motion **Skeleton v3 → Walking → Walk** (6 frames). Template motion keeps outfit/colors identical per frame. Generate South, East, North only (West = flipped East).
- Export → **ZIP (all frames)**. Idle rotations are 48×48, animation frames are **64×64** (centered padding). Pad idle to 64×64 centered (`sips -p 64 64`) so feet line up — both end up with 10px empty below the feet.
- File naming: `{name}_idle_{south|north|east}.png`, `{name}_walk_{south|north|east}_{0..5}.png` → put in `Sprites/Characters/{name}/` → Visual's animator ⋮ → Fill From Character Folder.
- Free plan: 1 concurrent job, ~8–10 min per walk direction. Check credits/pricing on the site.

## Working with the owner

- Owner does editor clicks (placing objects, attaching components) — give step-by-step instructions. Claude writes C#, editor tooling, and batch/repetitive work.
- Owner often forgets Play mode is on — remind that changes made in Play mode are lost.
- Before committing, verify via the saved scene YAML (grep script GUIDs) that components are actually wired, rather than trusting screenshots.
- Browser automation: start with `claude --chrome` (requires claude.ai login, not API key/token). Ask before downloading files; owner handles signup/payment.
- Background/decision history lives in the owner's wiki: `~/Documents/llm_wiki/wiki/sources/social-pixel-game-feasibility-2026-10-20.md` and `.../sources/dg-village-day1-2026-10-20.md`.

## Gotchas

- Fonts: TMP default (LiberationSans) has no Hangul. `Fonts/Galmuri11 SDF` (Dynamic, multi-atlas, OFL — keep `Galmuri-OFL.txt`) is registered in **TMP Settings → Fallback Font Assets**, so Korean works everywhere without per-component changes. Latin still renders in LiberationSans.
- `ProjectSettings.asset` `preloadedAssets` (Input System actions) flips on/off around builds — noise, commit whichever state.

- Unity editor log is `unity/Logs/Editor.log` (not `~/Library/Logs/Unity`). Grep `error CS` there to check compile results.
- Editing `Packages/manifest.json` while the editor is open is **not** picked up automatically — install via Package Manager "Add package from git URL" (or restart the editor).

- **Import race**: when a new/changed `AssetPostprocessor` and new assets land in the same refresh, assets may import before the script recompiles. Commit the importer first or fix `.meta` (`alignment: 9`, `spritePivot`) afterwards — reimport alone won't re-apply because of the first-import-only guard.
- Mesh Collider warning on Ground ("missing pre-baked triangle collision") is harmless for now.

## Unity conventions

- Put our code under `unity/Assets/_Project/` (Scripts, Sprites, Materials, Prefabs, Scenes) to keep it apart from template/third-party assets.
- Always commit `.meta` files together with their assets. Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`.
- Do not edit Unity-generated YAML (`.unity`, `.prefab`, `.asset`) by hand unless necessary; prefer giving editor steps to the owner.
- C#: PascalCase for types/methods, `_camelCase` for private fields, `[SerializeField] private` over public fields.
