# 2.5D pixel-art map rendering approaches for dg-village

Scope: comparing (a) 3D low-poly + pixel/hand-painted textures + 2D sprites (RO / HD-2D), (b) 2D painted or tilemap backgrounds, (c) pixel-perfect 3D (low-res render texture + outlines), (d) all-sprite 2.5D (billboards on a ground plane). Project context: Unity 6 URP, fixed ortho camera tilted ~35°, 64x64 chibi sprites at 32 PPU (~1.5 units tall), Point filter, small social maps.

Research note (2026-10-21): about 16 searches and fetches. Primary developer sources on rendering pipelines were scarce for RO, Tales Weaver, Eastward and Don't Starve. Several key points below are therefore labelled as inference or as community or forum claims.

---

## Q1. How did Ragnarok Online / Tales Weaver build their maps, and what makes them look good?

### Takeaway
Ragnarok Online used approach (a). The terrain is a 3D heightfield grid textured with small tiling textures at about 32 px per tile. Each surface has an 8x8 baked lightmap posterized to 16 levels, plus per-tile vertex colours. 3D props (RSM models) sit on top, and characters are 8-direction 2D sprites. Tales Weaver was deliberately 2D, but no primary source on its map pipeline was found.

### Cited Findings
- RO map data is split across files. GND holds the static terrain geometry, textures and lightmaps. GAT holds altitude and walkability types (ground, cliff, water). RSW places 3D objects, water, lights, sounds and effect emitters. — [RagnarokFileFormats README](https://github.com/rdw-archive/RagnarokFileFormats/blob/master/README.MD); [GND spec](https://github.com/Duckwhale/RagnarokFileFormats/blob/master/GND.MD)
- In RO, most characters, monsters and NPCs are 2D sprites with animation clips per viewing direction (8 directions), chosen by camera angle. Most 3D models are map props like trees and buildings, in a custom RSM format. — [RagnarokFileFormats README](https://github.com/rdw-archive/RagnarokFileFormats/blob/master/README.MD)
- The GND terrain is a grid of "cubes", each covering 2x2 GAT tiles, with 4 corner heights. Each cube defines only TOP, NORTH and EAST surfaces. A surface is GROUND or WALL and references a shared surface definition (UVs, texture ID, lightmap slice ID, ARGB vertex colour). — [GND spec](https://github.com/Duckwhale/RagnarokFileFormats/blob/master/GND.MD)
- Texture density in the GND spec is "a 32-pixel texture fits one tile", and surfaces are about 64 px wide per texture slice. — [GND spec](https://github.com/Duckwhale/RagnarokFileFormats/blob/master/GND.MD)
- Lightmaps are 8x8 px per surface slice. Each slice has a shadow/AO block and a colour block, plus a 1-px border against bleeding. The shadow map is applied after lighting, and the client posterizes the colour map to 16 levels. — [GND spec](https://github.com/Duckwhale/RagnarokFileFormats/blob/master/GND.MD)
- Per-tile vertex colour adds hue variation without extra textures. Walls (cliffs) are generated wherever neighbouring cubes differ in height, and are not separate geometry. — [GND spec](https://github.com/Duckwhale/RagnarokFileFormats/blob/master/GND.MD)
- The community editor BrowEdit edits RSW/GND/GAT. Tutorials cover terrain height editing, texture placement, walls and smoothing. — [GitHub topic grf](https://www.Github.com/topics/grf); [How to Create Ragnarok Map (Scribd, community guide)](https://www.scribd.com/document/251220347/How-to-Create-Ragnarok-Map)
- Custom RO map makers treat lightmap tuning as a selling point, e.g. "lightmaps altered, now there is more coming from the sun". — [Najara custom maps](https://najara.gumroad.com/l/tIFeL)
- Tales Weaver (Softmax/Nexon) chose 2D over 3D on purpose. A 2003 article says that instead of imperfect 3D it specialised in 2D graphics, which make cute SD characters easy to express. — [경향신문 2003](https://www.khan.co.kr/article/200304071552051)

### Inferences
- RO's "look" comes from a few cheap ingredients. Small tiling ground textures (low texel density, matching the sprite pixel size) give a pixel feel. Baked, posterized, low-res lightmaps give soft blotchy shadows under trees and buildings that read as hand-painted. Per-tile vertex-colour tinting breaks up texture repetition. The ortho-like high camera hides the low-poly geometry. All of this can be reproduced in Unity URP: tiled Point-filtered textures, baked lighting or a painted AO/shadow texture, vertex colours, and a posterize step.
- RO's ground density (32 px per tile) is close to this project's 32 PPU. If one Unity unit equals one "tile", RO-style ground textures at 32 px/unit match the sprite pixel size directly. That is a strong argument for (a) with 32 px/unit textures.
- Tales Weaver maps look like hand-drawn 2D pixel backgrounds with layered objects, i.e. approach (b). This is inference from screenshots and the 2D decision, not confirmed.

### Gaps
- No Gravity developer interview or postmortem on RO's original map toolchain (internal editor, texture painting process) was found.
- No primary source on Tales Weaver's map pipeline (tiles vs. full painted backgrounds, tools) was found.

---

## Q2. Approach-by-approach comparison (visual result, effort, Unity URP complexity, pixel consistency, camera)

### Takeaway
(a) and (c) both use a real 3D map. (a) keeps texel density consistent by hand (texture px per unit), while (c) gets consistency automatically by rendering everything to a low-res buffer, but it re-pixelates the characters too. (b) gives the most "authentic" Korean-2D look but fights the tilted 3D camera. (d) is the most AI-friendly (every asset is just a PNG), but needs sorting and shadow work.

### Cited Findings

**(a) 3D low-poly + pixel/painted textures + 2D sprites (RO, HD-2D)**
- RO: 3D terrain plus 3D prop models, with 2D sprite characters (see Q1). — [RagnarokFileFormats README](https://github.com/rdw-archive/RagnarokFileFormats/blob/master/README.MD)
- Octopath Traveler's HD-2D started from 2D lighting techniques, then added "3D elements" to give depth. The aim was to combine lighting and pixels to show how light hits objects and how shadows fall, keeping a strong pixel aesthetic in a "three-dimensional environment". — [Nintendo Everything / Nintendo Dream interview](https://nintendoeverything.com/octopath-traveler-devs-on-character-origins-visual-style-initial-hd-rumble-plans-much-more/)
- The Octopath devs said early versions lacked depth, which made the view uninteresting, while pushing resolution and saturation too far made the pixel art lose its appeal. Sprites looked "lonely" on large screens, so they added more tile variety and colour density. — [Siliconera, Famitsu translation](https://www.siliconera.com/project-octopath-traveler-developers-answer-project-started-troubles-developing-hd-2d/)
- Triangle Strategy producer Asano said that as proportions get more realistic, resolution increases and pixels get smaller, pushing the result toward illustration. Deformed (chibi) characters must stay attractive as pixel art. — [Nintendo Life](https://www.nintendolife.com/news/2022/05/triangle-strategy-producers-talk-hd-2d-and-why-other-devs-havent-used-it)
- Octopath Traveler II's HD-2D is built in Unreal Engine (Epic developer interview). — [Unreal Engine interview](https://www.unrealengine.com/developer-interviews/octopath-traveler-ii-builds-a-bigger-bolder-world-in-its-stunning-hd-2d-style?lang=ja) (page returned 403 on fetch, so its content was not read)
- 3D texel density should stay consistent across assets. A mismatch, e.g. a small cup with a higher texture resolution than the table, breaks immersion. — [RenderHub texel density calculator](https://www.renderhub.com/3d-tools/texel-density-calculator)

**(b) 2D tilemap / painted backgrounds**
- RPG Maker "parallax mapping" means painting the whole map in an external art program and placing it in the engine as a background image. In older engines this worked around tile limits. MZ's four layers reduce the need. — [RPG Maker parallax mapping guide](https://www.rpgmakerweb.com/a/rpg-maker-vxace-recommend/parallax-mapping-guide); [RPG Maker MZ parallax blog](https://rpgmakerweb.com/blog/parallax-mapping-in-mz-basics-and-tips); [RPG Maker Wiki – Map](https://rpgmaker.fandom.com/wiki/Map)
- RPG Maker Unite (the Unity-based RPG Maker) supports parallax mapping and "large parts". — [Gematsu](https://www.gematsu.com/2022/07/rpg-maker-unite-details-parallax-mapping-large-parts)
- Eastward is pure pixel art in an in-house engine (Gii, on top of MOAI), with Aseprite for animation and Photoshop for design. The team built tools to automate asset workflows. — [Game Developer, Road to the IGF: Eastward](https://www.gamedeveloper.com/disciplines/road-to-the-igf-pixpil-s-i-eastward-i-)
- Eastward's look is often summarised as "retro-pixel + 3D lighting", with lighting that affects 2D props. The details come from secondary sources and were truncated. — [Eastward wiki](https://www.eastwardwiki.com/Eastward); [CDM techspeak blog](https://blogs.thecdm.ca/techspeak/wp-json/wp/v2/posts/538)

**(c) Pixel-perfect 3D (low-res render texture + post shaders)**
- A Short Hike's goal, in the developer's words: "I wanted to see if I could make a beautiful (and readable) 3D world using as few pixels as possible". "The low-resolution can help the world feel lush, and let your imagination fill in the details". The choice led to "flat cohesive shading and no anti-aliasing". A pixel-size option was added because some players disliked the look. — [PlayStation Blog](https://blog.playstation.com/2021/08/05/crafting-a-tiny-open-world-a-look-behind-the-scenes-at-the-creation-of-a-short-hike/)
- Wikipedia says A Short Hike used "big crunchy pixels" and simple models, which let the developer expand scope despite limited art skills. — [Wikipedia: A Short Hike](https://en.wikipedia.org/wiki/A_Short_Hike)
- Forum users describe A Short Hike as rendering to a low-res render texture and upscaling it, plus a colour-grading post shader. These are user descriptions citing a GDC talk, not confirmed by the developer here. — [itch.io post](https://itch.io/post/4487212); [itch.io post](https://itch.io/post/2065934)
- t3ssel8r-style tools render at low resolution, upscale, and snap the camera and objects to reduce "pixel creep" or jitter. They add outline materials with separate silhouette and crease-line controls. Orthographic cameras are required for clean snapping. — [Fab plugin listing](https://www.fab.com/listings/816f34f2-1a2e-4910-b3dd-009b5da0873b)
- Snapping works under orthographic projection because every pixel shifts by the same amount. Under perspective, pixels at different depths drift at different rates. — [arXiv 2603.14587 "Texel Splatting" (2026)](https://arxiv.org/abs/2603.14587)
- UPixelator for Unity renders to a low-res render texture, upscales, and snaps the camera and tagged objects to a world-space pixel grid. Creep reduction works only with orthographic cameras. — [UPixelator docs](https://github.com/Radivarig/UPixelator_Documentation); [Unity forum thread](https://discussions.unity.com/t/released-upixelator-pixelate-your-scenes/909110)
- kode80 PixelRender is a free (non-commercial) Unity system for real-time 3D-as-pixel-art with a palette shader and a PixelOutline effect. It is old and may not support URP. — [awesome.ecosyste.ms entry](https://awesome.ecosyste.ms/projects/github.com%2Fkode80%2Fpixelrenderunity3d)
- Retro Shaders Pro for URP adds inverted-hull outlines and texel-aligned (snapped) lighting. — [itch devlog](https://itch.io/devlog/998452/version-14-brings-hull-outlines-and-texel-aligned-shading.amp)
- t3ssel8r's public videos that got coverage are about animating 3D pixel-art characters (Cascadeur). He renders characters as 3D models, not as 2D sprites. — [Cascadeur interview](https://cascadeur.com/blog/general/making-an-animation-for-a-3d-pixel-art-game); [80.lv](https://80.lv/articles/tips-on-convincing-animations-for-3d-pixel-art-games)
- An academic 2026 SBGames paper (Pixelat3D) presents a real-time pixel-art-style 3D shader. — [SBGames paper](https://sol.sbc.org.br/index.php/sbgames/article/download/45414/45175/)

**(d) Sprite-based 2.5D (billboards on a ground plane)**
- Don't Starve is commonly described as a 3D world with billboard sprites facing the camera. This is a community claim, not a Klei statement. — [Defold forum](https://forum.defold.com/t/view-like-in-smash-bash-solved/12917); [Klei forums](https://kleiforums.com/forums/topic/56775-is-there-a-name-for-the-games-2d-as-3d-animation-style/)
- A Y-axis-only billboard viewed from a high camera gets foreshortened or distorted. The fix is to tilt the sprite or scale it in Y. — [Godot forums](https://godotforums.org/d/41420-billboarding-with-sprite-3d-with-a-current-camera)
- Upright sprites in 3D need depth handling. One URP devlog writes each sprite's screen-Y pivot into a custom depth texture so outlines and occlusion sort correctly. Another uses a custom URP Unlit shader so sprites sort as 3D objects by depth. — [Metavoidal devlog](https://yellowlabgames.itch.io/metavoidal/devlog/509534/an-indie-devs-guide-to-rendering-inside-of-the-lines); [GameDev.net 2.5D in Unity Part 2](https://gamedev.net/tutorials/programming/graphics/making-25d-game-in-unity-part-2-r5368/)
- A sprite outline shader that samples neighbouring pixels bleeds into neighbouring frames of a sprite sheet. — [Unity forum](https://discussions.unity.com/t/2d-outline-with-texture-sprite-in-multiple-invading-neighbor-frame/948815)

**Mixels / pixel consistency (applies to all)**
- "Mixels" means pixels of different sizes in one image or scene. Common advice (citing Michael Azzi's *Pixel Logic*) is never to mix resolutions. Backgrounds are sometimes tolerated as an exception, and in-engine scaling is a common cause. — [Flowlab community](https://community.flowlab.io/t/mini-rant-stop-using-mixels/38287); [Pixnote resolution guide](https://pixnote.net/en/learn/resolution); [bugnet resolution guide](https://bugnet.io/blog/choosing-a-pixel-art-resolution-for-your-game)
- Unity's Pixels Per Unit setting means sprite pixels per world unit. Keeping one PPU across all sprites keeps the pixel grid consistent. — [Unity docs: spritePixelsPerUnit](https://docs.unity.cn/ru/ScriptReference/TextureImporter-spritePixelsPerUnit.html)

### Inferences (project-specific; my analysis, not from sources)
- **Camera math.** Ortho size 5 at 720p gives 72 screen px per world unit vertically, so a 32 PPU sprite shows about 2.25 screen px per texel. That is non-integer, so Point-filtered sprites and textures will shimmer or have uneven pixel widths as the camera moves. It is worth fixing first whatever the approach. Options:
  - pick a resolution and ortho size so px per unit is a multiple of 32 (e.g. 64 or 96);
  - or render the world to a low-res RT (as in approach (c));
  - or accept slight unevenness, as many HD-2D-style games do.
- **Ground foreshortening.** With a 35° tilt, a flat ground texture at 32 px/unit is squashed vertically on screen by about sin(35°) ≈ 0.57. Sprites tilted to face the camera are not squashed. So ground pixels look wider than tall, a mild mixel effect that RO also has and that players accept. Approach (c) removes it because everything is re-rasterised to one grid.
- **(a) in Unity URP.** Effort is medium. Modelling is simple boxes and planes (ProBuilder is built into Unity), and textures are 32 px/unit tiles (AI- or Aseprite-made). Lighting is baked lightmaps or a single directional light with soft shadows. Optional extras are a posterize/colour-grading volume and URP depth of field with a tilt-shift feel (HD-2D). Pixel match is good if every texture keeps 32 px per unit. It matches the "Ragnarok" reference exactly, and the current camera already fits.
- **(b) in a 3D scene.** A painted 2D background on a flat plane only looks right from exactly the angle it was painted for. Since the camera is fixed, that is feasible: paint or generate each map as one image matching the 35° view, then add invisible colliders and upright sprite "overlay" layers for occlusion (trees or roofs in front of characters). It is the most labour-intensive per map and hardest for AI to keep consistent at a large size. Depth sorting with a 3D plane is manual. Tile-based (Unity Tilemap) art is designed for top-down or 3/4 2D cameras, not a tilted 3D camera, so switching to it would mean changing the scene to a pure 2D setup.
- **(c) with existing sprite characters.** If characters are rendered into the same low-res RT, they are resampled. At an RT density of exactly 32 px per unit and a pixel-snapped camera they stay crisp. Otherwise they get eaten or doubled pixels. A common pattern is to render the world to the low-res RT and characters at native resolution on top, but that brings back mixels between world and characters. (c) is technically the hardest (custom URP Renderer Feature, outlines, snapping, smooth-camera sub-pixel offset). Visually it is closest to t3ssel8r and A Short Hike, which are 3D-character games, not sprite-character games.
- **(d).** Every asset (tree, lamp, bench, house facade) is a PNG from PixelLab or another AI tool at 32 PPU, placed upright and tilted 35° to face the camera, exactly like the characters. That gives the best pixel consistency with the characters and the lowest 3D skill requirement. Weaknesses:
  - flat-looking buildings (only one face visible, which is fine with a fixed camera);
  - sorting overlaps, solvable by putting sprites in 3D space with depth write and Alpha Clip, which the project already uses for characters;
  - ground transitions;
  - no real shadows unless blob shadows or shadow casting from the sprite quads are added.
- **Hybrid.** The most practical mix for a solo coder is RO-style (a) for the ground plus (d) for props. The ground is a 3D grid or plane (with optional height steps) using tiled 32 px/unit textures, vertex-colour variation and baked lightmaps. Buildings are simple 3D boxes with pixel textures or upright sprites, and trees, props and decor are upright sprites. This is effectively what RO itself does, minus 3D trees, and it keeps one pixel density everywhere.

### Gaps
- No primary technical breakdown of Octopath's HD-2D pipeline (texture density, sprite planes, DoF parameters) was readable. The Unreal interview returned 403.
- No primary source confirms t3ssel8r's exact Unity renderer. His rendering videos were not found in search, so claims rely on the third-party tools that imitate him.
- No primary Klei source on Don't Starve's renderer was found.
- No source on To the Moon's mapping technique specifically. It is an RPG Maker XP game, which is training knowledge, not verified here.
- "Crossing Worlds" was not researched for lack of tool budget.

---

## Q3. Which approach is most feasible for a solo coder using AI generation + light Aseprite touch-up?

### Takeaway
A hybrid of (a) and (d) is the most feasible: an RO-style textured ground with upright pixel-sprite props. It reuses the existing sprite pipeline (32 PPU, Point, Alpha Clip, camera-facing tilt) and needs only seamless 32 px tiles plus individual prop PNGs from AI. Full approach (c) is a stretch goal with a big pixel-consistency question for sprite characters. Approach (b) costs the most art per map.

### Cited Findings
- A Short Hike's simple models plus "big crunchy pixels" let a developer with limited art skills expand scope. This supports low-detail approaches for non-artists. — [Wikipedia: A Short Hike](https://en.wikipedia.org/wiki/A_Short_Hike)
- The HD-2D lesson: richness came from tile variety and colour density, not resolution, and overly high resolution or saturation hurt the pixel appeal. — [Siliconera](https://www.siliconera.com/project-octopath-traveler-developers-answer-project-started-troubles-developing-hd-2d/)
- RO itself relies on small reusable textures, baked lightmaps and vertex colours rather than unique painted maps. — [GND spec](https://github.com/Duckwhale/RagnarokFileFormats/blob/master/GND.MD)
- Eastward (a small team of 3 at the start) built tooling to automate asset import, which shows how much asset pipeline automation matters even for 2D. — [Game Developer](https://www.gamedeveloper.com/disciplines/road-to-the-igf-pixpil-s-i-eastward-i-); [Eastward wiki](https://www.eastwardwiki.com/Eastward)

### Inferences
- **Feasibility ranking for this project:** (a+d hybrid) > (d) > (a with 3D buildings) > (c) > (b).
- **Concrete first steps** (inference, untested):
  1. Fix the pixel grid: choose ortho size or resolution so screen px per unit is an integer multiple of 32, or add a pixel-perfect RT later.
  2. Replace the flat plane with a tiled ground material: a 32x32 seamless grass/stone/dirt texture with Point filter and tiling equal to world units. Add a second "detail" layer or vertex-colour noise to break repetition (RO's trick).
  3. Add props as upright sprites using the same importer and an Alpha Clip material, with a blob-shadow decal or a sprite shadow-caster.
  4. Add baked lighting or a single soft directional light plus URP post-processing (colour grading, slight vignette, optional DoF) for HD-2D warmth.
  5. Only then consider (c) as a post effect experiment (UPixelator or a custom Renderer Feature).
- **AI asset fit:**
  - Seamless tiles and single props are where current AI pixel tools are reliable.
  - Whole-map paintings (b) are where AI style drift and perspective errors are most visible.
  - 3D buildings from Meshy/Tripo come out with high-res PBR textures. They need retexturing down to 32 px/unit, otherwise they cause a texel-density mismatch (mixels).

### Gaps
- No user studies or polls compare player perception of these approaches.
- AI tooling capabilities (PixelLab tiles/map objects, Meshy/Tripo pixel textures) as of late 2026 were not verified in this research. Another researcher may cover them.
