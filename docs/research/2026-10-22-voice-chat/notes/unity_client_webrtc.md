# Unity 6 client voice libraries: WebRTC feasibility on desktop (macOS arm64, Windows) and WebGL

Research date: 2026-10-22. GitHub data (commit/release dates) pulled live via `gh api` on that date.

## 1. com.unity.webrtc: version, platforms, audio-only use, maintenance, AEC

### Takeaway
com.unity.webrtc is **deprecated**: the README got a deprecation note on 2026-08-18. The last release is 3.0.0 (libwebrtc M116, Sept 2025). It supports Windows x64 and Apple Silicon but **not WebGL**. It has **no echo cancellation / noise suppression API**, and the AEC issues have been open for years. Don't build new voice work on it.

### Cited Findings
- README says verbatim "com.unity.webrtc is deprecated." The last commit is `readme: add deprecation note (#1124)` on 2026-08-18. It names no voice replacement and only points to Unity Render Streaming for streaming. — [GitHub repo](https://github.com/Unity-Technologies/com.unity.webrtc) (commit date via `gh api repos/Unity-Technologies/com.unity.webrtc/commits`)
- A Japanese tech blog also reports the deprecation annotation on 2026-08-18. It notes that Render Streaming still pins 3.0.0-pre.8. — [issoh.co.jp](https://www.issoh.co.jp/md/tech/1476)
- Latest version is 3.0.0, the "Release verified version" with libwebrtc M116, released Sept 2025. Earlier releases: 3.0.0-pre.8 (Dec 2024). The last GitHub release asset is tagged `M116-20250805` (published 2025-08-13). The repo has 117 open issues and 17 open PRs. — [GitHub repo](https://github.com/Unity-Technologies/com.unity.webrtc)
- Requirements page for 3.0: Unity Editor **6000.3** only. Platforms: Windows 10 x64 only, Ubuntu 22.04/24.04, macOS Apple Silicon, iOS, Android ARM64 only. "**WebGL** platform is not supported." — [Unity docs: requirements](https://docs.unity3d.com/Packages/com.unity.webrtc@3.0/manual/requirements.html)
- Apple Silicon support arrived in 2.4.0-exp.4. — [GitHub roadmap](https://github.com/Unity-Technologies/com.unity.webrtc)
- Audio-only usage: send with `new AudioStreamTrack(audioSource)` + `peerConnection.AddTrack`. Receive with `OnTrack` → `AudioSource.SetTrack(track)`, and set `loop = true`. The no-argument constructor takes `AudioListener` / manual `SetData`, which "is supposed to be called on the audio thread" from `OnAudioFilterRead`. The sample uses 48000 Hz. The docs say nothing about Microphone capture or echo cancellation. — [Unity docs: audio streaming](https://docs.unity3d.com/Packages/com.unity.webrtc@3.0/manual/audiostreaming.html)
- `AudioStreamTrack.Loopback` controls local playback of the sender track (sender side only). — [Unity docs AudioStreamTrack 3.0](https://docs.unity3d.com/ja/Packages/com.unity.webrtc@3.0/api/Unity.WebRTC.AudioStreamTrack.html)
- Open AEC issues:
  - [#964 "The sound is captured back to the microphone"](https://github.com/Unity-Technologies/com.unity.webrtc/issues/964) (2023-08, 3.0.0-pre.6). The reporter tried AEC / echo suppression: "it's not work for Unity".
  - [#651 "Remove echo on iOS"](https://github.com/Unity-Technologies/com.unity.webrtc/issues/651)
  - [#587 "Configurable audio options (noise suppression, auto gain, etc.)"](https://github.com/Unity-Technologies/com.unity.webrtc/issues/587)
  - PR [#978 "Add AudioOptions class"](https://github.com/Unity-Technologies/com.unity.webrtc/pull/978) is still open (unmerged).

### Inferences
- Unity mic input goes through Unity's `Microphone` → `AudioSource` → WebRTC. That path bypasses libwebrtc's audio device module, so the built-in AEC3/NS/AGC never applies. Speakers + open mic ("keep open while working") would echo.
- The package is deprecated and pinned to 6000.3 docs, so newer Unity 6.x versions (the project uses 6000.6) may break with no fix coming.
- You would also need to write signaling (fine over our FastAPI WS) plus an SFU/mesh yourself. Mesh only works for a handful of peers.

### Gaps
- Couldn't confirm whether the 3.0.0 binary works on Unity 6000.6 specifically, and found no community reports either way.
- No official Unity statement explains the reason for the deprecation or names a replacement for voice. It may be related to Vivox being Unity's voice product, but that is not confirmed.

## 2. WebGL: workarounds and whether one library covers desktop + WebGL

### Takeaway
No single package covers native desktop + WebGL with the same binary. The common pattern is **two SDKs behind one API**: a native SDK for desktop plus a jslib bridge to the browser's RTCPeerConnection for WebGL. LiveKit (client-sdk-unity + client-sdk-unity-web) and Agora (official + community WebGL) both follow it. In WebGL, the browser's own `getUserMedia` provides AEC/NS/AGC.

### Cited Findings
- LiveKit's native Unity SDK README: WebGL is unchecked in its own platform list. "WebGL is currently supported with client-sdk-unity-web." "We plan to support all Unity platforms with this SDK." — [livekit/client-sdk-unity README](https://raw.githubusercontent.com/livekit/client-sdk-unity/main/README.md)
- client-sdk-unity-web is the "Official LiveKit SDK for Unity WebGL" (81 stars, 11 open issues). Two modes: C#-only (ExampleRoom sample) or a JS bridge via the npm package `@livekit/livekit-unity`. Flow: `Room` → connect(url, token) → `EnableCameraAndMicrophone()`. — [livekit/client-sdk-unity-web](https://github.com/livekit/client-sdk-unity-web)
- client-sdk-unity-web latest release is **v2.0.0 (2024-09-03)**. The last commit is 2026-09-16, a README-only change ("Update README.md with Agents block"). — `gh api repos/livekit/client-sdk-unity-web/releases` / commits
- client-sdk-unity-web's C# wraps JS through `JSNative.CallMethod`. Example: `RemoteAudioTrack.SetVolume(float)` / `GetVolume()` and `RemoteParticipant.SetVolume` call JS `setVolume`. — [RemoteAudioTrack.cs](https://github.com/livekit/client-sdk-unity-web/blob/main/Runtime/Scripts/Room/Track/RemoteAudioTrack.cs), [RemoteParticipant.cs](https://github.com/livekit/client-sdk-unity-web/blob/main/Runtime/Scripts/Room/Participant/RemoteParticipant.cs)
- LiveKit docs: on Unity WebGL, video playback needs an `HTMLVideoElement`. — [LiveKit docs: subscribing](https://docs.livekit.io/home/client/tracks/subscribe/)
- Browsers have no UDP socket access, so native voice plugins can't simply be recompiled for web. Browser voice relies on WebRTC. — [Unity forum: voice chat in WebGL](https://forum.unity.com/threads/voice-chat-in-webgl.900377/)
- Agora_Unity_WebGL is a **community** WebGL plugin wrapping Agora Web SDK 4.x and is in "open beta". It requires Chrome, is "not compatible with the official Agora Unity SDK 4.x", and warns that Spatial Audio may not work. "The Agora Support team does not assume responsibility." — [AgoraIO-Community/Agora_Unity_WebGL](https://github.com/AgoraIO-Community/Agora_Unity_WebGL)
- Browser mic: `echoCancellation` is a standard getUserMedia constraint that reduces crosstalk between output and input devices. — [MDN echoCancellation](https://developer.mozilla.org/en-US/docs/Web/API/MediaTrackSettings/echoCancellation)

### Inferences
- A hand-rolled jslib bridge to RTCPeerConnection is feasible for P2P audio. Play audio through browser `<audio>`/WebAudio rather than Unity's audio, and you get browser AEC for free. It is still extra work on top of the desktop path, and Unity's WebGL audio can't easily take remote streams. The LiveKit web SDK plays audio in the browser layer, which suggests this design.
- With LiveKit, the C# APIs differ slightly between the two packages (namespaces, async patterns, and `JSNative` handles vs FFI handles), so expect `#if UNITY_WEBGL` wrappers.
- The web SDK has had no release for about 2 years. Treat it as "works but slow-moving" and verify before committing.

### Gaps
- Couldn't verify that client-sdk-unity-web v2.0.0 builds cleanly on Unity 6000.6. Its README states no Unity version.
- Found no maintained open-source "one API for both" wrapper.

## 3. LiveKit Unity SDK (client-sdk-unity, Rust FFI): platforms, maturity, spatial audio, selective subscription, per-track volume, AEC

### Takeaway
This is the most active and capable option for desktop. It shipped v2.1.0 on 2026-09-23 and is tested on Unity 6000.3.10. Platforms: Windows/macOS/Linux/iOS/Android. It has `SetSubscribed` for per-track subscribe/unsubscribe. Remote audio goes through a Unity `AudioSource`, so Unity's 3D spatialization and volume apply. **AEC on the Unity Audio path was added in v2.1.0.** Platform Audio mode uses libwebrtc's native AEC/NS/AGC.

### Cited Findings
- "We officially support Unity 2022 onwards, we test on Unity 2022.3.62 and Unity 6000.3.10." Platforms: Windows, macOS, Linux, iOS, Android (WebGL is a separate package). Apache-2.0. Install via git URL `...client-sdk-unity.git#vX.X.X` (OpenUPM is documented too). — [README](https://raw.githubusercontent.com/livekit/client-sdk-unity/main/README.md)
- Releases: **v2.1.0 (2026-09-23)**, v2.0.0 (2026-07-20), v1.4.0 before that. v2.1.0 includes "AEC (Acoustic Echo Cancellation) in Unity Audio path" (#381), "Fix iOS build on Unity 6.5", and "Don't stop destroyed audio sources". v2.0.0 includes "Surface FFI panic events instead of silently dropping them" and UniTask support. — [GitHub releases](https://github.com/livekit/client-sdk-unity/releases)
- Two audio modes:
  - **Platform Audio** uses the native WebRTC audio device module, which gives echo cancellation, noise suppression and AGC.
  - **Unity Audio** uses the Unity `Microphone` and `AudioSource` APIs. "Unity's `Microphone` path does not go through a platform audio device module, so on its own it has no echo cancellation." Passing `AudioProcessingOptions` enables libwebrtc AEC3. Its reference signal comes from a `PlayoutReference` component the SDK attaches to the active `AudioListener`.
  - "Unity's output sample rate must be a multiple of 100 Hz (48000, 44100 and 24000 all are)", otherwise processing is bypassed.

  — [README](https://raw.githubusercontent.com/livekit/client-sdk-unity/main/README.md)
- Known Platform Audio issues: "On MacOS with bluetooth headset, unmuting can break audio output". On iOS, disposing Platform Audio stops Unity audio output, and on iOS + Unity 6, backgrounding breaks it. — [README](https://raw.githubusercontent.com/livekit/client-sdk-unity/main/README.md)
- Remote audio on the Unity Audio path: `new AudioStream(audioTrack, audioSource)` on a GameObject's `AudioSource`. "Continuations resume on Unity's main thread." — [README](https://raw.githubusercontent.com/livekit/client-sdk-unity/main/README.md)
- With Platform Audio, "remote audio plays back automatically and needs no extra handling". With Unity Audio, attach each remote track to an `AudioSource` and keep/dispose the `AudioStream` on unsubscribe. — [LiveKit docs: subscribing](https://docs.livekit.io/home/client/tracks/subscribe/)
- Selective subscription: disable `autoSubscribe` and call `publication.setSubscribed(true/false)`. A server API `updateSubscriptions` also exists. Audio volume is per track, 0–1.0. — [LiveKit docs: subscribing](https://docs.livekit.io/home/client/tracks/subscribe/)
- Unity SDK `TrackPublication.SetSubscribed(bool)` is implemented via an FFI `SetSubscribedRequest`. — [TrackPublication.cs](https://github.com/livekit/client-sdk-unity/blob/main/Runtime/Scripts/Core/TrackPublication.cs)

### Inferences
- **Proximity voice**:
  - **Unity Audio mode**: put each remote `AudioSource` on the remote player's GameObject with `spatialBlend = 1` and a custom rolloff. That gives distance attenuation, panning and per-player volume for free. Add the v2.1.0 AEC3 for speaker use.
  - **Platform Audio mode**: better device AEC, but audio bypasses Unity, so no Unity spatialization. Volume would need an SDK API, and the README doesn't document one.
- Zones (desk focus / lounge / hallway) map naturally onto `SetSubscribed` (or server-side `updateSubscriptions` driven by our Python server, which already knows who hears whom). That saves bandwidth, not just volume.
- AEC in Unity Audio mode is brand new (one release old). Test it with real speakers on Mac and Windows.
- The Rust FFI uses native threads internally. That's fine on desktop and irrelevant on WebGL, which uses the separate JS-based package.
- LiveKit needs a LiveKit server (self-host the Go SFU, or LiveKit Cloud) plus tokens minted by our server. A Python `livekit-api` package exists, but that is not verified in this note.

### Gaps
- The README doesn't document a per-track volume API for the native SDK. On the Unity Audio path, `AudioSource.volume` covers it.
- The README doesn't mention spatial audio. The spatialization path above is inferred from the AudioSource design.
- No version stability label ("beta"/"stable"). The 2.x major bump suggests API churn.

## 4. Other options: Photon Voice 2, Vivox, Agora, Dissonance (platforms, WebGL, AEC)

### Takeaway
- **Vivox**: official WebGL support since 16.5.0, but "Limited": **no 3D positional channels** on WebGL and one channel per instance.
- **Photon Voice 2**: no clearly documented WebGL support (only scattered forum workarounds).
- **Agora**: WebGL only through a community beta plugin.
- **Dissonance**: no evidence of WebGL support.

All the native SDKs offer AEC on desktop in some form.

### Cited Findings
- **Vivox WebGL**: available from 16.5.0 at a "Limited" support level. Limitations: single-channel joins per instance, no display names, **no 3D positional channels**, no participant-updated events (audio energy, talking, mute), and audio device control limited by the browser. Unimplemented features throw `NotImplementedException`. HTTPS required, Firefox recommended for local tests. The Vivox Core Web SDK is not publicly available. — [Unity Vivox WebGL docs](https://docs.unity.com/ugs/en-us/manual/vivox-unity/manual/Unity/developer-guide/vivox-webgl) (search-result summary; the page returned 503 on fetch), [Unity support article](https://support.unity.com/hc/en-us/articles/4418757732244-Does-Vivox-support-Web-applications-WebGL)
- **Photon Voice 2**:
  - Capture/playback uses Unity APIs by default. Photon native mic modules enable hardware processing including AEC.
  - Using both the Photon mic and `WebRtcAudioDsp` AEC causes double processing. The warning says "Please use only one echo algorithm".
  - Echo persists on some Android devices.

  — [Photon Voice docs](https://doc.photonengine.com/voice), [Photon forum: echo](https://forum.photonengine.com/discussion/comment/51265/), [Photon forum: noise & echo](https://forum.photonengine.com/discussion/14804/how-to-reduce-noise-echo-when-using-photon-voice)
- **Photon Voice WebGL**:
  - Staff (2019–2022): "Photon Voice will not support WebGL", WebRTC planned with "no schedule". Later: working on an alternative, "no ETA".
  - Version history v2.52 (May 2023): the Recorder uses the Photon mic type on WebGL builds.
  - One user got WebGL audio working by switching the protocol to WSS.
  - The forum is now read-only.

  — [Photon forum WebGL request](https://forum.photonengine.com/discussion/20686/request-unity-webgl-support-for-photon-voice), [WebGL and Photon Voice](https://forum.photonengine.com/discussion/comment/54048), [Version history](https://doc.photonengine.com/voice/current/reference/version-history)
- Photon Voice 2 reportedly had huge noise on macOS (user report). — [Photon forum](https://forum.photonengine.com/discussion/21980/photon-voice-2-got-huge-noise-on-macos)
- **Agora**: the official Unity SDK markets low latency, spatial sound and voice changing, with no WebGL mention. WebGL exists only via the community beta plugin (see section 2). Users in a ~2022–23 Unity thread called it "not 100% functional for advance usage". — [Agora Unity partner page](https://www.agora.io/en/partners/unity/), [Unity Discussions: Vivox WebGL](https://discussions.unity.com/t/does-vivox-support-webgl-build/871913)
- **Dissonance**: its docs list Opus, positional audio and push-to-talk, with no WebGL mention. A forum user said Dissonance doesn't work on WebGL (anecdotal). — [Dissonance docs](https://dissonance.readthedocs.io/), [Unity Discussions](https://discussions.unity.com/t/does-vivox-support-webgl-build/871913)

### Inferences
- If WebGL proximity voice matters, Vivox's missing 3D channels on WebGL is a real blocker. You'd have to fake proximity by switching channels (only one allowed per instance).
- Photon Voice, Agora (official) and Dissonance are effectively desktop/mobile-only for us.
- LiveKit is the only option with official packages for both desktop and WebGL that keeps the same server and room model.

### Gaps
- Couldn't fetch the Vivox supported-platforms page (503), so macOS arm64 / Unity 6000.6 support for Vivox is unconfirmed here.
- No primary 2026 source on Photon Voice WebGL status. Photon's docs pages returned 404 on the paths tried.
- Dissonance's official platform list wasn't retrieved.

## 5. AEC / noise suppression per option, and threading issues on WebGL

### Takeaway
On desktop, reliable AEC comes from:
- LiveKit Platform Audio (native ADM)
- LiveKit Unity Audio + AEC3 (new in v2.1.0)
- Photon native mic or WebRtcAudioDsp
- Vivox (built in, not verified here)

com.unity.webrtc has none. In WebGL, the browser's getUserMedia does AEC/NS/AGC for any WebRTC-in-browser approach. No viable option needs C# threads in WebGL. The WebGL SDKs delegate to browser JS, and native SDKs' internal threads exist only on desktop.

### Cited Findings
- LiveKit Platform Audio gives AEC + NS + AGC. Unity Audio needs `AudioProcessingOptions` (AEC3) and a sample rate that is a multiple of 100 Hz. — [LiveKit Unity README](https://raw.githubusercontent.com/livekit/client-sdk-unity/main/README.md)
- com.unity.webrtc exposes no audio processing options. The feature request (#587) and PR (#978) are open/unmerged. — [#587](https://github.com/Unity-Technologies/com.unity.webrtc/issues/587), [#978](https://github.com/Unity-Technologies/com.unity.webrtc/pull/978)
- com.unity.webrtc `SetData` is called from the audio thread (`OnAudioFilterRead`). — [Unity docs](https://docs.unity3d.com/Packages/com.unity.webrtc@3.0/manual/audiostreaming.html)
- Photon: native mic modules give hardware AEC. Software `WebRtcAudioDsp` AEC/NS is also available, but don't stack both. — [Photon forum](https://forum.photonengine.com/discussion/comment/51265/)
- An old Photon forum thread discusses Photon Voice with multithreaded WebAssembly in Unity 2019.1, which shows threading was a WebGL concern for its native DSP. — [Photon forum](https://forum.photonengine.com/discussion/comment/45950/)
- LiveKit web SDK uses `JSNative` calls into the browser JS SDK, with no native threads. — [client-sdk-unity-web source](https://github.com/livekit/client-sdk-unity-web/blob/main/Runtime/Scripts/Room/Track/RemoteAudioTrack.cs)

### Inferences
- For the "keep open while working, speakers on" use case, the safest desktop choice is LiveKit:
  - Platform Audio = best AEC, but no Unity spatialization.
  - Unity Audio + AEC3 = spatialization plus new AEC that needs testing.

  Server-side zone logic via subscriptions can replace client spatialization when there is no Unity spatialization.
- The project's "no threads in client networking code" rule is met on WebGL by any JS-bridged option. On desktop, the native SDKs' own threads don't violate it, since our C# stays on the main thread.

### Gaps
- No measured AEC quality comparisons between options. Vivox desktop AEC wasn't verified in this session.
