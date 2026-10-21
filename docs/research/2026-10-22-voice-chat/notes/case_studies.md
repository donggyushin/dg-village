# 근접 음성/화상 채팅 실제 구현 사례 (Case Studies)

조사일: 2026-10-22. 출처의 날짜를 가능한 한 표기. 검색은 약 15회로 제한.

## Gather.town: P2P에서 SFU로? 근접 "버블", private area, quiet/focus mode

### Takeaway
현재 Gather는 **자체 개발(in-house) SFU**를 여러 데이터센터에서 직접 운영하고, 클라이언트마다 여러 SFU 연결을 맺던 구조를 **SFU cascading**(SFU끼리 backhaul로 영상 전달, 클라이언트는 1개 연결)으로 바꾸는 중이었다. mediasoup 사용 근거는 없음. 근접 판정은 타일 기반(기본 약 5타일, quiet mode 1타일), private area = 영역 안끼리만 연결, spotlight = 방 전체 방송.

### Cited Findings
- Gather 채용공고(Staff Backend Engineer, Video; 게시일 불명, "6+ months ago", 마감): "Our self-operated video infrastructure spans 5 data centers, and multiple clouds, and features an SFU design built in-house." 동시 사용자 수천 명, 전 대륙. — [Gather job posting (MIT Orbit)](https://jobs.orbit.mit.edu/companies/gather-2-674309dd-fe8e-47c7-8df5-bcd9b2a88b57/jobs/31180045-staff-backend-engineer-video)
- 같은 공고: 핵심 과제 = SFU cascading — 클라이언트가 여러 SFU에 동시에 연결하던 것을 클라이언트당 1개 연결로 바꾸고, SFU 인스턴스끼리 backhaul로 영상 전달. 목표: 수백 명 동시 미팅. — [Gather job posting](https://jobs.orbit.mit.edu/companies/gather-2-674309dd-fe8e-47c7-8df5-bcd9b2a88b57/jobs/31180045-staff-backend-engineer-video)
- 같은 공고: 방(room) 기반 시스템과 달리 "임의의 두 사용자가 즉시 오디오/비디오 연결"을 맺을 수 있어야 함 → 대형 이벤트에서도 주변 사람과 대화 가능. — [Gather job posting](https://jobs.orbit.mit.edu/companies/gather-2-674309dd-fe8e-47c7-8df5-bcd9b2a88b57/jobs/31180045-staff-backend-engineer-video)
- 다른 버전의 공고에서는 데이터센터 "10개"로 표기(시점 차이로 보임). — [Dynamite Jobs posting](https://dynamitejobs.com/company/gather/remote-job/staff-backend-engineer-video)
- Gather가 mediasoup을 쓴다는 근거는 찾지 못함. 경쟁사(Meta Town) 비교 페이지가 Gather를 "Mixed architecture"로 표현했지만 마케팅 자료. — [Meta Town compare](https://metatown.ai/compare/gather-town)
- 근접 동작: 가까워지면 상대 영상이 뜨고, 멀어지면 오디오/비디오가 흐려지다 사라짐. Private area 안의 대화는 밖에서 들리지 않음(가까워도). Spotlight 칸에서 말하면 방 전체로 방송. — [OIST Gather quick start (PDF)](https://groups.oist.jp/sites/default/files/imce/u111990/Quick%20start%20to%20gather.town%20%281%29.pdf)
- 기본 연결 범위: 열린 공간에서 5타일 이내 모두와 연결, quiet mode가 아닌 경우. Quiet mode = 1타일 이내 아바타와만 연결. — [NOAA GatherTown Readme 2022 (PDF)](https://esrl.noaa.gov/gmd/publications/annual_meetings/2022/GatherTown%20Readme.pdf), [NIES Gathertown guidelines (PDF)](https://www.nies.go.jp/soc/doc/IWGGMS-18/GathertownGuidelines.pdf)
- 단축키 Ctrl/⌘+U = 이벤트 스페이스에서는 quiet mode, 리모트워크 스페이스에서는 **do-not-disturb** 토글. — [Cancer.gov Gather guide (PDF)](https://events.cancer.gov/sites/default/files/assets/cgh-ascgr/GatherGuide%20508%5B39%5D.pdf)
- Gather 데스크톱 앱 Mini Mode의 "주변 사람" = 7타일. — [Gather support: Desktop Mini Mode](https://support.gather.town/articles/3720395050-desktop-mini-mode)

### Inferences
- Gather의 근접 모델 = "거리 내 상대와만 트랙 구독"으로 보는 것이 자연스럽고, 서버 측 SFU에서 구독 대상을 바꾸는 방식이면 "즉시 연결"이 가능. 초기(2020)에는 P2P였다는 이야기가 흔하지만 1차 출처를 이번 조사에서 확인하지 못함.
- dg-village의 Desk(focus) = Gather의 quiet mode/DND, Lounge = private area, Hallway = 기본 5타일 근접과 거의 1:1 대응. 서버가 이미 `Room.hearers()`로 판정하므로 같은 로직을 음성 구독 목록에 재사용 가능.
- 대형 서비스도 결국 SFU로 수렴하며, 자체 SFU 운영은 전담 팀이 필요한 수준(5~10개 DC, cascading) → 1인 취미 프로젝트는 관리형 SFU(LiveKit Cloud 등)가 현실적.

### Gaps
- Gather 공식 엔지니어링 블로그에서 P2P→SFU 전환 시점/이유를 다룬 글은 찾지 못함(검색 2회 실패).
- 거리 기반 볼륨 감쇠가 클라이언트에서인지, 구독 on/off만인지 1차 출처 없음.
- Gather 2.0(2024~) 이후 미디어 스택 변경 여부 미확인.

## ZEP (네이버제트 × 슈퍼캣): 미디어 스택

### Takeaway
ZEP은 웹(Phaser) 기반이며 **WebRTC를 내재화**했다고 2026-08 AWS 블로그에서 밝힘. 그러나 Agora/mediasoup/LiveKit 중 무엇인지, 자체 SFU인지는 공개 자료로 확인 불가.

### Cited Findings
- "400만 MAU 규모의 서비스 환경에서 WebRTC 기술을 내재화해 안정적인 동시 스트리밍 경험을 제공" + 웹 공간은 Phaser 렌더링, 대규모 동시접속 게임 서버 사용. (2026-08-31) — [AWS 기술 블로그: ZEP 퀴즈 생성 아키텍처](https://aws.amazon.com/ko/blogs/tech/zep-quiz-generation-bedrock-agentcore/)
- ZEP 프론트엔드 채용 공고: "WebRTC 사용으로 인한 발열/쓰로틀링 문제 해결"이 업무로 명시 → 브라우저 WebRTC를 직접 다룸. — [원티드: 젭 프론트엔드 개발자](https://www.wanted.co.kr/wd/276144)
- 베타 공개(2021-11) 기사: 슈퍼캣의 대용량 트래픽 처리 기술로 한 공간 최대 5만 명 동시 접속, 웹 기반(설치 불필요), 아바타 간 거리가 가까우면 화상·음성 공유. — [전자신문 2021-11-30](https://m.etnews.com/20211130000230), [디지털투데이](https://www.digitaltoday.co.kr/news/articleView.html?idxno=426586)
- 출시 8개월(2022-11) 누적 300만, 비디오챗 소통 140만 회. — [바이라인네트워크 2022-11](https://byline.network/2022/11/%eb%a9%94%ed%83%80%eb%b2%84%ec%8a%a4-zep-%ec%9e%98%eb%82%98%ea%b0%80%eb%84%a4-%ec%b6%9c%ec%8b%9c-8%ea%b0%9c%ec%9b%94%ec%97%90-%eb%88%84%ec%a0%81-300%eb%a7%8c%eb%aa%85-%eb%8f%8c%ed%8c%8c/)
- ZEP은 Gather의 private area와 같은 "PA(Private Area)" 설정 기능을 제공(사용자 가이드). — [ZEP Blog: PA 설정하는 방법](https://blog.zep.us/user-guide-ko/%EB%82%98%EB%A7%8C%EC%9D%98-%EA%B3%B5%EA%B0%84-%EC%9A%B0%EB%A6%AC%EB%A7%8C%EC%9D%98-%EA%B3%B5%EA%B0%84%EC%9D%B4-%ED%95%84%EC%9A%94%ED%95%A0-%EB%95%8C-pa-%EC%84%A4%EC%A0%95%ED%95%98%EB%8A%94/)

### Inferences
- "5만 명 동시접속"은 위치/게임 서버 쪽 수치이고 화상과는 별개일 가능성이 큼(화상은 근접 그룹 단위라 동시 스트림은 소수).
- 발열/쓰로틀링 이슈 = 다수 비디오 디코딩 비용. 음성만 먼저 하는 dg-village에는 덜 중요하지만, 비디오 도입 시 "보이는 사람만 구독" 필요.

### Gaps
- ZEP의 미디어 서버(자체 SFU vs 상용 SDK) 공개 자료 없음. NAVER D2/if(kakao)/velog에서도 이번 검색으로는 발견 못함.

## WorkAdventure (오픈소스): P2P 버블 → LiveKit, Jitsi 회의실

### Takeaway
가장 문서화가 잘 된 사례. 버블은 **full-mesh WebRTC P2P(최대 4명)**로 시작해서 **5번째 사람이 들어오면 LiveKit SFU로 자동 전환**(v1.27.0, 2025-11-07). 전환 시 영상은 ~200ms 끊기고 음성은 유지. 직접 연결 실패 시 TURN(coturn, Cloudflare TURN) 사용.

### Cited Findings
- v1.27.0(2025-11-07): 이전 버블은 4명 제한. full-mesh에선 각 사용자가 모든 사용자에게 영상을 보내므로 "doesn't scale well". 이제 버블은 full-mesh로 시작해 5번째 사용자 입장 시 LiveKit으로 업그레이드, "short 200ms glitch" in video, 오디오는 끊김 없음. 버블 모양도 원형 → 아바타 출입에 따라 휘는 유기적 형태. — [WorkAdventure 1.27.0 release](https://workadventu.re/release/workadventure-1-27-0-the-road-to-workadventure-2/)
- 같은 글: LiveKit = UI 없는 저수준 오픈소스 SFU, 깊게 통합 가능 → 네이티브 회의실. 회의실은 ~30명 이하 권장, 그 이상은 podium/audience(발표자만 송출) 영역. Jitsi 회의실은 유지, 기능 동등(녹화 등) 후 자동 마이그레이션 예정. 애니메이션 가상배경은 추가 데이터를 보내므로 절제 권고. — [WorkAdventure 1.27.0 release](https://workadventu.re/release/workadventure-1-27-0-the-road-to-workadventure-2/)
- 이전 시스템은 P2P 기반으로 "effective but limited"; Jitsi와 LiveKit 모두 SFU. — [WorkAdventure: improving communication experience](https://workadventu.re/article-en/improving-the-virtual-conversation-and-communication-experience-with-workadventure/)
- 네트워크 문서: 버블(또는 5명 미만 회의실)에서는 WebRTC 직접 연결 시도, UDP + NAT traversal, 같은 LAN이면 인터넷 대역폭 절약. 실패 시 TURN relay: `coturn.workadventure.fr`(UDP 443, 10000-20000 / TCP 443), `turn.cloudflare.com`(UDP 3478 / TCP 443, 5349), STUN `stun.cloudflare.com`. UDP 차단 금지 권고(TCP 폴백은 지연이 눈에 띔). 대형 컨퍼런스나 서버 과부하 시 LiveKit Cloud로 오프로드. 패킷 검사/프록시/QoS 회피 권고. — [WorkAdventure docs: Preparing your network](https://docs.workadventu.re/admin/preparing-your-network)
- FAQ에 "버블/회의실에서 말이 안 들림" 항목이 따로 있을 만큼 연결 문제(방화벽)가 흔한 지원 이슈. — [WorkAdventure FAQ](https://workadventu.re/faq/why-cannot-i-speak-to-other-users-in-a-bubble-zone-or-in-a-conference-room/)

### Inferences
- "P2P 소규모 + 인원 증가 시 SFU 전환" 하이브리드는 서버비를 아끼지만 구현 복잡도(두 경로 + 전환 처리)가 큼. WorkAdventure도 결국 LiveKit 의존을 늘리는 방향.
- TURN 서버는 P2P를 쓰는 한 필수(기업망, 대칭 NAT). Cloudflare TURN 같은 관리형을 함께 쓰는 점이 참고할 만함.
- dg-village Lounge(영역 내 전원 청취)는 사실상 "회의실", Hallway 근접은 "버블" → WorkAdventure 구조가 거의 그대로 대응됨. 처음부터 SFU 하나(LiveKit)로 통일하면 전환 로직이 필요 없음.

### Gaps
- full-mesh 4명 한계의 근거가 된 실측(대역폭/CPU) 수치는 공개 자료에서 찾지 못함.
- WorkAdventure의 TURN 트래픽 비용 수치 없음.

## 기타 가상오피스: SkyOffice, Kumospace, Teamflow, SpatialChat, Topia, Wonder, Ohyay

### Takeaway
오픈소스 SkyOffice는 **Colyseus + PeerJS(P2P)**의 단순 구조. 상용 Kumospace·Teamflow는 직접 만들지 않고 **Daily(관리형 SFU) API**를 사용하고, 근접은 **트랙 구독(subscription) on/off**로 구현. Kumospace는 Twilio(방당 50명 제한) → Daily로 이전.

### Cited Findings
- SkyOffice(kevinshen56714/SkyOffice, MIT): Phaser + React/Redux + PeerJS + Colyseus(TypeScript). 다른 플레이어 근처로 가면 웹캠 영상 통화 시작, 범위를 벗어나면 연결 종료. 컴퓨터 오브젝트에서 화면공유, 데스크톱 브라우저 전용. ~1.2k stars(2026-04 기준 인덱스). — [Phaser forum: SkyOffice](https://phaser.discourse.group/t/phaser-3-skyoffice-2d-immersive-virtual-office/10589), [osrepos: SkyOffice](https://osrepos.com/repo/kevinshen56714-skyoffice.md), [oosmetrics](https://oosmetrics.com/repo/kevinshen56714/SkyOffice)
- Kumospace(Daily 블로그 2022-04-11): 공간 오디오/비디오는 "powered by Daily's track subscription API"; 방의 모든 아바타는 보이지만 가까운 사람과만 대화. 이전 Twilio는 방당 50명 제한 + 가격/지원 문제로 교체, 마이그레이션 수일. 화면공유 코드가 이전 경쟁사(Agora) 수백 줄 → Daily 4줄. — [Daily blog: How Kumospace built...](https://www.daily.co/blog/how-kumospace-built-a-platform-for-meaningful-connection/), [Daily case study: Kumospace](https://www.daily.co/customers/kumospace/)
- Daily 케이스 스터디: Kumospace는 Twilio 대비 방당 카메라 켠 인원 6배. (벤더 마케팅 수치) — [Daily case study: Kumospace](https://www.daily.co/customers/kumospace/)
- Teamflow: CEO가 Daily call object로 약 3시간 만에 프로토타입; 공간 오디오(가까운 사람만 들림). (벤더 마케팅) — [Daily case study: Teamflow](https://www.daily.co/customers/teamflow/)
- SpatialChat private area: 밖의 사용자는 안쪽 사용자의 카메라를 볼 수 없고, private area를 닫아 신규 입장 차단 가능. — [SpatialChat help: private areas](https://how.spatial.chat/help/product-features/how-to-use-private-areas/)
- Cosmos: 영역 밖에서는 한 칸 떨어져 있어도 들을 수 없음(Gather private area와 동일 패턴). — [Cosmos support: proximity based video chat](https://support.cosmos.video/article/34-proximity-based-video-chat)

### Inferences
- 상용 서비스 대다수는 미디어를 아웃소싱(Twilio/Daily/Agora)하고 "누구 트랙을 구독할지"만 자기 게임 로직으로 결정. dg-village도 서버의 근접/존 판정 → LiveKit 같은 SFU의 구독 권한/선택 구독으로 매핑하는 구조가 가장 단순.
- 벤더 교체 사례(Twilio→Daily, Agora 언급)가 있으므로 SFU 의존부를 얇게 추상화해 두는 것이 좋음. 참고로 Twilio는 Programmable Video 종료를 발표했다가 철회한 바 있으나 이번 조사에서 출처 확인 못함(Gaps).

### Gaps
- Topia, Wonder, Ohyay의 미디어 스택 1차 출처 없음(Ohyay는 서비스 종료된 것으로 알려졌으나 미확인).
- Kumospace/Teamflow의 현재(2025~2026) 공급자 유지 여부 미확인. 거리 기반 볼륨(Web Audio 패닝 등) 구현 세부 미공개.
- SkyOffice 소스 코드에서 근접 판정(거리값, 서버/클라 어느 쪽인지) 직접 확인하지 않음.

## 게임의 근접 음성: Lethal Company, Among Us 모드, Rust, VRChat

### Takeaway
Lethal Company는 Unity 에셋 **Dissonance VoIP + Netcode for GameObjects**(커뮤니티 확인). Dissonance는 근접을 "공간 해시 그리드로 같은 방(room) 배정" 또는 "콜라이더 트리거"로 처리. Among Us의 CrewLink/BetterCrewLink는 **WebRTC P2P + 자체 시그널링 서버**(게임 메모리에서 위치를 읽음). Unity용 상용 대안은 Photon Voice(Opus, 위치 오디오).

### Cited Findings
- Lethal Company: 게임 plugins 폴더에서 Dissonance VOIP 확인, 네트워크는 Unity Netcode for GameObjects (Steam 커뮤니티, 2024, 공식 발표 아님). — [Steam discussion](https://steamcommunity.com/app/1966720/discussions/0/4364627964252165209)
- Dissonance 근접 방식 2가지: (1) Grid Proximity — 공간 해싱으로 근처 플레이어를 같은 방에 넣음, Broadcast/Receipt Trigger의 범위 동일해야 함, "scales very well", 근처 인원 수와 무관하게 추가 대역폭 없음. (2) Collider Proximity — 플레이어별 콜라이더 볼륨이 겹치면 대화, 정밀하지만 설정 복잡. 두 방식 모두 position tracking 필요. — [Dissonance docs: Proximity Chat](https://dissonance.readthedocs.io/en/latest/Tutorials/Proximity-Chat.html)
- Dissonance는 Unity 게임에 직접 내장하는 실시간 VoIP, positional audio 지원. 기존 네트워크 라이브러리(NGO, FishNet 등) 위에 얹는 통합 패키지 방식. — [Dissonance docs](https://dissonance.readthedocs.io/), [Kitemetric: Dissonance + FMOD + FishNet](https://kitemetric.com/blogs/dynamic-voice-manipulation-in-unity-using-dissonance-fmod-and-fishnet)
- CrewLink(Ottomated): WebRTC(P2P)로 근접 음성 구현. BetterCrewLink(OhMyGuus 포크): 벽이 소리 차단, UPnP 꺼진 strict NAT에서도 동작, 별도 서버 레포(BetterCrewLink-server), 가능하면 사설 서버 배포 권장, 기본 포트 443(HTTPS)/9736. 게임 프로세스에 후킹해 데이터 읽음(백신 경고). — [Daily Dot: CrewLink](https://lede-v2.dailydot.com/among-us-crewlink-voice-chat), [npm: bettercrewlink](https://www.npmjs.org/package/bettercrewlink-rhdev), [Pterodactyl egg: BetterCrewlink Server](https://eggs.pterodactyl.io/egg/games-bettercrewlink-server)
- Photon Voice 2: Opus 코덱, Unity 오브젝트에 오디오 소스 부착(위치 오디오), WebGL 포함 다수 플랫폼, Photon Cloud 무료 20 CCU. Fusion은 오디오를 직접 전송하지 않고 별도 Photon Voice SDK 사용(Recorder/Speaker 컴포넌트). — [Photon Voice](https://www.photonengine.com:443/voice), [Unity Asset Store: Photon Voice 2](https://marketplace.unity.com/packages/tools/audio/photon-voice-2-130518), [Photon Fusion docs: voice chat](https://doc.photonengine.com/fusion/current/concepts-and-patterns/voice-chat)
- Meta 플랫폼 P2P VoIP는 2023-04 deprecated, 대안으로 Photon 제시. — [Meta developers: VoIP](https://developers.meta.com/vr/documentation/unreal/ps-voip/)

### Inferences
- 게임 쪽 근접 음성은 "거리 판정 → 방(그룹) 소속 갱신 → 그룹 단위 전송" 패턴(Dissonance Grid)이 흔함. dg-village 서버의 존/hearers 판정을 그대로 "음성 그룹"으로 내보내는 것과 같은 발상.
- Dissonance/Photon Voice는 Unity 네이티브 네트워킹(NGO/Photon) 전제라 hand-rolled Python WebSocket 서버인 dg-village와는 맞지 않을 수 있음(Dissonance는 커스텀 네트워크 통합도 가능하다고 알려졌으나 이번 조사에서 미확인). WebGL 호환도 별도 확인 필요.
- CrewLink 사례 = P2P는 서버비 0에 가깝지만 NAT 문제로 결국 TURN/시그널링 서버를 직접 운영해야 함.

### Gaps
- Rust(Facepunch) 근접 음성 기술: 출처를 찾지 못함(검색하지 못함).
- VRChat 음성 스택: Photon 기반 네트워킹으로 알려져 있으나 음성 구현 1차 출처 미확인.
- Dissonance의 토폴로지(서버 경유 vs P2P)와 WebGL 지원 여부를 문서에서 확인 못함.

## 교훈: P2P 한계, TURN 비용, 에코 문제

### Takeaway
실제 사례에서 P2P full-mesh는 **4명 내외**가 실용 한계(WorkAdventure)이고, 규모가 커지면 모두 SFU로 갔다(Gather 자체 SFU, WorkAdventure LiveKit, Kumospace/Teamflow Daily). P2P도 TURN 서버가 사실상 필수이며 UDP 차단 환경에서는 TCP 폴백으로 품질 저하.

### Cited Findings
- full-mesh는 인원 N이면 각자 N-1개 스트림 송신 → 확장 불가, 버블 4명 제한의 원인. — [WorkAdventure 1.27.0](https://workadventu.re/release/workadventure-1-27-0-the-road-to-workadventure-2/)
- P2P 실패 시 TURN relay 필요, UDP 막히면 TCP 폴백은 눈에 띄는 지연. — [WorkAdventure network docs](https://docs.workadventu.re/admin/preparing-your-network)
- 업계 일반 패턴: 1:1은 P2P, 인원 증가 시 SFU로 전환(하이브리드); P2P는 소규모 고정 그룹, SFU는 비용이 조금 더 들지만 다수에 적합. (벤더 블로그) — [Digital Samba](https://www.digitalsamba.com/blog/p2p-sfu-and-mcu-webrtc-architectures-explained), [GetStream](https://getstream.io/blog/what-is-a-selective-forwarding-unit-in-webrtc/)
- 화면에 보이지 않는 참가자의 비디오는 다운로드하지 않는 최적화(Stream SFU). — [GetStream architecture & benchmark](https://getstream.io/video/docs/javascript/v2/architecture-and-benchmark.md)
- Twilio 방당 50명 제한이 Kumospace 이전의 이유. — [Daily blog: Kumospace](https://www.daily.co/blog/how-kumospace-built-a-platform-for-meaningful-connection/)
- WebRTC 다중 스트림으로 인한 발열/쓰로틀링이 ZEP의 실제 과제. — [원티드: 젭 프론트엔드](https://www.wanted.co.kr/wd/276144)
- Gather도 클라이언트당 여러 SFU 연결 → 1개 연결(cascading)로 개선 중이었음 = 근접 대상이 여러 서버에 흩어지면 연결 수가 늘어나는 문제. — [Gather job posting](https://jobs.orbit.mit.edu/companies/gather-2-674309dd-fe8e-47c7-8df5-bcd9b2a88b57/jobs/31180045-staff-backend-engineer-video)

### Inferences
- dg-village(1인, 소규모 테스터, 데스크톱 + 추후 WebGL)라면: P2P/하이브리드보다 처음부터 관리형 SFU(LiveKit Cloud 무료 티어 또는 자체 LiveKit) + 서버 권한 기반 구독 제어가 구현/운영 모두 단순. 이는 CLAUDE.md의 "voice는 LiveKit/Vivox, 직접 구현 금지" 결정과 일치.
- 근접 = SFU 레벨 "구독 on/off", 거리 감쇠/패닝 = 클라이언트 볼륨 조절로 나누는 것이 사례들의 공통 분업.
- 게임 서버(Python)가 이미 hearers를 계산하므로, 그 결과를 SFU 권한(토큰/구독 API)에 반영하면 클라이언트 조작으로 focus 모드를 우회하는 것도 막을 수 있음.

### Gaps
- TURN 트래픽 비용 실측 수치(예: 전체 연결 중 TURN 경유 비율, GB당 비용)를 이번 조사에서 1차 출처로 확보하지 못함.
- 에코/하울링(스피커 사용 시 AEC) 관련 사례 글을 찾지 못함 — 브라우저 WebRTC는 AEC 내장이지만 Unity 네이티브 클라이언트에서는 SDK별 확인 필요.
- Gather/ZEP의 P2P 시절 실측 한계 수치 없음.
