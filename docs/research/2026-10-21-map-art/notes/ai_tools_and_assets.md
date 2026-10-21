# AI Tools and Asset Packs for Pixel-Art Map/Environment Art (2.5D, Unity) — research notes, 2026-10-21

Context: Unity 6 URP, fixed ortho camera tilted ~35°, characters 64x64 px @ 32 PPU from PixelLab ("Low Top-Down", 48px base, single-color dark outline, cel shading). Maps = small social spaces (plaza, café, park, rooms).

Source-quality note: the PixelLab facts below come mostly from PixelLab's own **live OpenAPI spec** (`https://api.pixellab.ai/v2/openapi.json`, fetched 2026-10-21), the most authoritative and current source found. PixelLab's pricing page renders client-side and could not be read, so its prices come from a third-party guide dated Sep 2026.

## PixelLab: map/environment features, views, pricing, export

### Takeaway
PixelLab is the strongest fit because it already made the characters. It has a dedicated Wang tileset generator with a **"low top-down" view option** (the same view as the characters), a map-object generator (props, transparent background, "low top-down" view, outline/shading/detail presets, forced palette, inpainting onto a background image), "Tiles Pro" (square/isometric/oblique/hex tiles plus road autotiles and **building kits** with walls/doorways/stairs), isometric tiles, and an inpainting-based "Create Map" tool. Entry paid tier is reportedly $12/mo for 2,000 generations (third-party, Sep 2026).

### Cited Findings
**Tilesets (`POST /create-tileset`, "top-down tileset")**
- Generates a Wang tileset with two terrain levels, "lower" (e.g. grass, water) and "upper" (e.g. dirt path, stone), plus an optional transition description. Output is 16 tiles, or 25 when `transition_size=1.0`. — [PixelLab API OpenAPI spec](https://api.pixellab.ai/v2/openapi.json)
- `view` options: `"low top-down"` and `"high top-down"` (default high top-down). — [PixelLab OpenAPI](https://api.pixellab.ai/v2/openapi.json)
- `tile_size`: 16 or 32 px in `mode="standard"`; 64 px needs `mode="pro"` ("richer painted tilesets, 16/32/64px tiles"). — [PixelLab OpenAPI](https://api.pixellab.ai/v2/openapi.json)
- Style controls: `outline`, `shading`, `detail` presets; per-terrain `lower_reference_image` / `upper_reference_image` / `transition_reference_image`; `color_image` (palette reference); `target_palette` (exact hex colours and/or a max colour count); `seed`. Pro-mode shape controls: `raggedness`, `roundness`, `boundary_amplitude`, `step_slope` (cliff vs. slope for raised sets), `connect_diagonals`. — [PixelLab OpenAPI](https://api.pixellab.ai/v2/openapi.json)
- `lower_base_tile_id` / `upper_base_tile_id` let a new tileset reuse a terrain from an earlier one, so tilesets can be chained (grass→dirt, dirt→stone) with matching base tiles. — [PixelLab OpenAPI](https://api.pixellab.ai/v2/openapi.json)
- PixelLab's own API intro lists "Build a connected map tileset — `POST /create-tileset`. Wang tiles whose edges line up, for top-down maps and platformers" as one of its four core features. A separate sidescroller tileset endpoint also exists (`/create-tileset-sidescroller`). — [PixelLab OpenAPI](https://api.pixellab.ai/v2/openapi.json), [PixelLab llms.txt](https://api.pixellab.ai/v2/llms.txt)

**Map objects (`POST /map-objects`)**
- "Creates a pixel art object with transparent background for game maps", takes ~15–30 s. Size 32–400 px on each side, any aspect ratio. — [PixelLab OpenAPI](https://api.pixellab.ai/v2/openapi.json)
- `view`: `"low top-down"`, `"high top-down"`, `"side"`. `outline`: `single color outline` (default) / `selective outline` / `lineless`. `shading`: flat / basic / medium / detailed. `detail`: low / medium / high. Also `init_image` with strength, `color_image` (forced palette), and `background_image` + `inpainting` (mask/oval/rectangle), which paints an object into an existing map image to match its style. — [PixelLab OpenAPI](https://api.pixellab.ai/v2/openapi.json)
- A third-party wiki for a tool built on PixelLab says map objects suit chests, doors, switches, trees, rocks, barrels, crates and furniture, including alternate states (open/closed, lit/unlit). — [Sorceress/WizardGenie wiki](https://sorceress.games/wiki/wizard-genie/pixellab-and-sprites)

**Tiles Pro (`POST /create-tiles-pro`, Pro model)**
- `tile_type`: hex, hex_pointy, isometric, oblique, octagon, square_topdown. `tile_size` 16–128 px (32 recommended). `tile_view`: top-down (no depth) / high top-down (~15% depth) / **low top-down (~30% depth, default)** / side (~50%). A continuous `tile_view_angle` (0–90°) is also available. — [PixelLab OpenAPI](https://api.pixellab.ai/v2/openapi.json)
- `tile_feature`: `roads` = 18-configuration path autotile set (square_topdown exactly 32 px, isometric); `tileset` = 16-tile corner Wang set; `building` = construction kit with floor, connectable wall segments, doorways, pillar and staircase. Building options: wall height 1–3 tiles, wall/floor/roof material descriptions, and `building_wall_angle` (wall height as its own camera angle, separate from the ground pitch). Connectable sets return per-tile placement rules. — [PixelLab OpenAPI](https://api.pixellab.ai/v2/openapi.json)
- `style_images` + `style_options` (copy palette / outline / detail / shading from reference tiles), but this "cannot be combined with" `tile_feature`. `target_palette` is supported. The spec itself says that for square top-down terrain transitions, `/create-tileset` is the dedicated model. — [PixelLab OpenAPI](https://api.pixellab.ai/v2/openapi.json)

**Isometric tiles (`POST /create-isometric-tile`)**
- Area from 16x16 to 64x64 ("sizes above 24x24 often produce better quality"). Tile size 16 or 32. Shape: thin tile / thick tile / block. Default outline `lineless`. — [PixelLab OpenAPI](https://api.pixellab.ai/v2/openapi.json)

**Create Map / inpainting**
- The Create Map guide builds a map area by area with inpainting (select a 4x4-tile area, paint black on the inpaint layer, describe it, then extend with overlapping selections). "The model can only see what is inside the selection." The example model "is trained on 16x16 pixel tiles". — [PixelLab docs: Creating maps](https://www.pixellab.ai/docs/guides/map-tiles)
- The docs index lists Create map (pixflux), Extend map (v2), Create texture, Create tileset, Create isometric tile, Create tiles (Pro), several inpaint versions, and "Create images from style references (Pro)". — [PixelLab docs](https://www.pixellab.ai/docs)
- The API recommends running user-supplied reference art through `POST /unzoom` first: "Upscaled pixel art confuses every one of these models, and this is the most common cause of disappointing output." — [PixelLab OpenAPI](https://api.pixellab.ai/v2/openapi.json)

**Integration**
- Python client (`pip install pixellab`), REST API, an MCP server for AI assistants, and an Aseprite plugin. The MCP exposes `create_tileset` (Wang) and `create_isometric_tile`. — [pixellab-mcp GitHub](https://github.com/pixellab-code/pixellab-mcp), [PixelLab OpenAPI](https://api.pixellab.ai/v2/openapi.json), [Cinevva guide](https://app.cinevva.com/guides/ai-pixel-art-generators)
- No official Unity importer was found. Outputs are PNG images or tile sheets, and connectable sets include placement metadata in the API response. — [PixelLab OpenAPI](https://api.pixellab.ai/v2/openapi.json)

**Pricing**
- Free trial: 40 fast generations, no card. Pixel Apprentice $12/mo (2,000 generations). Pixel Artisan $24/mo (priority queue, up to 400×400). Pixel Architect $50/mo (10,000 generations, 20 concurrent jobs). Annual billing saves about 25%. — [Cinevva, "Best AI Pixel Art and Sprite Generators for Games in 2026", updated Sep 2026](https://app.cinevva.com/guides/ai-pixel-art-generators)
- Other sources disagree: $10/mo with 1,000 generations, and a free tier of "40 fast then 5 slow per day". These are probably older plan versions. — [cutout.pro](https://www.cutout.pro/learn/pixellab/), [Knowara](https://knowara.com/ai-tools/image/pixellab-review/)
- Billing has two currencies, "subscription generations" and USD credits. Example: a pixel font costs "15 subscription generations or at least $0.115 in credits". Pro endpoints "run on a stronger, slower, more expensive model". — [PixelLab OpenAPI](https://api.pixellab.ai/v2/openapi.json)

### Inferences
- For dg-village, use `create-tileset` with `view="low top-down"`, `tile_size=32` (1 tile = 1 Unity unit at 32 PPU), and outline/shading presets matching the characters. Feed `target_palette` or `color_image` from the girl01/boy01 sprites to lock colours.
- `map-objects` with `view="low top-down"` and `single color outline` is the natural way to make props (benches, fountain, trees, café tables) as billboard sprites in the 3D scene, like the characters.
- A Tiles Pro building kit could provide café/room walls and floors, but style references can't be combined with kits, so expect palette touch-up in Aseprite.
- The low top-down perspective (~30% depth) baked into tiles may clash with a real 3D-tilted ortho camera (35°). Flat ground tiles on a plane look fine. Vertical objects (walls, trees) should be upright billboard sprites rather than painted-in-perspective tiles laid flat, or the perspective gets applied twice.

### Gaps
- PixelLab's official pricing page could not be read (it loads client-side). Current tier prices and per-endpoint generation costs (e.g. how many generations a tileset costs) are unverified.
- Commercial-use terms for each tier were not verified (Cinevva: "check current terms for the tier before shipping").
- Whether free-plan users can use Pro endpoints (Tiles Pro, tileset pro mode) is unknown.

## Other 2D pixel generators (Retro Diffusion, Scenario, Sprite-AI, Ludo, Layer, etc.)

### Takeaway
Retro Diffusion is the only other tool with first-class tileset and top-down/isometric pixel styles plus custom user styles from a reference image, at pay-per-image prices ($0.03–$0.18/image, $0.10/tileset). Scenario and Layer offer custom model training but aren't pixel- or tileset-specific. Sprite-AI and Sprixen don't do tilesets. Scenario's and Leonardo's free tiers are non-commercial.

### Cited Findings
**Retro Diffusion**
- Model families and API prices: RD Fast $0.03/image, RD Plus $0.06, RD Pro $0.18 (fps_weapon $0.25). Tiles (`single_tile`, `tile_variation`, `tile_object`, `scene_object`) $0.06 each. **Tileset $0.10**. `check_cost: true` returns the price for free. — [Retro-Diffusion api-examples (GitHub)](https://github.com/Retro-Diffusion/api-examples)
- Environment-relevant styles:
  - `rd_tile__tileset`: tile size 16–32 px; builds a Wang-style tileset from one prompt. `rd_tile__tileset_advanced` takes inside/outside textures and optional input images.
  - `rd_tile__tile_object` (16–96 px) and `rd_tile__scene_object` (64–384 px).
  - `rd_plus__topdown_map`, `topdown_asset`, `topdown_item`, `isometric`, `isometric_asset`.
  - `rd_pro__topdown`, `rd_pro__isometric`, `rd_pro__dungeon_map`, `rd_pro__hexagonal_tiles`, `rd_pro__pixelate` (converts an input image to pixel art).
  — [Retro-Diffusion api-examples](https://github.com/Retro-Diffusion/api-examples)
- Consistency features: RD Pro accepts up to 9 `reference_images`. `input_palette` constrains output to a palette image. User styles (`POST /v2/styles`) are built from exactly one reference image plus a prompt template, with optional `force_palette`. — [Retro-Diffusion api-examples](https://github.com/Retro-Diffusion/api-examples)
- Aseprite extension: $20 (Lite) or $65 (full), one-time, runs locally, needs a reasonable GPU. Web app: 50 free credits, packs from $5 (250 credits) to $50 (3,500 credits). "Local output is yours." — [Cinevva, Sep 2026](https://app.cinevva.com/guides/ai-pixel-art-generators)
- Prepaid, credits don't expire, no subscription. — [mcpservers.org listing of Retro Diffusion MCP](https://mcpservers.org/ko/servers/retro-diffusion/retro-diffusion-mcp)

**Scenario**
- Free: 50 credits/day, personal and evaluation use only. Starter $15/mo (1,500 credits). Pro $45/mo (5,000 credits, custom model training). Max $75/mo (10,000). Annual billing saves 33%. Commercial rights on paid plans. The pricing page doesn't mention pixel art or tilesets. — [Scenario pricing](https://www.scenario.com/pricing) (fetched 2026-10-21)

**Ludo.ai**
- No free tier. Indie $15/mo billed annually (3,000 credits/year), Pro $35/mo, Studio $300/mo. 30+ style presets including 8-bit and 16-bit. Tilesets are not a listed feature. — [Cinevva, Sep 2026](https://app.cinevva.com/guides/ai-pixel-art-generators)

**Layer.ai**
- Consumption-based, from $10/mo for 300 creative units. Custom-trained models. Commercial license on every plan. — [Cinevva, Sep 2026](https://app.cinevva.com/guides/ai-pixel-art-generators)
- Layer also hosts third-party 3D models such as Tencent Hunyuan3D 3.0. — [Layer: Hunyuan3D 3.0](https://www.layer.ai/models/tencent-hunyuan3d-3-0)

**Sprite-AI and Sprixen**
- Sprite-AI: free (15 generations), Creator $8, Studio $24, Production $50/mo. Static props, items and icons only; no tilesets; style not enforced across sprites. Sprixen: $10/mo, "Style Lock" pins palette, resolution and proportions; no tilesets. — [Cinevva, Sep 2026](https://app.cinevva.com/guides/ai-pixel-art-generators)

**SpriteCook**
- Top-down mode outputs a 15-piece autotile sheet (4x4 layout) at 16 or 32 px tiles. — [SpriteCook blog, June 2026](https://www.spritecook.ai/blog/top-down-tileset)

**Leonardo**
- Free: 150 tokens/day, non-commercial. Paid from $12/mo. Commercial rights on paid plans. — [Cinevva, Sep 2026](https://app.cinevva.com/guides/ai-pixel-art-generators)

**Steam**
- Steam requires disclosing AI-generated assets on the store page. — [Cinevva, Sep 2026](https://app.cinevva.com/guides/ai-pixel-art-generators)

### Inferences
- Retro Diffusion is a good secondary or alternative tool, especially for one-off props (`tile_object` / `scene_object` at $0.06), whole-scene concept maps (`topdown_map`), and pixelizing images from Midjourney or ChatGPT (`rd_pro__pixelate`). A user style made from a girl01 sprite plus `input_palette` is the main consistency lever.
- For a hobby budget, pay-per-use Retro Diffusion ($5–$10 of credits) suits occasional props. PixelLab's subscription is better value if many tiles and objects are made in one month.

### Gaps
- Retro Diffusion's commercial license text for web/API outputs was not found. Only "local output is yours" (Aseprite extension) is sourced.
- The Midjourney/ChatGPT → pixelization pipeline wasn't researched in depth beyond `rd_pro__pixelate`. No sourced quality comparisons were found.
- No Reddit r/aigamedev or r/PixelArt threads surfaced in search (search tool returned none).
- Pixelicious was not researched.

## 3D generators (Meshy, Tripo, Hunyuan3D, Rodin) for low-poly props/buildings

### Takeaway
Meshy and Tripo both export Unity-ready formats and offer some low-poly or remesh options. Neither source documents a "pixel-art texture" mode, so pixel-style textures would need post-processing (texture downscale + Point filter + palette), or the map would use flat pixel sprites instead. Pricing data is partly in KRW or contradictory.

### Cited Findings
**Meshy**
- Tiers (KRW, as shown to a Korean visitor 2026-10-21):

  | Tier | Price/mo | Credits/mo |
  |---|---|---|
  | Free | ₩0 | 100 |
  | Pro | ₩28,869 | 1,000 |
  | Premium | ₩58,000 | 3,000 |
  | Ultra | ₩144,000 | 8,000 |
  | Studio | ₩100,000 | 5,500, shared |

  20% off annual plans; 50% off the first month. — [Meshy pricing](https://www.meshy.ai/pricing)
- Free-plan assets are licensed **CC BY 4.0** (attribution required). Paid plans have "Private & Customer owned" assets. — [Meshy pricing](https://www.meshy.ai/pricing)
- Exports: .fbx, .obj, .usdz, .glb, .stl, .blend. Remesh is available on Pro and above, not Free. A "Low Poly Generator" feature is linked, but its details weren't readable (feature page returned 404). — [Meshy pricing](https://www.meshy.ai/pricing)
- In USD: Meshy Pro $20/mo for 1,000 credits; a textured model costs ~20 credits (10 model + 10 texture), so ~50 models/mo, ~$0.40/model. This article is dated **Sep 2025 (older)**. — [Sloyd 3D AI price comparison](https://sloyd.ai/blog/3d-ai-price-comparison)

**Tripo**
- Professional $15.90/mo for 3,000 credits. An HD-textured image-to-3D model costs 40 credits (+5 each for style or quad), so ~75 models/mo, ~$0.21/model. Dated **Sep 2025 (older)**. — [Sloyd comparison](https://sloyd.ai/blog/3d-ai-price-comparison)
- Conflicting 2026 data: an aggregator captured 2026-10-14 lists Pro at $20/mo (annual), Team $55, Max $90, credit packs $10–$1,000. A Jan 2026 Tripo blog post says free = 300 credits and Pro = 3,000. A Japanese guide mentions "Smart Low-Poly" on a higher tier. The official pricing page returned 403 to the fetch tool. — [WebSearch summary of pricingsaas / aipicks.jp / droid.tools results](https://pricingsaas.com/companies/tripo3d), [aipicks.jp](https://aipicks.jp/mag/tripo-ai-guide-2026)

**Sloyd**
- $15/mo flat, unlimited generations (parametric and AI generator aimed at low-poly game assets). — [Sloyd comparison](https://sloyd.ai/blog/3d-ai-price-comparison)

**Hunyuan3D 3.0**
- Available via Layer, which says exports are compatible with Unity, Unreal and Blender; generation is paid per use on that platform. — [Layer: Hunyuan3D 3.0](https://www.layer.ai/models/tencent-hunyuan3d-3-0)

### Inferences
- With the current art direction (3D ground + 2D pixel sprite characters, fixed camera), AI 3D models with smooth PBR-like textures would likely clash with 32-PPU pixel sprites. Safer options:
  - (a) keep the 3D layer to simple geometry (ground plane, low boxes for building volumes) textured with PixelLab/Retro Diffusion pixel tiles at Point filtering, or
  - (b) make props and buildings as camera-facing pixel sprites (PixelLab map objects, low top-down view).
- Use Meshy or Tripo only for blockout/reference, or with downscaled textures (e.g. 64–128 px textures, Point filter, palette-quantized).
- On Meshy's free plan, CC BY 4.0 means a credit line is required in the game.

### Gaps
- No source confirmed whether Meshy, Tripo or Rodin can output pixel-art or hand-painted texture styles directly, or what their low-poly polycount targets are.
- Rodin (Hyper3D) and Hunyuan3D pricing and licensing were not found. Hunyuan3D's open-weight license terms (regional/scale restrictions in earlier versions) are unverified for 3.0.
- Official 2026 Tripo prices couldn't be fetched (403).

## Asset packs matching cute RPG pixel style

### Takeaway
LimeZu Modern Exteriors and Interiors are cheap (Exteriors $5, on sale for $2.50 in Oct 2026), come in 16/32/48 px, and allow commercial use with credit. They fit a modern Gather-like plaza or café, but their style is "modern cozy", not Ragnarok fantasy. Seliel's Mana Seed packs fit the fantasy-village look but are 16x16-based. **Their license reportedly forbids mixing with generative-AI content in the same product**, a direct conflict with dg-village's AI-generated characters.

### Cited Findings
**LimeZu Modern Exteriors**
- List $5.00, $2.50 during the "Exteriors Fall Sale 2026". Available in 16x16, 32x32 and 48x48. Top-down with RPG Maker MV/MZ files, autotiles for Godot and GameMaker. Contents: streets, buildings, props, animated vehicles.
- License: may "edit and use the asset in any commercial or non commercial project"; may not resell or redistribute; credit to LimeZu required. The page states "No generative AI was used" but says nothing about mixing with AI assets.
- Last devlog update: 2026-01-03.
- Source: [LimeZu Modern Exteriors (itch.io)](https://limezu.itch.io/modernexteriors)

**Seliel the Shaper (Mana Seed)**
- Prices: Thatch/Half-Timber/Stonework/Timber Roof Home tilesets $17.99 each; Iconic Homestead $19.99; Cozy Furnishings (250+ interior objects) $16.99; Village Accessories $7.99; Fences & Walls $7.99; Royal Garden $13.99; Grand Library $14.99 (16x16); Growable Trees $9.99. — [Seliel the Shaper itch.io](https://seliel-the-shaper.itch.io/)
- Creator, in itch.io comments: using the art in a product alongside generative AI content "would be a violation of the Mana Seed User License"; the art may only sit alongside original human-created content. Commercial use is allowed, credit optional. License text: [Mana Seed User License](https://selieltheshaper.weebly.com/user-license.html) (not fetched). — [itch.io comment threads](https://itch.io/post/14276406), [Iconic Interiors comments](https://seliel-the-shaper.itch.io/iconic-interiors/comments)

### Inferences
- **Mana Seed is likely off-limits for dg-village**, since characters are PixelLab-generated. Verify against the license text before buying.
- Pixel scale: characters are 64 px tall at 32 PPU (= 2 units). With 32x32 LimeZu tiles at 32 PPU, 1 tile = 1 unit, and a character is about 2 tiles tall, which matches the classic RPG Maker proportion (48 px chars on 48 tiles is closer to 1:1). The 48x48 variant at 32 PPU would make props 1.5x relative to characters. Use 32x32 at 32 PPU for consistent pixel density.
- LimeZu's flat top-down perspective (front-facing walls) vs. PixelLab's "low top-down" is close enough for RPG-style maps, but its palette differs from girl01's saturated colours.

### Gaps
- Not researched with sources: Kenney (commonly CC0), Cainos "Pixel Art Top Down" packs, Synty POLYGON (low-poly 3D, not pixel), Humble bundles, Unity Asset Store pixel village packs. Prices and licenses unverified.
- LimeZu Modern Interiors price and size not fetched (presumably similar 16/32/48 px structure).
- Whether LimeZu or other packs forbid combining with AI-generated content was not confirmed.
- No pack specifically in "Ragnarok/Tales Weaver" style was found. Ragnarok itself uses 3D maps with 2D sprites, so packs in that exact style are rare.

## Keeping AI environment art consistent with characters

### Takeaway
The tools themselves expose the main levers: same view preset ("low top-down"), same outline preset (single colour outline), the character's palette as `target_palette` or `color_image`, reference images, fixed seeds, and one pixel density (32 px tiles at 32 PPU, Point filter). PixelLab warns that upscaled reference art is the top cause of bad results.

### Cited Findings
- PixelLab tileset and map-object controls include `view` (low/high top-down), `outline` / `shading` / `detail` presets, `color_image` (forced palette), `target_palette` (exact hex list + `max_colors`), per-terrain reference images, and `seed`. — [PixelLab OpenAPI](https://api.pixellab.ai/v2/openapi.json)
- Tiles Pro `style_options` can copy colour palette, outline, detail and shading from reference tiles. — [PixelLab OpenAPI](https://api.pixellab.ai/v2/openapi.json)
- Run reference art through `/unzoom`, since "Upscaled pixel art confuses every one of these models". — [PixelLab OpenAPI](https://api.pixellab.ai/v2/openapi.json)
- Retro Diffusion offers `input_palette`, up to 9 reference images (RD Pro), and user styles with `force_palette`. — [Retro-Diffusion api-examples](https://github.com/Retro-Diffusion/api-examples)
- Sprixen's "Style Lock" pins palette, resolution and proportions. Sprite-AI does not enforce style across sprites. — [Cinevva, Sep 2026](https://app.cinevva.com/guides/ai-pixel-art-generators)

### Inferences
- Build a master palette (e.g. extract 32–48 colours from girl01 + boy01 sprites and add some environment greens and browns). Pass it to every generation and quantize in Aseprite afterwards.
- Generate all ground tiles at 32 px with the same view, outline and shading presets. Keep the existing importer settings (Point, no compression, 32 PPU) for `Sprites/` so pixel density matches.
- Keep outline rules consistent: characters have a dark single-colour outline, so props should too. Ground tiles are usually lineless or selective, so characters and props pop against the ground.

### Gaps
- No community-tested write-ups (Reddit/YouTube) on matching PixelLab environments to PixelLab characters were found.

## Common failure modes of AI tilesets and fixes

### Takeaway
Typical problems:
- edge mismatch and visible seams, often from an AI "vignette" darkening tile edges
- 1 px grid offsets and stray border pixels that break autotiling
- missing corner variants
- perspective mismatch between tile view and camera
- off-grid "fake pixels" from upscaled or non-native outputs

Fixes: dedicated Wang/autotile generators, offset-and-repaint seams, `/unzoom` or pixelate passes, palette quantization, and checking that every corner variant exists.

### Cited Findings
- A tile only works if its right column matches its neighbour's left column, and top/bottom likewise. Common models add a brightness falloff toward the edges that breaks repeating tiles; crop or remove the vignette. Manual fix: offset by half the width and height, paint over the seams, offset back. 64x64 is easier than 16x16 for seamlessness. — [Sprite-AI: seamless pixel art tiles](https://www.sprite-ai.art/blog/seamless-pixel-art-tiles), [Sprite-AI guide](https://www.sprite-ai.art/guides/seamless-tiles)
- Itch.io tileset comment threads (human-made packs) report tiles shifted by 1 px, stray pixels on borders creating unwanted tiles in Godot, and missing corners that break autotiling. — [rhosgfx RPG Overworld comments](https://rhosgfx.itch.io/rpg-overworld/comments), [itch.io thread](https://itch.io/t/532027/some-oddities)
- PixelLab's Create Map guide: the model sees only the current selection, so extend with overlapping selections, and expect retries. — [PixelLab docs](https://www.pixellab.ai/docs/guides/map-tiles)
- PixelLab tileset endpoints have `tileset_adherence` / `tile_strength` parameters to enforce tile structure. Tiles Pro offers `outline_mode="segmentation"`, described as "giving cleaner seamless edges". — [PixelLab OpenAPI](https://api.pixellab.ai/v2/openapi.json)

### Inferences
- Perspective: PixelLab "low top-down" tiles assume ~30% depth. In dg-village's 3D-tilted camera, flat ground tiles on a plane are fine. Walls and props should be sprite billboards (like characters, rotated X 35°) or real 3D geometry, not flat tiles with painted perspective.
- Validate in Unity with a Tilemap or a simple test grid at 32 PPU. Look for seams, which can also come from texture bleeding: use a sprite atlas with padding, or Pixel Perfect settings.

### Gaps
- No sourced community discussion was found specifically on AI-tileset perspective mismatch. Recommendations here are inference.
