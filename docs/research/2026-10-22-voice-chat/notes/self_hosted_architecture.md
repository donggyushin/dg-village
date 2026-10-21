# Self-hosted WebRTC architectures for proximity voice (P2P mesh vs SFU: LiveKit OSS, mediasoup, Janus, Pion)

Context: Unity client (desktop now, WebGL later), Python FastAPI WebSocket game server that already knows positions and zones (desk = focus mode, mute both ways; lounge = everyone in the same lounge hears each other; hallway = within `hearingRadius`). Hosting: AWS Lightsail $12 plan (2 GB RAM, 2 vCPU burstable) in Sydney, already running FastAPI + Postgres + Caddy. Most users probably in Korea.

## P2P mesh: realistic peer count, churn, signaling over the existing WebSocket

### Takeaway
Published mesh limits (4-6 peers) are for video. Audio-only uses far less bandwidth, so roughly 8-10 audible peers per client looks workable. No source quantifies an audio-only limit, though. In a proximity game the number of open connections stays small, but every time someone walks in or out of range a new ICE/DTLS handshake starts, which takes a few hundred ms to over a second. The FastAPI server can relay offer/answer/ICE messages with little extra code, but TURN is still needed for the ~10-20% of users behind restrictive NATs.

### Cited Findings
- BlogGeek.me: mesh "can usually scale to 4-6 participants for video sessions at most". Its main drawbacks are that it can't scale to many participants and needs a lot of uplink bandwidth from each participant — [BlogGeek.me glossary: Mesh](https://bloggeek.me/webrtcglossary/mesh)
- Other vendors give similar video-centric limits: Forasoft says the practical mesh limit is about 4 participants on consumer connections, and Ant Media suggests mesh for 2-4 — [Forasoft glossary](https://www.forasoft.com/learn/video-streaming/glossary/terms-streaming/mesh-topology); [Ant Media: WebRTC network topology](https://antmedia.io/webrtc-network-topology/)
- I found no source with an audio-only mesh limit. The bottleneck is still N-1 uplink streams plus N-1 PeerConnections per client (per the search summary of the sources above).
- Gather (the reference product) runs its own **SFU** across several data centers, not a mesh. It says "any two users can have an audio/video connection in an instant", and it is re-architecting toward SFU cascading to replace "multiple simultaneous SFU connections" with single connections — [Gather job posting (Staff Backend Engineer, Video)](https://jobs.orbit.mit.edu/companies/gather-2-674309dd-fe8e-47c7-8df5-bcd9b2a88b57/jobs/31180045-staff-backend-engineer-video); [Dynamite Jobs listing](https://dynamitejobs.com/company/gather/remote-job/staff-backend-engineer-video)
- In Gather, A/V connects automatically when two or more avatars come close — [Cool Stars 20.5 conference lessons, arXiv 2105.08795](https://arxiv.org/pdf/2105.08795)
- mediasoup and similar SFU libraries are "signaling agnostic", so with an SFU library the developer also writes signaling. Mesh has the same requirement — [mediasoup overview](https://mediasoup.org/documentation/overview/)

### Inferences
- **Bandwidth math** (my estimate): Opus at 32 kbps plus RTP/UDP/IP/SRTP overhead at 20 ms packets (50 pps) comes to roughly 45-55 kbps per stream on the wire. 8 audible peers ≈ 400 kbps up and 400 kbps down per client. Fine on home broadband, but each peer is a separate encrypted stream, and the per-PeerConnection CPU cost (no source) adds up on weak laptops.
- **Churn**: a game that connects and disconnects on range changes runs a full offer/answer + ICE + DTLS cycle on every crossing, unless peers are pre-connected at a larger radius and only muted/unmuted at the hearing radius. Useful mitigations: hysteresis (connect at radius R+2, disconnect at R+4), keeping connections for lounge members, and muting the track rather than tearing down.
- **Signaling over FastAPI**: new WS message types such as `rtc_offer {to, sdp}`, `rtc_answer`, `rtc_ice {to, candidate}`, relayed only between players in the same room. The server can refuse to relay when the two players aren't allowed to hear each other (zone rules). That gates **connection setup**, not media.
- **Unity**: a mesh needs a Unity WebRTC binding (Unity's `com.unity.webrtc` for desktop, plus a separate JS bridge for WebGL), and you'd build your own audio routing. Not researched in depth here.
- Lounge with many people (e.g. 15 people in one lounge) is the mesh worst case: 14 up/down streams per client. That is where an SFU clearly wins.

### Gaps
- No measured data on audio-only mesh limits, or on per-PeerConnection CPU cost in Unity/libwebrtc.
- No measured ICE setup time for typical Korean home networks. The "few hundred ms to >1 s" figure is general WebRTC experience, not a cited number.

## SFU: how proximity is implemented (selective subscription, permissions, rooms per zone) and LiveKit server API from FastAPI

### Takeaway
LiveKit supports the "server decides who hears whom" model directly. Clients connect with `autoSubscribe` off, and the FastAPI backend (Python `livekit-api`, which needs `roomAdmin`) calls `UpdateSubscriptions(room, identity, track_sids, subscribe)` whenever its zone/proximity computation changes. `UpdateParticipant` (permissions/attributes/metadata) can be used for focus mode, e.g. revoking `can_publish`. Client-side volume falloff can be layered on top for smooth fades. One LiveKit room per channel+map maps 1:1 to the existing `"<channel>/<map>"` rooms.

### Cited Findings
- `autoSubscribe` is on by default: "the server automatically delivers new tracks to participants". Turning it off is recommended for "spatial applications and/or applications that require precise control" (`auto_subscribe=False` in Python/Rust) — [LiveKit: Subscribing to tracks](https://docs.livekit.io/home/client/tracks/subscribe/)
- Track publications are always visible to clients even when not subscribed, so a client knows tracks exist even when it isn't receiving them — [LiveKit: Receiving tracks](https://docs.livekit.io/home/client/receive)
- Server-side subscription management: `lkapi.room.update_subscriptions(UpdateSubscriptionsRequest(...))` in Python (Node: `updateSubscriptions('myroom', 'receiving-identity', ['TR_TRACKID'], true)`; `false` unsubscribes) — [LiveKit: Subscribing to tracks](https://docs.livekit.io/home/client/tracks/subscribe/)
- `UpdateSubscriptions` parameters: `room`, `identity`, `track_sids` (list), `subscribe` (bool). Requires `roomAdmin`. It "directly sets a participant's track subscriptions". Warning: calling it on an egress participant overrides that participant's subscriptions — [LiveKit Server APIs](https://docs.livekit.io/reference/server/server-apis/); [LiveKit room management](https://docs.livekit.io/server/room-management/)
- `UpdateParticipant(room, identity, metadata?, permission?, name?, attributes?)` requires `roomAdmin`. Permission fields: `can_subscribe`, `can_publish`, `can_publish_data`, `can_publish_sources`, `hidden`, `can_update_metadata`, `can_subscribe_metrics`. Updating permissions **revokes the participant's current token**, and the server issues a new one to the connected client. Clients receive `ParticipantPermissionChanged` — [LiveKit Server APIs](https://docs.livekit.io/reference/server/server-apis/); [LiveKit: Managing participants](https://docs.livekit.io/home/server/managing-participants/)
- Revoking `CanPublish` automatically unpublishes all of that participant's tracks. `MutePublishedTrack(room, identity, track_sid, muted)` (roomAdmin) mutes a specific track. Remote **unmute** is disabled by default (`room.enable_remote_unmute: true` when self-hosting) — [LiveKit: Managing participants](https://docs.livekit.io/home/server/managing-participants/)
- Python example of permission updates: `await lkapi.room.update_participant(UpdateParticipantRequest(room=..., identity=..., permission=ParticipantPermission(can_subscribe=True, can_publish=False, can_publish_data=True)))` — [LiveKit: Managing participants](https://docs.livekit.io/home/server/managing-participants/)
- Server API transport: HTTP POST `/twirp/livekit.RoomService/<Method>` with a Bearer token, JSON body in snake_case or camelCase. Server SDKs handle auth — [LiveKit Server APIs](https://docs.livekit.io/reference/server/server-apis/)
- Token issuing from Python: `api.AccessToken(key, secret).with_identity(...).with_name(...).with_grants(api.VideoGrants(room_join=True, room="...", can_subscribe=True, can_publish=True, can_publish_sources=[...])).to_jwt()`. `can_publish_sources` values include `microphone`. Room config in the token applies only when the room is first created. Self-hosted deployments should use short token TTLs — [LiveKit: Generating tokens](https://docs.livekit.io/home/server/generating-tokens/)
- Publisher-side **Track Subscription Permissions** (`setTrackSubscriptionPermissions(allParticipantsAllowed, trackPermissions)`): by default everyone can subscribe. Omitted participants get no permission, and tracks published later need a fresh permissions update — [LiveKit Flutter SDK ref](https://docs.livekit.io/reference/client-sdk-flutter/livekit_client/LocalParticipant/setTrackSubscriptionPermissions.html); [LiveKit: Publishing tracks](https://docs.livekit.io/transport/media/publish/)
- LiveKit markets the spatial use case: "Control how audio is modulated and which streams a user subscribes to by proximity or privileges", plus permissions so "private spaces stay private" — [LiveKit use cases: Spatial computing](https://livekit.io/use-cases/metaverse)
- LiveKit has a spatial-audio tutorial using Web Audio (client-side panning/volume by position) — [LiveKit blog: spatial audio tutorial](https://livekit.com/blog/tutorial-using-webrtc-react-webaudio-to-create-spatial-audio)
- **Unity SDK**: `livekit/client-sdk-unity` supports Windows, macOS, Linux, iOS, Android (Unity 2022+, tested on 6000.3). **WebGL is a separate package, `client-sdk-unity-web`**. Remote audio goes to a Unity `AudioSource` (so Unity's 3D audio/volume can apply). A "Platform Audio" mode adds AEC/NS/AGC. Known issues: macOS Bluetooth headset unmute breaking output, iOS backgrounding — [GitHub: livekit/client-sdk-unity](https://github.com/livekit/client-sdk-unity)
- mediasoup is a low-level Node.js SFU module with small JS/C++ client libs. It is signaling agnostic: no rooms, signaling, or auth provided — [mediasoup overview](https://mediasoup.org/documentation/overview/)

### Inferences
- **Recommended LiveKit pattern for dg-village**: FastAPI already runs the 10 Hz tick and `Room.hearers()`. Add a "voice graph" diff: on each tick (or on zone/seat change), compute for each listener the set of audible speakers. For changes, call `update_subscriptions(listener, [speaker_mic_track_sid], True/False)`. Map player id (account hex) = LiveKit identity, and LiveKit room = `"<channel>/<map>"`. Listen to LiveKit webhooks (or `ListParticipants`) to learn track SIDs.
- Hysteresis and rate limiting are still needed: subscription changes are cheap compared with mesh renegotiation, but each one is an HTTP call to LiveKit. Batch per tick and only send diffs.
- Smooth volume falloff inside the hearing radius = client-side `AudioSource.volume` / Unity 3D spatial blend. That is cosmetic, because enforcement is the subscription.
- **One room per zone** (move people between LiveKit rooms on zone change) is simpler conceptually, but means reconnects on every lounge entry/exit. It doesn't model hallway radius at all. Selective subscription inside one room per map fits better.
- mediasoup / Janus / Pion(ion) would require writing the room/subscription/auth layer yourself (mediasoup needs a Node.js sidecar; ion is largely unmaintained in my knowledge — not verified this session). There is also no first-party Unity SDK comparable to LiveKit's, so for a solo dev LiveKit OSS has by far the lowest effort.

### Gaps
- Not confirmed: whether a participant whose token has `can_subscribe=false` can still be force-subscribed via the admin `UpdateSubscriptions`. One search summary claimed "As an admin, you can subscribe a participant to a track even if they do not have canSubscribe permission", but fetching the cited LiveKit room-management page did **not** show that sentence. Treat it as unverified and test it locally (it decides whether clients can be fully locked out of self-subscribing).
- No info on `MoveParticipant`/`ForwardParticipant` (moving between rooms without reconnecting). Pages fetched didn't document them.
- Did not verify Janus/Pion-ion maintenance status or features this session.
- The latency of a `UpdateSubscriptions` call until audio starts flowing was not documented.

## Security: can a malicious client hear far-away / focused players? Server- vs client-enforced proximity

### Takeaway
Any scheme where the client receives the audio and decides locally (client volume = 0, or client-side `setSubscribed(true/false)` with `can_subscribe=true`) is **client-enforced**. A modified client can hear everyone in the room. Real enforcement needs the media path to be decided server-side: an SFU with server-driven subscriptions (and clients unable to self-subscribe), or a mesh where the signaling server refuses to broker connections. Even then, an existing mesh connection can't be cut by the server.

### Cited Findings
- With `autoSubscribe` on (default), every participant receives every track. With it off, clients call `publication.setSubscribed(true)` themselves, which is a client-side decision — [LiveKit: Subscribing to tracks](https://docs.livekit.io/home/client/tracks/subscribe/)
- Publications are visible to all clients even when not subscribed. A malicious client knows which track SIDs exist — [LiveKit: Receiving tracks](https://docs.livekit.io/home/client/receive)
- Permission changes revoke and re-issue the participant token, so a client can't reconnect with old (wider) permissions — [LiveKit Server APIs](https://docs.livekit.io/reference/server/server-apis/)
- Revoking `can_publish` unpublishes all of a participant's tracks. This is a server-side way to guarantee a focused (desk) player's mic reaches nobody — [LiveKit: Managing participants](https://docs.livekit.io/home/server/managing-participants/)
- The JS SDK exposes a subscription status `not-allowed`, which suggests the SFU rejects subscriptions that the publisher's track permissions don't allow. Whether the server enforces track subscription permissions is not stated explicitly in the docs I fetched — [LiveKit JS SDK: SubscriptionStatus](https://docs.livekit.io/reference/client-sdk-js/enums/trackpublication.subscriptionstatus.html); [LiveKit: Publishing tracks](https://docs.livekit.io/transport/media/publish/)
- An OpenVidu forum thread (OpenVidu 3 runs on LiveKit) reports new joiners in large rooms still seeing/subscribing to an unpublished track. It is unresolved, so treat it as anecdotal — [OpenVidu forum](https://openvidu.discourse.group/t/issue-with-unpublishing-tracks-remote-participants-still-see-tracks-in-large-meetings-in-openvidu-v3-with-livekit-single-node/5303)

### Inferences
- **Threat model for dg-village**: players aren't coworkers, so eavesdropping on a lounge from far away or on "focused" people is a real privacy concern, though low-stakes for a hobby game.
- **Strongest LiveKit setup** (to verify):
  1. Token with `can_subscribe=false` (if admin force-subscribe works under it), or `can_subscribe=true` combined with publisher-side track permissions set by the server.
  2. FastAPI drives all subscriptions via `UpdateSubscriptions`.
  3. Focus mode: speaker side via `can_publish=false` or `MutePublishedTrack`; listener side by unsubscribing everything.

  Server-side track permissions can't be set by the publisher's client if that client is malicious. That doesn't matter, because the attacker is the listener, not the publisher.
- If `can_subscribe=false` blocks admin subscription too, the fallback is `can_subscribe=true` + publisher-side `setTrackSubscriptionPermissions` driven by messages from FastAPI. That is enforced by the SFU against listeners, but relies on honest publisher clients. That's acceptable: a publisher only harms their own privacy.
- **Mesh**: the server can refuse to relay signaling, so a malicious client can't open new connections to far-away players. But once A↔B are connected, B's client keeps receiving A's audio until **A's** client closes the connection. An honest A client closes it when out of range, so the protection depends on the victim's client being honest, which is fine. A malicious B that ignores "disconnect" still gets cut off when A hangs up. Mesh is therefore "victim-client-enforced", acceptable for a hobby game.
- Client-side volume only (everyone subscribed to everyone, volume by distance) = **no** privacy. Avoid it.

### Gaps
- Server-side enforcement of `setTrackSubscriptionPermissions` and the `can_subscribe=false` + admin subscribe combination are not confirmed in docs. Both need a local experiment with `livekit-server --dev` + `lk` CLI.

## TURN: share of users needing relay, coturn vs LiveKit embedded TURN, bandwidth

### Takeaway
Roughly 10-20% of WebRTC connections need TURN. Sources are vendor blogs with unnamed data, so treat the range as soft. LiveKit ships an embedded TURN server (auth tied to the signal connection) plus ICE/TCP on port 7881, so a separate coturn isn't needed with an SFU. P2P mesh needs coturn (or a paid TURN service), and relayed mesh traffic multiplies the server bandwidth.

### Cited Findings
- "Almost 20% of WebRTC call connections require a TURN server" — vendor blogs, source data unnamed — [Metered: What is a TURN server](https://www.metered.ca/blog/what-is-a-turn-server/); [100ms: WebRTC TURN server](https://www.100ms.live/blog/webrtc-turn-server)
- EasyRTC docs estimate that 85-90% of connections don't need TURN (≈10-15% do). No source cited — [EasyRTC server ICE docs](https://github.com/gunjank/easyrtc/blob/master/docs/easyrtc_server_ice.md)
- Callstats.io (2016): 22% of *failed* calls required some form of media relay. Different denominator, not comparable — [No Jitter: Know Where to TURN](https://www.nojitter.com/video-conferencing/know-where-to-turn-when-deploying-webrtc)
- LiveKit includes a TURN server with built-in authentication: "only clients that have already established a signal connection could connect to our TURN server". TURN/TLS gives the broadest coverage, including corporate firewalls. TURN needs **its own domain and a CA-signed certificate**, and LiveKit terminates TURN/TLS itself (cert/key in config). Default `turn.tls_port` 3478, recommended 443 when no load balancer; `turn.udp_port` 443 for networks allowing UDP 443 — [LiveKit: Self-hosting deployment](https://docs.livekit.io/transport/self-hosting/deployment/)
- A community load test on a single 8-core host saw failures around 1,600 participants that were connection/TURN-related rather than CPU-bound — [LiveKit community forum](https://community.livekit.io/t/how-to-size-self-hosted-livekit-for-200-concurrent-classrooms-x-50-participants-10-000-total-what-hardware-config-do-i-need/1779)

### Inferences
- With LiveKit, most users connect over UDP 50000-60000 directly to the SFU. Users behind UDP-blocking networks fall back to ICE/TCP 7881 or TURN/TLS. Relay through LiveKit's embedded TURN runs on the same box, so the bandwidth is the same as direct SFU traffic (just a different port).
- With mesh + coturn, every relayed peer pair puts that pair's audio through the VPS: up to 2 × 50 kbps per relayed pair. With 20% relayed this is small for audio, but needs coturn setup (static-auth-secret, time-limited credentials issued by FastAPI, TLS on 5349/443, UDP relay port range).
- **Port conflict**: Caddy already owns TCP 443 (and UDP 443 if HTTP/3 is enabled, which is Caddy's default). LiveKit TURN/TLS on 443 would clash → use 5349 (or disable Caddy HTTP/3 and give TURN/UDP 443). sslip.io can provide a second hostname (e.g. `turn-13-54-176-157.sslip.io`) for the separate TURN cert.

### Gaps
- No primary measurement (e.g. Chrome UMA) of the TURN share was found. No Korea-specific data on symmetric NAT / corporate firewall share.

## Capacity and cost: LiveKit OSS on the Lightsail $12 (2 GB / 2 vCPU) Sydney instance for 10/30/50 users; latency Korea↔Sydney

### Takeaway
For audio only, bandwidth and CPU for 10-50 users are small compared with LiveKit's benchmarks (which push ~1M packets/s on 16 cores). A 2 vCPU box should handle tens of audio users, but it is burstable and shared with FastAPI/Postgres, and LiveKit gives no small-instance numbers, so load-test with `lk load-test`. The Sydney $12 plan has a **1.5 TB/month** allowance (half of 3 TB), with overage at **$0.17/GB**. "Keep open all day" usage with DTX should fit. The bigger problem is latency: Korea↔Sydney is about 145 ms RTT, so speaker→Sydney SFU→listener adds about 145 ms of network delay one-way (two legs of ~72 ms). Total mouth-to-ear lands around 200-250 ms, above the ITU 150 ms "transparent" mark.

### Cited Findings
- LiveKit audio benchmark (16-core c2-standard-16, ~3 kbps average audio bitrate): 10 publishers / 3,000 subscribers → 7.3 kB/s in, 23 MB/s out, 305 pkts/s in, 959,156 pkts/s out, ~80% CPU. Tested with `lk load-test` (simulated publishers send blank packets at a target bitrate) — [LiveKit: Benchmarks](https://docs.livekit.io/transport/self-hosting/benchmark/)
- LiveKit: scalability is limited by CPU and bandwidth. Use compute-optimized instances and host networking in Docker. Ports: 7880 (API/WS signal), 7881 TCP (ICE/TCP), 50000-60000 UDP (RTC), TURN 3478/5349/443, optional Prometheus 6789. Redis recommended for multi-node only. The docs give no specific capacity numbers — [LiveKit: Self-hosting deployment](https://docs.livekit.io/transport/self-hosting/deployment/)
- Community: a single 8-core / 7.8 GB host was stable at ~800 people across 20 rooms, with CPU at 60-68% at failure (load generator on the same machine) — [LiveKit community forum](https://community.livekit.io/t/how-to-size-self-hosted-livekit-for-200-concurrent-classrooms-x-50-participants-10-000-total-what-hardware-config-do-i-need/1779)
- Lightsail Linux $12 bundle: 2 GB RAM, 2 vCPU, 60 GB SSD, 3 TB transfer. Instances created on or before 2023-06-29 have 1 vCPU — [AWS Lightsail pricing](https://aws.amazon.com/lightsail/pricing/)
- Sydney (and Mumbai, Jakarta, Malaysia, Hong Kong, São Paulo) plans "include half the data transfer allowances" → $12 plan = **1.5 TB** in Sydney. Both IN and OUT count toward the allowance, but only excess OUT is charged. Sydney overage = **$0.17/GB** (Seoul $0.13, Tokyo $0.14, Singapore $0.12) — [AWS Lightsail data transfer FAQ](https://docs.aws.amazon.com/lightsail/latest/userguide/amazon-lightsail-faq-data-transfer-allowance.html)
- Seoul (ap-northeast-2) ↔ Sydney (ap-southeast-2) latency ≈ 144.7 ms (third-party aggregator); Seoul↔Tokyo ≈ 36 ms, Seoul↔Osaka ≈ 26 ms — [Economize: ap-northeast-2 vs ap-southeast-2](https://www.economize.cloud/resources/aws/latency/ap-northeast-2-vs-ap-southeast-2/)
- ITU-T G.114: one-way delay should not exceed 400 ms for general planning; ~150 ms is widely cited as the limit for transparent interactivity, and 150-400 ms is acceptable with more hesitation/talk-over — [ITU-T G.114 (2003) PDF](https://www.cs.columbia.edu/%7Eandreaf/new/documents/other/T-REC-G.114-200305.pdf); [Twilio glossary: latency](https://www.twilio.com/docs/glossary/what-is-latency)

### Inferences (my own estimates; verify with `lk load-test`)
- **Per-stream wire rate**: ~50 kbps (32 kbps Opus + headers at 50 pps). SFU ingress = 1 stream per speaker. Egress = Σ over listeners of audible speakers.

| Concurrent users | Assumed avg audible peers | SFU egress (all talking, worst case) | Packets/s out |
|---|---|---|---|
| 10 | 9 (all together) | 10×9×50 kbps ≈ 4.5 Mbps | ~4,500 |
| 30 | 8 | 30×8×50 ≈ 12 Mbps | ~12,000 |
| 50 | 10 | 50×10×50 ≈ 25 Mbps | ~25,000 |

  Compared with the benchmark's ~960k pkts/s on 16 cores (~60k pkts/s per core at 80%), 25k pkts/s is a small fraction of one core. CPU looks feasible on 2 vCPUs even with FastAPI/Postgres, as long as the burstable CPU baseline isn't exhausted.
- **Silence**: with Opus DTX (silent users send ~1 packet/400 ms) and people mostly working silently, real averages should be far below the "all talking" column.
- **Monthly transfer** (worst case, no DTX): 30 users × 8 h/day × 30 days at 12 Mbps ≈ 5.4 GB/h × 240 h ≈ 1.3 TB, close to the 1.5 TB Sydney allowance (inbound also counts). Overage at $0.17/GB ≈ $17 per extra 100 GB. With DTX and realistic talk ratios (say 10-20% talk time), expect a few hundred GB or less. 50 users always talking for 8 h/day would be ~2.7 TB → ~$200/month of overage, so DTX and unsubscribe-when-out-of-range matter.
- **RAM**: LiveKit is a single Go binary. Tens of audio participants should need only a few hundred MB, but no official number was found. 2 GB shared with Postgres + FastAPI + Caddy is tight but plausible; watch it.
- **Latency**: Korea→Sydney→Korea is ~145 ms network for a speaker→listener path, plus ~20 ms frame + 40-80 ms jitter buffer + device latency → roughly 200-250 ms mouth-to-ear. That's in G.114's "acceptable but noticeable" band: OK for casual chat, slightly laggy for fast back-and-forth. Korea↔Korea P2P would be ~10-40 ms network. Seoul/Tokyo hosting would fix this but conflicts with the AWS free-credit Sydney lock (owner context). A cheap Seoul/Tokyo VPS from another provider just for LiveKit is an option.
- **Operational burden on the existing VPS**:
  - Open UDP 50000-60000 (or a narrower range, e.g. 50000-50200 for small scale), TCP 7881, and TURN port(s) in the Lightsail firewall.
  - Run LiveKit with `network_mode: host`.
  - Put the signal endpoint (7880) behind Caddy (`livekit.<domain>` → reverse_proxy with WebSocket); Caddy handles TLS.
  - Second hostname + cert for TURN/TLS.
  - API key/secret in `deploy/.env`.
  - FastAPI issues LiveKit tokens (new endpoint, e.g. `POST /voice/token`) and calls `update_subscriptions`.

### Gaps
- No official LiveKit sizing for small (2 vCPU) instances, and no RAM figure.
- Lightsail burstable CPU baseline percentage for the $12 plan was not checked this session. Sustained SFU load could exhaust burst credits.
- The latency figure is from a third-party aggregator, not measured from Korean ISPs.

## Implementation effort / complexity for a solo dev (summary across options)

### Takeaway
For this project, LiveKit OSS (self-hosted, one container next to the existing stack) + LiveKit Unity SDK + FastAPI-driven `UpdateSubscriptions` is the lowest-effort path that also gives server-enforced privacy. P2P mesh avoids a media server, but needs a Unity WebRTC binding, coturn, and churn handling, and has weaker privacy and lounge scaling. mediasoup/Janus/Pion are toolkits that need much more custom code and lack a first-party Unity SDK.

### Cited Findings
- LiveKit provides a server with rooms/auth/TURN built in, server APIs for subscriptions/permissions, Python server SDK, and a Unity client SDK (desktop + mobile; WebGL via `client-sdk-unity-web`) — [LiveKit Server APIs](https://docs.livekit.io/reference/server/server-apis/); [LiveKit: Self-hosting deployment](https://docs.livekit.io/transport/self-hosting/deployment/); [GitHub: livekit/client-sdk-unity](https://github.com/livekit/client-sdk-unity)
- mediasoup = minimal media layer as a Node.js module, signaling agnostic (rooms/signaling/auth are on you) — [mediasoup overview](https://mediasoup.org/documentation/overview/)
- Mesh tops out around 4-6 participants for video, and its uplink needs grow with N — [BlogGeek.me glossary: Mesh](https://bloggeek.me/webrtcglossary/mesh)

### Inferences
| Option | Server work | Client work (Unity) | Privacy enforcement | Ops burden | Fit |
|---|---|---|---|---|---|
| P2P mesh + coturn | WS relay of SDP/ICE + zone gate; coturn + temp creds | Unity WebRTC binding + WebGL JS bridge, PeerConnection lifecycle, hysteresis | Victim-client-enforced (OK) | coturn ports + TLS | OK for ≤~8 audible peers; lounges hurt |
| LiveKit OSS (self-host) | Token endpoint + subscription diff loop via `livekit-api` | LiveKit Unity SDK, map remote tracks → AudioSource | Server-driven subscriptions (verify can_subscribe=false) | One container, UDP range, TURN cert | **Best effort/benefit** |
| mediasoup | Node sidecar + own signaling/rooms/auth | No official Unity SDK (libmediasoupclient C++ binding) | Server-enforced (you write it) | Node service + ports | High effort |
| Janus / Pion(ion) | Plugin/own SFU code | No official Unity SDK | Server-enforced (you write it) | Similar | High effort; not verified this session |

- Suggested first step: run `livekit-server --dev` locally, prove the "FastAPI drives `update_subscriptions` from `Room.hearers()`" loop with 2 Unity clients, and test the `can_subscribe=false` + admin subscribe question before committing to the design.

### Gaps
- Effort estimates are qualitative (no sourced time figures).
- WebGL path (`client-sdk-unity-web`) maturity was not examined.
