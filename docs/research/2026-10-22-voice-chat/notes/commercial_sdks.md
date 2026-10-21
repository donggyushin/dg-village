# Commercial / managed voice SDKs for Unity proximity voice (dg-village)

Researched 2026-10-22. Context: dg-village = Unity 6 client (macOS + Windows desktop now, WebGL later), Python FastAPI server that already computes "who hears whom" (`Room.hearers()`: hallway radius, lounge = everyone in the connected carpet area, desk focus = hears nobody / heard by nobody). Several official pages (Unity docs, Photon pricing) returned HTTP 503 during research; those facts come from search snippets of the official pages and are flagged.

Usage model for cost estimates (my assumption, not from a source): "N concurrent" = N people online together ~4h/day, 30 days → **7,200 participant-minutes per user per month**. 10 CCU ≈ 72,000 min/mo, 50 CCU ≈ 360,000 min/mo, 200 CCU ≈ 1,440,000 min/mo. PCU-billed vendors (Vivox, ODIN, Photon) bill on the monthly peak = N.

## Pricing, free tiers, and estimated monthly cost (10 / 50 / 200 concurrent)

### Takeaway
For a hobby-scale game, **Vivox is $0 up to 5,000 PCU** and **ODIN is ~€0–58/mo**; minute-billed vendors (LiveKit Cloud, Agora) cost more as usage grows ($50→$500/mo LiveKit, ~$60→$1,400/mo Agora at 10→200 CCU with 4h/day). Dissonance is a one-time ~$110 asset but you host the transport yourself; EOS Voice is free.

### Cited Findings
- **Vivox (Unity Gaming Services)**: free up to 5,000 PCU; then $2,000 per 5,000 PCU (5,001–50,000), $1,500 per 5,000 (50,001–100,000), $1,250, then $1,000 per 5,000 above 200,000 — [Unity Gaming Services pricing](https://activation.unity3d.com/en/products/gaming-services/pricing); same figures on [unity.com pricing (ja)](https://unity.com/ja/products/gaming-services/pricing)
- Vivox PCU = highest count of users signed in at any moment during the billing period (calendar month); the support article states it is current as of **2024-11-22** (older than 2025 — flag) — [Unity Support](https://support.unity.com/hc/en-us/articles/31045802890260)
- A competitor site claims Vivox bills in $2,000 buckets and has no built-in usage cap (unverified, competitor source) — [fiveminutes.io](https://fiveminutes.io/articles/fiveminute-chat-vs-vivox)
- Vivox Safe Text $0.05/user after 5,000 MAU; Safe Voice (beta) $0.10/user after 5,000 MAU — [Unity pricing](https://unity.com/ja/products/gaming-services/pricing)
- **LiveKit Cloud**: Build $0/mo (5,000 WebRTC participant-min, 100 concurrent connections, 50 GB downstream); Ship from $50/mo (150,000 min, then $0.0005/min; 1,000 connections; 250 GB, then $0.12/GB); Scale from $500/mo (1.5M min, then $0.0004/min; 5,000 connections; 3 TB, then $0.10/GB); Enterprise custom — [LiveKit pricing](https://livekit.com/pricing), [pricing.md](https://livekit.com/pricing.md)
- LiveKit Build free quota confirmed in docs: 5,000 WebRTC participant minutes — [LiveKit quotas & limits](https://docs.livekit.io/home/cloud/quotas-and-limits/)
- **4Players ODIN**: "Voice Free" = 25 PCU/month with all features; paid €0.29 per PCU/month (1–19,999 PCU), down to €0.19 above 80,000 — [ODIN pricing](https://odin.4players.io/pricing/)
- ODIN docs/marketing also say free up to 25 concurrent users "for developing and testing", subscription required for production — [ODIN gaming](https://odin.4players.io/solutions/gaming). This conflicts with the pricing page wording (Free tier lists no "dev only" restriction) — check ToS.
- ODIN older self-hosted figures: €350/instance/month or €10,000 perpetual per instance (blog, possibly dated) — [4Players blog](https://4players.io/blog/odin-what-the-new-sdk-can-do-for-you)
- **Agora Voice**: 10,000 free minutes/month; Standard $0.99 per 1,000 minutes; volume discounts from 100K minutes (5%, 7%, 10%) — [Agora voice pricing docs](https://docs.agora.io/en/realtime-media/voice/reference/pricing.md), [Agora RTC pricing](https://www.agora.io/en/pricing/agora-rtc)
- **Photon Voice 2**: free asset with Photon Cloud hosting up to 20 CCU — [Unity Asset Store](https://marketplace.unity.com/packages/tools/audio/photon-voice-2-130518)
- Photon Voice has the same plans/pricing as PUN/Realtime; a forum post quoted 1,000 CCU at $185/mo (old forum thread, likely pre-2025 — flag); using one AppId for PUN + Voice double-counts CCU — [Photon forum](https://forum.photonengine.com/discussion/comment/44723/), [Photon forum](https://forum.photonengine.com/discussion/comment/34671/)
- **Dissonance**: Asset Store price ~$110 (one snapshot showed $100), v9.0.8 released Oct 2025 — [Asset Store](https://assetstore.unity.com/packages/tools/audio/dissonance-voice-chat-70078)
- **EOS Voice**: Epic announced voice as free for all creators — [Epic news](https://www.epicgames.com/site/news/epic-online-services-launches-free-in-game-voice-and-easy-anti-cheat) (2021 launch article, older than 2025 — flag); "completely free for any number of players" — [Modulate](https://www.modulate.ai/blog/voip-for-gaming-101-part-1)

### Inferences
Estimated monthly cost (computed from the cited prices + the 7,200 min/user/mo assumption):

| Vendor | 10 CCU (72k min) | 50 CCU (360k min) | 200 CCU (1.44M min) |
|---|---|---|---|
| Vivox | $0 | $0 | $0 (free ≤5,000 PCU) |
| ODIN cloud | €0 (≤25 PCU, if production allowed on Free) | ~€14.5 (50 × €0.29) | ~€58 |
| LiveKit Cloud | $50 (Ship; Build's 5k min is exceeded in <1 day) | ~$155 (Ship: 50 + 210k × $0.0005) | $500 (Scale covers 1.5M) or ~$695 on Ship |
| Agora | ~$61 | ~$347 | ~$1,350–1,416 (before/after 5% discount band) |
| Photon Voice | $0 (≤20 CCU) | paid plan needed (price not verified for 2026) | paid plan needed |
| Dissonance | ~$110 once + own server bandwidth | same | same |
| EOS Voice | $0 | $0 | $0 |

- LiveKit downstream data: an audio stream is roughly 30–40 kbps; a listener receiving 5 speakers continuously for 4h/day ≈ ~8–9 GB/month worst case, but Opus DTX/silence makes real usage much lower. 10 users could approach Build's 50 GB only in a worst case. Not sourced — order-of-magnitude only.
- The "keep it open while working" concept means long sessions, mostly silent. **PCU billing (Vivox, ODIN, Photon) fits this better than per-minute billing** (LiveKit, Agora), which charges for every connected minute even when silent (unless clients disconnect from voice while at a desk in focus mode — a real cost lever for minute-billed vendors).

### Gaps
- Current (2026) Photon paid plan table: photonengine.com pricing returned 503; only the free 20 CCU and an old $185/1000 CCU quote were verified.
- Whether ODIN Free 25 PCU may be used in production (sources conflict).
- Agora: whether 3D Spatial Audio has an extra per-minute surcharge; monthly minimum fees mentioned in search snippets were not confirmed.
- No 2026-dated Vivox pricing announcement found; numbers are as currently indexed on Unity's page.

## Built-in proximity / positional features and fit with custom rules (zone, focus mute, radius)

### Takeaway
Vivox (positional channels), ODIN (server culling by position), Agora (spatial audio + range), and Photon (interest groups, DIY distance) offer proximity primitives; LiveKit has none but gives a **server API to set exactly who subscribes to whom**, which maps 1:1 onto dg-village's existing `Room.hearers()` rules. All can emulate lounge/focus via extra channels/rooms or mute/unsubscribe.

### Cited Findings
- **Vivox**: positional (3D) channels; the game must frequently update position/orientation, recommended 2–4 times per second; audibility configured via positional channel properties — [Vivox positional channel configuration](https://docs.unity.com/en-us/vivox-unity/developer-guide/channels/positional-channel-configuration), [positional channels](https://docs.unity.com/ugs/en-us/manual/vivox-v15/manual/developer-guide/channels/positional-channels)
- **ODIN**: Unity SDK provides automatic proximity chat (3D spatial audio); volume adjusts by distance and occlusion — [ODIN voice chat](https://odin.4players.io/voice-chat)
- ODIN server uses client-submitted coordinates to **cull peers server-side** within a room (unit circles radius 1.0; scale positions by 1/MaxHearingDistance; peers outside appear as not in the room) — [ODIN C API odin_room_update_position](https://docs.4players.io/voice/core/api/functions/odin_room_update_position), [Node.js updatePosition](https://4players.io/odin/sdk/nodejs/classes/odinroom/updateposition), [Unity 2.0 SetPosition](https://4players.io/odin/sdk/unity/2.0.0/classes/room/setposition/)
- **Photon Voice**: proximity via interest groups — each user speaks into one fixed group, listening groups change with distance (`DynamicAudioGroupMember` checks distance in `Update()`), requires Fusion in that add-on — [Photon Dynamic Audio Group](https://doc.photonengine.com/arvr/current/xr-addons/voice/fusion-industries-addons-dynamic-audio-group)
- Photon: all actors listen to group 0 by default; others via `OpChangeGroups`; speaker's `recorder.InterestGroup` sets target; demo `DemoProximityVoiceChat` uses triggers — [Photon Voice intro](https://doc.photonengine.com/zh-tw/voice), [Photon forum](https://forum.photonengine.com/discussion/comment/51154/)
- **Agora**: 3D Spatial Audio via `ILocalSpatialAudioEngine` (`updateSelfPosition`, `updateRemotePosition`), Unity supported — [Agora spatial audio (Unity)](https://docs.agora.io/en/realtime-media/rtc/build/enhance-the-audio-experience/spatial-audio/unity), [Agora blog: spatial audio in Unity](https://www.agora.io/en/blog/implementing-spatial-audio-chat-in-unity-using-agora)
- Agora range: set max receivable distance in meters and a cap on received streams (≤16 recommended) — [Agora beta docs](https://docs-beta.agora.io/en/voice-calling/advanced-features/spatial-audio). (Note: "World mode / Team mode" range audio is a **ZEGOCLOUD** feature, not Agora — [Zego range audio](https://www.zegocloud.com/docs/real-time-video-flutter/communication/range-audio).)
- **LiveKit**: no built-in proximity; RoomService `UpdateSubscriptions` lets the backend (roomAdmin) subscribe/unsubscribe a participant to specific tracks; publishers can also restrict subscribers with track subscription permissions (client-side) — [LiveKit server APIs](https://docs.livekit.io/reference/server/server-apis/), [publish docs](https://docs.livekit.io/transport/media/publish.md)
- **Dissonance**: no own network; relies on integrations (Mirror, NGO, Photon, WebRTC P2P, etc.); custom integration requires unreliable/unordered (UDP-like) transport — "TCP is not suitable" — [Dissonance: Choosing a network](https://dissonance.readthedocs.io/en/latest/Basics/Choosing-A-Network.html)
- **EOS Voice**: one-on-one / group chat in lobbies or matches, relayed via Epic backend — [Epic news](https://www.epicgames.com/site/news/epic-online-services-launches-free-in-game-voice-and-easy-anti-cheat); no source found for built-in positional audio.

### Inferences
Mapping dg-village rules (hallway radius / lounge everyone / desk focus):
- **LiveKit**: one LiveKit room per `channel/map`; Python server already knows `hearers()` → call `UpdateSubscriptions` on changes (or on the 10Hz tick diff). Lounge, focus, radius are all just subscription sets. Volume falloff by distance done client-side in Unity (AudioSource 3D). Most flexible, most server work.
- **Vivox**: hallway = positional channel (audible distance = hearingRadius); each lounge = separate non-positional (echo/group) channel; focus = leave/mute channels. Requires multi-channel → desktop only (WebGL is single channel, 2D only).
- **ODIN**: hallway = room with `SetPosition` server culling; lounge = separate room (join/leave on zone change); focus = leave rooms / mute. Close to Gather semantics with little code.
- **Agora**: hallway = channel + spatial audio range; lounge = separate channel or spatial off; focus = mute/leave. Range filtering is a client-side engine.
- **Photon Voice**: interest groups per player/zone; lounge = shared group; focus = unsubscribe all. Logic runs client-side; Photon's realtime room model overlaps with our own server (two networking stacks).
- **Dissonance**: has rooms + positional "proximity" broadcast triggers in Unity, but needs a UDP-capable transport; our server is WebSocket (TCP) only → would need its WebRTC P2P integration or a new UDP relay — high fit cost.
- **EOS**: would need DIY proximity (per-participant mute / multiple lobbies); weakest fit.

### Gaps
- Vivox exact audibility model parameters (audible distance, conversational distance, rolloff, fade model) — Unity docs pages returned 503; only search snippets seen.
- EOS RTC per-participant receive control specifics for Unity (PlayEveryWare plugin) not researched.

## Server-side vs client-side enforcement of proximity

### Takeaway
Only LiveKit (server API subscriptions driven by our authoritative server) gives **fully server-authoritative** hearing rules. ODIN and Vivox compute audibility on their servers but from **client-reported positions** (spoofable); channel/room membership can still be gated by server-issued tokens. Agora range and Photon interest groups are client-driven.

### Cited Findings
- ODIN: "The server utilizes the provided coordinates to perform automatic culling among peers in the same room" — coordinates come from the client SDK call — [ODIN C API](https://docs.4players.io/voice/core/api/functions/odin_room_update_position)
- Vivox: the game (client) updates the SDK with the user's position 2–4×/s — [Vivox positional channel configuration](https://docs.unity.com/en-us/vivox-unity/developer-guide/channels/positional-channel-configuration)
- Vivox: access tokens (JWT-like, HMAC-SHA256 with the secret key) generated on a secure server; per action/channel claims — [Vivox Python token example](https://docs.unity.com/en-us/vivox-unity/access-token-guide/generate-token-secure-server/example-python)
- Photon proximity add-on: distance check runs in a client component `Update()` — [Photon Dynamic Audio Group](https://doc.photonengine.com/arvr/current/xr-addons/voice/fusion-industries-addons-dynamic-audio-group)
- Agora spatial audio: positions supplied via local engine calls — [Agora spatial audio](https://docs.agora.io/en/realtime-media/voice/build/enhance-the-audio-experience/spatial-audio.md)
- LiveKit: `UpdateSubscriptions` requires roomAdmin (server credential); admin can subscribe a participant even without canSubscribe; `UpdateParticipant` permission changes revoke the old token — [LiveKit server APIs](https://docs.livekit.io/reference/server/server-apis/)

### Inferences
- For a friends-only hobby game, spoofed positions are a low risk; but focus mode ("nobody hears me at my desk") is a privacy expectation — with LiveKit the server can guarantee it; with Vivox/ODIN, the server can only guarantee it if focus = token-gated leaving of channels/rooms (client must comply, server can't force-kick from a Vivox channel without server REST APIs — not verified).
- LiveKit: if clients are given `canSubscribe=false` and only the server subscribes them, the rules are fully enforced.

### Gaps
- Whether Vivox or ODIN offer server REST APIs to kick/mute a user in a channel (not verified).

## Unity platform support (macOS, Windows, WebGL)

### Takeaway
All six run on Windows/macOS desktop builds (Dissonance/EOS need checking). **WebGL is the weak spot**: Vivox WebGL = 2D-only single channel; ODIN, LiveKit and Agora require a separate web/JS-based SDK path for WebGL; Photon Voice does not officially support WebGL.

### Cited Findings
- **Vivox WebGL**: limited support since 16.5.0; only 2D (non-positional) channels, no 3D; one channel per instance; no participant events (audio energy, is_talking, mute); unsupported calls throw NotImplementedException — [Vivox WebGL](https://docs.unity.com/ugs/en-us/manual/vivox-unity/manual/Unity/developer-guide/vivox-webgl)
- **LiveKit**: native Unity SDK supports macOS, Windows, Linux, iOS, Android, Unity 2022.3+; WebGL uses a separate `client-sdk-unity-web` package; the native repo README has been labeled "Developer Preview, not ready for production" (current status unclear) — [LiveKit Unity docs](https://docs.livekit.io/transport/sdk-platforms/unity/), [LiveKit Unity WebGL docs](https://docs.livekit.io/transport/sdk-platforms/unity-web/), [OpenUPM package](https://openupm.com/packages/io.livekit.livekit-sdk/)
- **ODIN**: Unity SDK README states WebGL is not natively supported — use the JavaScript/TypeScript SDK for WebGL; Unity 2019.4+; v2.x beta (Unity 2021.4+), all room participants must use the same major version — [odin-sdk-unity GitHub](https://github.com/4Players/odin-sdk-unity), [ODIN intro](https://4players.io/odin/ai-training/general/introduction)
- **Agora**: native Unity SDK; WebGL via community plugin `Agora_Unity_WebGL` wrapping Web SDK 4.x (desktop browsers, best on Chrome; no Raw Data/PushAudioFrame; Editor runs native code so it doesn't reflect web behavior) — [Agora_Unity_WebGL wiki](https://github.com/AgoraIO-Community/Agora_Unity_WebGL/wiki)
- **Photon Voice 2**: Asset Store lists WebGL, but Photon staff on the forum said Voice will not officially support WebGL (forum read-only, answers may be dated); latest listed version 2.58 (2025-03-07) — [Photon forum](https://forum.photonengine.com/discussion/20686/request-unity-webgl-support-for-photon-voice), [Asset Store](https://marketplace.unity.com/packages/tools/audio/photon-voice-2-130518)
- **Dissonance**: 2016 announcement listed Windows, Linux, macOS, iOS, Android (old); no WebGL statement found in current docs — [Unity forum 2016](https://forum.unity3d.com/threads/coming-soon-dissonance-unity-voice-chat.438271/)
- **EOS**: as of the 2021 launch, the Unity EOS plugin did not offer full voice support (old — flag) — [Modulate](https://www.modulate.ai/blog/voip-for-gaming-101-part-1)

### Inferences
- If WebGL proximity voice matters later, the realistic candidates are LiveKit (web SDK + server-side subscriptions — proximity logic stays on our server, so WebGL gets identical behavior) and ODIN (JS SDK has `setPosition` server culling). Vivox WebGL loses positional audio entirely.
- LiveKit's "same server logic for all platforms" is attractive given our server already computes hearers.

### Gaps
- Dissonance / EOS WebGL status (no official current statement found; WebGL generally requires WebRTC since browsers have no raw UDP — inference).
- Vivox macOS/Windows platform listing page returned 503 (desktop support is well known but not re-verified here).

## Server-side integration (token issuance from Python backend, server APIs)

### Takeaway
All managed services use server-issued tokens that our FastAPI server can mint (JWT/HMAC). LiveKit has an official Python server SDK (`livekit-api`) including room/subscription management; Vivox publishes a Python token example; ODIN has a token module (`odin-tokens`, JS) and documents server-side token generation.

### Cited Findings
- LiveKit: `livekit-api` = backend package for access tokens and room management; `livekit` = realtime SDK — [LiveKit Python SDK index](https://docsearch.algolia.com/mcp/docs/repo/livekit/python-sdks), [LiveKit server APIs](https://docs.livekit.io/reference/server/server-apis/)
- Vivox: official Python and C# examples for generating access tokens on a secure server (HMAC-SHA256 JWT-style) — [Vivox Python example](https://docs.unity.com/en-us/vivox-unity/access-token-guide/generate-token-secure-server/example-python)
- ODIN: access token "should be generated on a server"; `odin-tokens` repo for generating access keys and room tokens; FAQ covers Cloud Functions/Lambda token generation — [ODIN docs](https://4players.io/odin), [4Players GitHub](https://github.com/4players)
- Agora: channel tokens (not researched in detail here) — no source collected.

### Inferences
- Integration effort ranking for dg-village (my judgment): ODIN ≈ Vivox (drop-in Unity components + token endpoint) < Agora < LiveKit (token endpoint + subscription sync from `RoomManager` + client-side 3D AudioSource per remote track) < Photon (second networking stack) < Dissonance (needs UDP/WebRTC transport).
- Vivox Python token sample has a py3 bug noted by the search summary (`rstrip('=')` on bytes) — verify when implementing.

### Gaps
- Agora token-generation specifics for Python not collected.
- Vivox server REST/admin APIs (kick, mute) not checked.

## Lock-in, terms, vendor reliability

### Takeaway
LiveKit is open-source (self-hostable fallback) and well-funded ($100M Series C at $1B, Jan 2026); Vivox is owned by Unity and tied to a UGS project; ODIN offers on-prem/self-host licenses; Agora/Photon are proprietary clouds.

### Cited Findings
- LiveKit Series B $45M at $345M valuation (Apr 2025, Altimeter lead); Series C $100M at $1B valuation (Index Ventures), dated 2026-01-22 by CB Insights — [TechLeap](https://finder.techleap.nl/news/feed/livekit-raises-45m-in-series-b), [Unite.ai](https://www.unite.ai/livekit-reaches-1-billion-valuation-as-voice-ai-infrastructure-heats-up/), [CB Insights](https://www.cbinsights.com/company/livekit/financials)
- LiveKit Cloud pricing page emphasizes AI agents (inference credits, agent sessions) — [LiveKit pricing](https://livekit.com/pricing) — i.e., the company's growth focus is voice AI.
- Vivox pricing/billing "subject to change" per Unity support — [Unity Support](https://support.unity.com/hc/en-us/articles/31045802890260)
- ODIN: enterprise options include public/private cloud, on-prem, air-gapped — [ODIN pricing](https://odin.4players.io/pricing/)

### Inferences
- LiveKit lock-in is lowest: same protocol self-hosted (could run on our Lightsail box later).
- Vivox: free tier very generous but tied to Unity's business decisions (Unity has changed pricing before, e.g. the 2023 Runtime Fee episode — training knowledge, not sourced here).
- Photon voice + our own server = two vendors' realtime stacks; Photon is a stable long-running vendor but adds architectural duplication.

### Gaps
- No 2025–2026 news found on Vivox roadmap/deprecation; no evidence of Vivox being discontinued.
- 4Players company size/funding not researched.

## Server regions near Korea

### Takeaway
Not well documented on the pages fetched. LiveKit Cloud claims a global edge network (<250ms) without naming regions; Photon's Asset Store mentions Asia hosting; Agora claims a global SD-RTN. No source confirmed a Seoul PoP for any vendor in this research.

### Cited Findings
- LiveKit pricing page: "global edge network", region pinning on Scale; no specific regions named — [LiveKit pricing](https://livekit.com/pricing)
- Photon Cloud hosting centers in US, Europe, Asia & Australia — [Asset Store](https://marketplace.unity.com/packages/tools/audio/photon-voice-2-130518)
- ODIN pricing page lists no regions — [ODIN pricing](https://odin.4players.io/pricing/)

### Inferences
- Testers are presumably in Korea while our game server is in Sydney; a voice SFU in Tokyo/Seoul/Singapore would give much better voice latency than routing via Sydney. Managed services with an Asia edge (LiveKit Cloud, Agora, Photon "Asia"/"kr") are likely better than self-hosting voice on the Sydney Lightsail box.

### Gaps
- Photon "kr" (Seoul) region existence — known from training data but not verified here.
- Vivox data center locations and Agora Seoul node — not found.
- LiveKit Cloud region list (docs page not fetched).
