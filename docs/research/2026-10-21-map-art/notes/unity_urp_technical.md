# Unity 6 (6000.6) / URP 17: 기술 구현 (2.5D 도트 맵 + 3D 씬 속 2D 스프라이트 캐릭터)

Context: fixed ortho camera (size 5, X 35°), XZ ground plane, SpriteRenderer characters tilted X 35°, 64x64 @ 32 PPU, Point, Alpha Clip. Desktop now, WebGL2 later.

Global version note: **Unity 6.4 removed URP Compatibility Mode (non-render-graph path) entirely, including the `URP_COMPATIBILITY_MODE` define** ([Unity 6.4 upgrade guide](https://docs.unity3d.com/Manual/UpgradeGuideUnity64.html)). The project runs 6000.6, so any renderer feature/pixelation tutorial using `ScriptableRenderPass.Execute` + `cmd.Blit`/`Blitter` without `RecordRenderGraph` (most 2019–2023 tutorials and repos) will **not work as-is**. Unity's own Compatibility-Mode blit sample page also says the non-render-graph path is no longer developed ([Unity 6.0 manual, fullscreen blit in Compatibility Mode](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/renderer-features/how-to-fullscreen-blit.html)).

## 1. Depth sorting of tilted sprites vs 3D geometry and vs each other; sprite shadows

### Takeaway
In a Universal (3D) Renderer scene, the robust approach is to treat sprites as **opaque alpha-clipped geometry that writes depth** (so the depth buffer, not sort order, resolves "walking behind trees/buildings"), and fix the "tilted quad pokes into walls" problem with a **vertex-shader depth rewrite** (bgolus' vertical-billboard trick). Shadows need a custom lit cutout shader (or Shader Graph Lit with Alpha Clip) plus Cast/Receive Shadows on the SpriteRenderer (exposed only in Debug inspector).

### Cited Findings
- Transparency Sort Mode (Perspective / Orthographic / Custom Axis + Transparency Sort Axis) is a property of the **Renderer 2D asset** in URP and applies to the transparent pass; older (2020–2022) manual pages put it under Project Settings > Graphics, which is the Built-in pipeline location (outdated for URP) — [Unity 6.3 URP manual (Setup)](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/Setup.html); [Unity 2022.3 sprites-sort (older)](https://docs.unity3d.com/2022.3/Documentation/Manual/sprites-sort.html)
- Jiaquarium/unity-URP-2.5D-lit-shader: lit sprite shader for a skewed/tilted ortho camera. It **rewrites each sprite's clip-space depth as if the sprite stood vertically** in the world (keeps the camera-facing look, only depth changes), so sprites stop clipping into nearby 3D meshes. Sprites can cast/receive realtime directional shadows; SpriteRenderer **Cast Shadows / Receive Shadows are only visible in Debug inspector mode**. Limitations: sprites render as opaque so sorting order is ignored; on mobile, shadows may still be received "as if clipped". Requires URP 10.2.1 / Unity 2020.2 (**outdated for URP 17; no license file visible**) — [GitHub](https://github.com/Jiaquarium/unity-URP-2.5D-lit-shader)
- bgolus' vertical-billboard depth trick (Unity Discussions thread): compute the depth of a camera-facing vertical plane through the sprite's pivot at each vertex's screen position and take the closer value: `float newPosZ = planeOutPos.z / planeOutPos.w * o.pos.w; o.pos.z = min(o.pos.z, newPosZ);` (non-reversed-Z branch). Needs a hand-written vertex/fragment shader; **cannot be cleanly ported to Shader Graph** (Shader Graph outputs object-space position, technique works in clip space; converting back warps textures, Feb 2025 discussion). A Mar 2025 post made a lit URP version with its own ShadowCaster and DepthOnly passes and fixed self-shadowing by computing shadow coords from the original (pre-billboard) world position; Aug 2025 post notes this makes received shadows slightly misplaced (unsolved) — [Unity Discussions, "2D billboard sprites clipping into 3D environment", p.4](https://discussions.unity.com/t/problem-solving-2d-billboard-sprites-clipping-into-3d-environment/743786?page=4)
- Billboard shadow self-shadowing: with both cast and receive on, the sprite's own shadow lands on itself; one approach splits a light-facing shadow quad from the camera-facing visual and adds a depth bias — [Unity Discussions, Shadowcaster that billboards towards light](https://discussions.unity.com/t/shadowcaster-that-billboards-towards-light/881036)
- Victor-Go/Unity-Sprite-Shader-for-URP: Shader Graph sprite shader that casts shadows; needs SpriteRenderer Cast Shadows enabled in Debug inspector and main/additional light shadows enabled on the URP asset — [GitHub](https://github.com/Victor-Go/Unity-Sprite-Shader-for-URP)
- URP Lit Shader Graph has Cast Shadows / Receive Shadows toggles; enabling Alpha Clipping adds the Alpha Clip Threshold block (default 0.5). Docs warn against alpha clipping when alpha and threshold are constant for the whole material (extra MSAA cost/artifacts) — not an issue for sprites with per-pixel alpha — [Unity 6 manual, Lit Shader Graph](https://docs.unity3d.com/jp/current/Manual/urp/prebuilt-shader-graphs-urp-lit.html); [Unlit Shader Graph](https://docs.unity3d.com/kr/current/Manual/urp/prebuilt-shader-graphs-urp-unlit.html)
- Old (2020) forum workaround: transparent surface + two-sided PBR graph for shadow-casting sprites — older URP, transparent surfaces don't write depth — [Unity forum](https://forum.unity.com/threads/urp-shader-for-sprites-to-cast-receive-shadows.950933/)
- Unity Issue Tracker: Forward/Forward+ rendering path causing lighting to not affect SpriteRenderer (sprite + 3D lit interplay has had bugs) — [Issue tracker](https://issuetracker.unity.com/issues/15963/rendering-path-forwardforward-causes-lighting-to-not-affect-spriterenderer)

### Inferences
- Current setup (Universal renderer + alpha-clip material + fixed X 35° tilt) is the right base. Because sprites are opaque+depth-writing, Transparency Sort Mode / Sorting Groups are mostly irrelevant; walking behind a tree happens via depth automatically. The remaining artifact is **the tilted quad's top half intersecting a wall/tree behind the player** (or the bottom half sinking into the ground slope). Options in order of effort: (a) keep sprites ~1 unit away from walls via colliders; (b) port bgolus' clip-space depth trick into a small hand-written URP HLSL shader (ForwardLit + ShadowCaster + DepthOnly/DepthNormals passes) — a good fit for a code-strong owner, and required anyway if outlines/SSAO-like effects read depth.
- Fixed tilt vs full billboard: with a fixed camera angle, a fixed X-tilt equal to the camera pitch is equivalent to a cylindrical billboard and avoids per-frame rotation; full spherical billboard only matters if the camera rotates (it won't).
- Shadows: simplest robust look for "old Korean RPG" style is **a blob/ellipse shadow decal or quad under the feet** (no shadow-map issues, works on WebGL), and let sprites *receive* shadows only. Real cast shadows from a tilted quad will be foreshortened/stretched and need the depth-bias/self-shadow handling above.
- Avoid the 2D Renderer route: see section 3 — it loses 3D lights.

### Gaps
- No official Unity 6 doc found on how SpriteRenderer sorting (sortingOrder vs camera distance) interacts with depth-writing alpha-clip materials in the Universal renderer; behavior inferred from general opaque rendering rules.
- No maintained, URP-17-verified open-source "2.5D sprite lit + depth-fix" shader found; Jiaquarium's is URP 10 era and must be ported (LitForwardPass includes changed across versions).

## 2. Pixel-consistent look (Pixel Perfect Camera, low-res RT + upscale, camera snapping, texel density)

### Takeaway
URP's Pixel Perfect Camera is documented as a **2D Renderer** feature; for a 3D Universal renderer scene, the standard technique is a **render-graph renderer feature that renders/downsamples to a low internal resolution, snaps the ortho camera to the texel grid, and upscales with a sub-pixel UV offset** (t3ssel8r-style). Two MIT Unity 6 URP repos already implement this.

### Cited Findings
- URP Pixel Perfect Camera: properties Asset PPU (must match every sprite), Reference Resolution (e.g. 320×180), Crop Frame (None/Pillarbox/Letterbox/Windowbox/Stretch Fill), Grid Snapping (None / Pixel Snapping / Upscale Render Texture), Filter Mode (Point vs Retro AA, Stretch Fill only). Upscale Render Texture renders to a temp texture at reference resolution then upscales. Docs say **don't install the separate 2D Pixel Perfect package (Built-in only; outdated)**. Docs describe it as part of the Universal 2D template and say nothing about 3D renderer use — [Unity 6.3 manual](https://docs.unity3d.com/Manual/urp/2d-pixelperfect-ref.html)
- Pixel Perfect package's pixel snapping only applies to sprites and only when Upscale Render Texture is disabled — [2D Pixel Perfect package docs](https://docs.unity3d.com/Packages/com.unity.2d.pixel-perfect@5.1/manual/index.html)
- **bababuyyy/unity-isometric-pixel-pipeline** (MIT, Unity 6 URP, t3ssel8r-inspired): Render Graph feature with 5 passes — copy color → downsample to internal res (default **640×360**) → outline detection at internal res (silhouette from depth over 8 neighbors w/ angle-adjusted threshold; crease from normals on 4 neighbors) → composite → **sharp upscale + panning offset**. Camera position **snaps to a texel grid in view space**; snap error passed to the upscale shader as UV offset so motion stays smooth. Includes `ToonLit` (quantized diffuse "Cuts", cloud shadows, optional Bayer dither, palette mode), GPU-instanced billboard grass (~35k) with wind sway, day/night. Requires SSAO disabled. Known limits: crease lines at object bases, no PBR — [GitHub](https://github.com/bababuyyy/unity-isometric-pixel-pipeline)
- **schyro/PixelPipeline** (MIT, Unity 6000.3+, URP 17, PC; mobile unsupported, WebGL not mentioned): low-res render + sharp upscale, depth/normal outlines, banded lighting with 1-pixel shadow edges, day/night + weather, cloud shadows, god rays, GPU-instanced billboard grass, leaf cards, water (banded depth color, foam, refraction, planar reflection), optional pixel-perfect ortho camera controller. Setup: git-URL package, **renderer set to Deferred+**, add Pixel Renderer Feature — [GitHub](https://github.com/schyro/PixelPipeline)
- UPixelator (paid, shaderless; URP 12/14 listed): render to lower-res RT and upscale; snap camera and tagged objects to a world-space pixel grid so the same pixels render while moving; apply sub-pixel offset in game resolution to hide camera shake; snapped moving objects still zig-zag (less noticeable at higher speed); **pixel-creep reduction works only with orthographic cameras** — [UPixelator docs](https://github.com/Radivarig/UPixelator_Documentation)
- "Texel Splatting" (arXiv, Mar 2026): under orthographic projection grid snapping works because all pixels shift equally; under perspective no single snap fixes all depths — [arXiv 2603.14587](https://arxiv.org/abs/2603.14587)
- HPSX URP Shader Graph Tools (PSX-style, resolution scaling, point filtering, dithering) — Unity 2019.4 only (**outdated**) — [GitHub](https://github.com/RubenTipparach/HPSXURP-Shader-Graph-Tools)
- DitherDown URP (itch, paid) uses URP's **Full Screen Pass Renderer Feature** with render scale matched to screen scale for pixelation — [itch.io](https://rtwarner.itch.io/ditherdown-urp)

### Inferences
- Concrete numbers for this project: ortho size 5 → visible height 10 world units. At 32 PPU that is **320 px tall** internal resolution (≈ 569×320 at 16:9). Sprites drawn at 1 texel = 1 screen-texel only if internal height = 2 × orthoSize × PPU. Pick internal res = 320 px tall (×3 = 960, ×4 = 1280 … not an integer for 1080p: 1080/320 = 3.375) — either change ortho size so 1080 / (2·size·32) is an integer (size 5.625 → 360 px → ×3 = 1080, ×2 = 720, ×4 = 1440) or accept letterboxing/crop. **Ortho size 5.625 with 640×360 internal is the clean choice** (matches bababuyyy's default).
- Camera tilt 35° foreshortens the ground: a ground texture at 32 texels/unit appears 32·sin(35°) ≈ 18 px/unit vertically on screen. To make ground texels look square at screen-pixel scale, either author ground textures with this squash in mind, or accept slight non-square texels (common in 3D-pixel games; the downsample pass makes everything consistent anyway). Upright surfaces (walls facing camera) at cos(35°)≈0.82 scale. Sprites tilted to face the camera show at full 32 px/unit.
- Snap the camera (not the player) to the internal texel grid **in camera space** (right/up axes), and pass the residual to the upscale pass; with SmoothDamp CameraFollow this is the step that removes shimmer. Characters moving at non-texel speeds will still subtly zig-zag; acceptable.
- Since characters are already pixel art, rendering everything at internal res also makes 3D map meshes look "pixel" and unifies line weight. Text/UI (name tags, chat bubbles) should render **after** the upscale (Screen Space Overlay already does; world-space TMP name tags would get pixelated — move them to screen space or a separate camera/layer drawn after the upscale).
- Writing our own render-graph feature (~200 lines: low-res color/depth texture, outline, upscale) is feasible; or vendor bababuyyy (MIT) as a reference. Verify both on 6000.6 and WebGL2 before adopting; PixelPipeline's Deferred+ requirement is a red flag for WebGL.

### Gaps
- No official Unity statement that Pixel Perfect Camera does/doesn't work with Universal (3D) renderer; docs are silent. Treat as unsupported.
- Neither MIT repo documents WebGL2 testing.

## 3. Lighting for pixel look (toon ramp, hard shadows, 2D vs 3D lights, baked, fog, LUT, bloom, outlines, day/night, WebGL perf)

### Takeaway
Stay on the **Universal (3D) Renderer** with 3D lights: 2D Lights don't affect 3D renderers, and 6.3's "3D meshes in 2D Renderer" mode drops 3D lighting and clears depth. Get the pixel look from a custom toon-lit shader (quantized N·L bands, hard shadows) + outline at internal resolution + cheap color grading (LUT). Keep to one shadowed directional light for WebGL.

### Cited Findings
- 3D and 2D Lights only affect 3D and 2D renderers respectively; 2D lighting does not affect Mesh Renderers and is not physically based — [Unity 6.3 manual, 2D lights intro](https://docs.unity3d.com/Manual/urp/Lights-2D-intro.html)
- Unity 6.3: 2D Renderer can now render Mesh/Skinned Mesh Renderers lit by 2D lights, sorted with sprites ("Sort 3D As 2D", Mesh2D-Lit-Default or Sprite Lit/Unlit/Custom Lit graphs). Limits: Sort 3D As 2D **clears the object's depth** (no depth-based post-FX) and **doesn't work with 3D lighting** — [Unity Discussions, Rendering 3D as 2D in 6.3](https://discussions.unity.com/t/rendering-3d-as-2d-in-unity-6-3-beta/1683244)
- URP Forward: max 9 lights per object; Forward+/Deferred for more — [Unity manual, light limits](https://docs.unity3d.com/Manual/urp/lighting/light-limits-in-urp.html)
- Toon references: bababuyyy `ToonLit` quantizes diffuse into bands ("Cuts"), cloud shadows, Bayer dither, palette mode — [GitHub](https://github.com/bababuyyy/unity-isometric-pixel-pipeline). PixelPipeline: banded light for sun/point/spot, 1-px shadow edges — [GitHub](https://github.com/schyro/PixelPipeline)
- Older toon repos: delt06/urp-toon-shader (ramp, shadows, inverted-hull outline) verified on Unity 2021.3 / URP 12.1.6, **no longer maintained** — [GitHub](https://github.com/delt06/urp-toon-shader); ColinLeung-NiloCat UnityURPToonLitShaderExample targets URP 10.3.2+ (**pre-URP 17, check**) — [GitHub](https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample)
- Daniel Ilett's Toon Shaders Pro (paid) has an outline renderer feature tagged render-graph — [itch.io](https://danielilett.itch.io/toon-shaders-pro-for-unity-urp)
- WebGL: Unity's supported web graphics API is WebGL2 — [Unity 6.6 manual, WebGL2](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/webgl/develop/graphics/web-gl2.md). WebGL2 has no compute shaders (GPU-compute features WebGPU-only, WebGPU still experimental per a Sep 2026 news summary — secondary source) — [Cinevva news](https://app.cinevva.com/fr/news/2026-09-01-unity-6-6-webgpu-production). Issue UUM-96046: render passes rendering incorrectly / GL_INVALID_OPERATION on WebGL2, versions 6.0–6.4, closed as "resolved indirectly" — [Unity Issue Tracker](https://issuetracker.unity.com/issues/7188/glinvalidoperation-errors-and-render-passes-do-not-render-correctly-when-building-for-webgl-with-webgl-2-graphics-api). A Unity 6.5 user reported shadow artifacts only in WebGL builds — [GameDev.tv community](https://community.gamedev.tv/t/render-with-artifacts-or-heavy-distortion-in-my-webgl-build/256770). Third-party (Luna playables) table: main light realtime shadows only on WebGL2, no additional-light shadows — not official — [Luna docs](https://docs.lunalabs.io/docs/playable/getting-started/supported-features/urp)

### Inferences
- Recommended stack: Forward (or Forward+) Universal renderer; **one directional light with soft shadows OFF (hard), low shadow resolution matched to the pixel grid**; custom toon shader with 2–3 bands (N·L step + shadow attenuation step); ambient from gradient/skybox color rather than baked GI initially; outlines computed at internal resolution from depth+normals (needs DepthNormals prepass — costs a pass on WebGL); color grading with a LUT via URP Volume (cheap, single fullscreen pass); bloom only if needed (multi-pass, expensive on low-end WebGL). Fog: URP built-in linear/exponential fog is per-shader and cheap.
- Day/night: animate directional light color/rotation + ambient color + a LUT/color-filter blend in a script; the server could broadcast a shared time-of-day so all clients match.
- Running everything at 640×360 internal makes fragment-heavy effects cheap — a big win for WebGL.
- Sprites need the same toon lighting to sit in the scene; easiest is to light sprites with flat N = world up (or camera-facing) normal + receive-shadow step, so they don't go dark when the sun is low.

### Gaps
- No official Unity 6 doc found listing which URP post-processing effects are supported/expensive on WebGL2; must be tested on a WebGL build.
- No free, verified URP-17 render-graph toon+outline package found besides the two MIT repos above.

## 4. Building map geometry (ProBuilder, Tilemap on XZ, decals, Polybrush, terrain vs meshes, walkable bounds)

### Takeaway
Use **Tilemap with Orientation XZ + GameObject Brush** (or a simple grid snapping script) to place modular 3D tile prefabs, ProBuilder for one-off buildings/blockouts, and simple colliders (box colliders for buildings/props + invisible boundary walls) for walkable bounds; NavMesh is unnecessary because players move by direct input.

### Cited Findings
- Tilemap component Orientation supports XY, **XZ**, YZ, ZX, ZY planes — useful for 3D views — [Unity 2019.3 Tilemap manual (still applicable concept)](https://docs.unity3d.com/2019.3/Documentation/Manual/class-Tilemap.html)
- GameObject Brush (2D Tilemap Extras): paints GameObject instances onto grid cells, instance must be child of the active grid; "Lock Z Position" option — [Tilemap Extras 8.0, GameObjectBrush](https://docs.unity3d.com/Packages/com.unity.2d.tilemap.extras@8.0/manual/GameObjectBrush.html)
- PixelPipeline grass places itself by raycasting onto a collider-based ground in a spawn box (implies mesh ground + colliders rather than Terrain) — [GitHub](https://github.com/schyro/PixelPipeline)

### Inferences
- Ground: rather than one flat plane, build a grid of 1×1 (or 2×2) unit tile meshes/quads with pixel textures (grass, path, stone) at 32 px/unit, painted via Tilemap XZ + GameObject Brush, or a single mesh generated by an editor script from a tile map (fewer draw calls — better for WebGL; SRP Batcher/GPU instancing helps either way). Edges/cliffs as simple box/stepped meshes give the "2.5D" depth that the flat plane lacks.
- Unity Terrain is a poor fit: its splat blending and LOD fight a pixel look and add WebGL cost.
- Decals (URP Decal Renderer Feature) for path/dirt variation are possible but cost a pass; vertex color painting (Polybrush) on ground meshes is cheaper for tint variation.
- Walkable bounds: since `PlayerMovement` moves by transform, add a CharacterController or Rigidbody (kinematic + sweep) and box colliders on obstacles; or a 2D grid "walkable mask" checked on the client and server (server-authoritative bounds is a natural fit with the existing Python server).

### Gaps
- Did not verify ProBuilder / Polybrush package status for Unity 6.6 or URP 17 decal performance on WebGL2 in this pass.
- Did not find the Unity 6 version of the Tilemap orientation page (cited page is 2019.3; feature still exists but verify UI).

## 5. Ambient life (water/grass shaders, particles, cloud shadows)

### Takeaway
Cheap, high-impact: vertex-shader wind sway on grass/foliage cards, scrolling-noise cloud shadows multiplied into the toon shader, banded water with scrolling foam, and a few particle systems (leaves, fireflies, butterflies) — all already implemented in the two MIT repos for reference.

### Cited Findings
- bababuyyy: GPU-instanced billboard grass (~35k blades in demo) inheriting terrain patch color, wind sway, cloud shadows, accent sprites; grass excluded from depth/normal passes so it doesn't produce outlines; cloud shadows with fixed direction — [GitHub](https://github.com/bababuyyy/unity-isometric-pixel-pipeline)
- PixelPipeline: shared wind across grass/foliage/clouds/rain/water; moving cloud shadows; god rays; pixel rain with splashes; camera-facing leaf cards (`PixelFoliageCanopy`); water with banded depth color, refraction, shore foam, glints, waves, planar reflection — [GitHub](https://github.com/schyro/PixelPipeline)
- Shader Graph vertex displacement: set Vertex Position block space correctly (Object vs Absolute World) and ensure URP target is active — low-quality secondary source, verify — [bugnet blog](https://bugnet.io/blog/fix-unity-shader-graph-master-stack-vertex-output-ignored)
- WebGL2 has no compute shaders — [Cinevva news (secondary)](https://app.cinevva.com/fr/news/2026-09-01-unity-6-6-webgpu-production)

### Inferences
- GPU-instanced grass via `Graphics.RenderMeshInstanced` (not compute) works on WebGL2; avoid compute-based grass/particles (VFX Graph needs compute → use the built-in Particle System / Shuriken for WebGL).
- Cloud shadows: sample a scrolling tiled noise texture by world XZ in the ground/toon shader and multiply light — one texture fetch, no extra pass.
- Water: on WebGL avoid planar reflection and refraction (extra camera/opaque texture); a banded color + scrolling foam + depth-fade (needs depth texture) is enough at pixel resolution.
- Wind sway for sprites (trees as sprite cards): offset top vertices by `sin(time + worldPos.x)` × UV.y in the vertex shader; quantize the offset to the texel size to avoid sub-pixel smearing.

### Gaps
- Did not retrieve Cyanilux or Daniel Ilett free tutorials (search returned no direct hits in this pass); no verified free Unity-6-specific grass sway tutorial cited.
- No measured WebGL performance numbers for any of these effects.
