# dg-village

**Restarted from scratch 2026-10-26** (commit b8c1ce4 "delete everything"). The previous Gather.town-style village (channels, chat, anonymous accounts, quarter-view maps) lives only in git history — `git show 100fb2a:<path>` to look at old code. Don't restore or copy old code unless the owner asks.

Solo hobby project (weekends + spare time). Owner is strong on Python/server, new to Unity and art — **explain Unity concepts when introducing them**.

## Working with the owner

- **Learning Unity is a goal of this project** — the owner wants to become a solo indie game developer as a hobby (2026-10-26). So the owner does the Unity work (code + editor) themselves as much as possible ("작업하지마. 내가 직접 할거야").
- Don't create projects, files or scripts unless explicitly asked. Default role = answer questions, explain Unity concepts (why, not just how), review, and update docs when asked. The owner will sometimes ask Claude to do a task — then do just that task.
- Always respond in Korean. Keep technical terms and code identifiers in original form.
- Owner often forgets Play mode is on — remind that changes made in Play mode are lost.

## Game concept (2026-10-26)

No concrete genre/story yet. The one hard goal: **a beautiful game**.

- World = **3D**. Ground = Unity **Terrain**, visually covered with **pixel-art (dot) textures**.
- Characters and objects = mostly **2D sprites** in the 3D world (HD-2D-like: 2D characters exploring a 3D world).
- Player = **8-direction** 2D sprite asset.
- Physics = Unity `Collider` + `Rigidbody` (collision, gravity).
- Camera = **fixed angle, no rotation**. **Orthographic isometric** (decided 2026-10-27): rotation (30, 45, 0) → a 1×1 floor square shows as a 2:1 diamond. Size 6 for now (to be tuned in the pixelation step).
- Map = **3D modular blocks**, not Terrain (decided 2026-10-27). Visual reference: **Witchbrook** (stepped heights: curbs, stairs, planter walls; crisp borders; floor decals). Terrain heightmaps only make smooth slopes and blended borders, so Terrain is kept for natural areas only, if ever.
- **Multiplayer from the start**: e.g. a `Player` prefab spawned at runtime by `PlayerSpawner` / `GameManager`, player id from the server, sprite set applied from the server's data.

## Layout (planned)

```
dg-village/
├── unity/    # Game client (Unity 6, URP) — to be recreated by the owner
└── python/   # Game server (Python) — to be recreated by the owner
```

Still in the repo: `.github/workflows/deploy.yml` (old auto-deploy; see **Deploy**), `.gitignore` (covers Unity + Python).

## Stack

| Area | Decision |
|---|---|
| Client | Unity `6000.6.6f1` (Apple Silicon, installed via Hub), URP |
| Server | Python, hosted on the same Lightsail instance as before |
| Graphics | 3D Terrain + 2D pixel sprites, fixed camera (Orthographic likely) |
| Platforms | Desktop first (Mac + Windows) |

Carry-over considerations from the old project (apply if still relevant):
- If WebGL is ever a target, pick WebGL-safe client libraries (e.g. NativeWebSocket, not `System.Net.WebSockets`; no threads in networking).
- Old protocol idea that worked: JSON over WebSocket, server assigns the player id in `welcome`, ~10Hz state broadcast, each client resolves `characterId` to a sprite set locally.

## Map building (greybox)

Roadmap: isometric camera ✅ → **greybox** (grey blocks for shape only, in progress) → pixelation render (low-res render target) → textures and decals.

- Pieces are **ProBuilder** shapes (`com.unity.probuilder` 6.1.4). ProBuilder 6 has no ProBuilder Window: create shapes via **GameObject → ProBuilder → Cube / Stair**, switch edit mode via the Tool Context dropdown at the top of the Scene tools overlay, and use the Scene view right-click menu for actions.
- Resize with the **ProBuilder Shape `Size`**, never Transform `Scale` (scale stretches textures). The default `ProBuilderDefault` material shows a 1 m grid, which works as a ruler.
- Units: floor cell 1×1 m, level height **1 m**, curb 0.25 m, ground slab 0.5 m thick with its top at y=0. Scene grid snapping on with grid size 0.25 (handles must be Global for snapping).
- +Z = north. The camera looks from the south-west, so south (-Z) is screen bottom-right and the -X/-Z faces are the ones facing the camera.
- SquareScene greybox v1 (done 2026-10-27): lower square `Ground` 20×20, north `Terrace` 20×1×6 (z 4..10), `Stairs` 6×1×2 (4 steps, z 2..4) on the terrace's south face, a big tree (`Planter` 4×0.5×4 + `Tree_Trunk` + `Tree_Canopy`) at (0, -5), `Road_South` 6×10 entering from the south. Layout axis: south road → tree → stairs → terrace (like Witchbrook's academy square). `ScaleRef_Human` capsules (1.6 m) are scale dummies; disable them for play tests (they have colliders). Camera Size **8** (a person ≈ 1/10 of the screen height, like the reference). Directional Light rotation (50, -30, 0).
- Next: pixelation render step.

## 2D sprites in a 3D world — notes

- Pixel textures (sprites and Terrain layers): Filter Mode **Point**, Compression **None**, one consistent Pixels Per Unit; integer scales only (mixed pixel sizes look amateur).
- Directional sprites: switch frames by movement direction instead of rotating the sprite. Fixed camera → the facing can be computed from the input/velocity direction alone. West side can reuse the east side with `flipX`.
- Typical character setup: root with `Rigidbody` (rotation frozen) + `CapsuleCollider`; sprite on a child object turned to face the camera (billboard).
- Use Alpha Clip (cutout) materials for sprites in the 3D scene to avoid transparency sorting issues.
- Terrain layers blend smoothly at their borders, which can break the pixel look — a custom Terrain shader (stepped/dithered blending) may be needed later.

## Deploy (server, still running the OLD code)

- AWS Lightsail instance `dg-village-origin` (Ubuntu 24.04, 2 GB, $12/mo) in **Sydney** `ap-southeast-2` — the AWS free plan ($120 credits until **2027-04-20**) is locked to Sydney by an SCP; unlocking other regions requires a paid plan and removes spend limits. Static IP `13.54.176.157`, firewall 22/80/443. URL `https://13-54-176-157.sslip.io` (free HTTPS via sslip.io + Caddy).
- SSH: `ssh -i ~/.ssh/dg-village ubuntu@13.54.176.157`; repo cloned at `~/dg-village`, `deploy/.env` (secrets) only on the server.
- Old setup (in git history: `deploy/`, `python/Dockerfile`): Docker Compose (Caddy → server:8000, Postgres), `deploy/deploy.sh` = `git pull --ff-only` + `docker compose up -d --build --wait`, daily pg_dump backups via cron + Lightsail snapshots.
- **`.github/workflows/deploy.yml` still exists**: a push to `main` touching `python/**`, `deploy/**` or the workflow runs ruff + pytest in `python/` and then SSH-deploys. With `python/` and `deploy/` gone, merging the delete commit into `main` will fail the workflow (and a successful deploy would pull a tree with no server). Update or remove the workflow before merging to `main`.

## Unity gotchas (from the old project)

- Editor log: `unity/Logs/Editor.log` (grep `error CS` for compile errors).
- Editing `Packages/manifest.json` while the editor is open isn't picked up — use Package Manager "Add package from git URL" or restart the editor.
- Build Profiles Scene List edits are saved only on **File → Save Project**.
- `EditorSceneManager.NewScene(..., Single)` unloads assets not referenced by the new scene — load assets after creating the scene.
- Once testers have builds, never change Player Settings `Company Name` / `Product Name` (PlayerPrefs location depends on them).

## Unity conventions

- Our code under `unity/Assets/_Project/` (Scripts, Sprites, Materials, Prefabs, Scenes), apart from template/third-party assets.
- Commit `.meta` files with their assets. Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`.
- C#: PascalCase for types/methods, `_camelCase` for private fields, `[SerializeField] private` over public fields.
