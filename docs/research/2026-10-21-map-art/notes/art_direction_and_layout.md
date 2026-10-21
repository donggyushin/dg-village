# 소셜 공간 도트 맵의 아트 디렉션 + 레벨/레이아웃 설계 (비전공 1인 개발자용)

> 대상: dg-village (Unity, 2.5D: 3D 맵 + 64x64 도트 캐릭터, 고정 Orthographic 카메라 ~35° 틸트). 조사일 2026-10-21.
> 조사 범위 메모: 웹 검색 약 18회. 라그나로크/테일즈위버/투더문의 "팔레트·조명" 같은 구체 분석 자료는 신뢰할 만한 1차 출처를 찾지 못함(Gaps 참고). 일반 원칙(가독성, 팔레트, 믹셀, 코지 디자인, 소셜 공간 크기)은 출처가 비교적 탄탄함.

## 1. 레퍼런스 게임들의 시각적 특징 (라그나로크, 테일즈위버, 투더문, HD-2D 등)

### Takeaway
dg-village와 가장 직접적으로 같은 구조는 라그나로크 온라인(3D 월드 + 2D 방향별 스프라이트)과 스퀘어에닉스 HD-2D(옥토패스 트래블러: 3D 환경 속 도트 스프라이트 + 동적 조명·피사계심도·틸트시프트)다. HD-2D 개발진이 겪은 실패담("깊이감 부족", "해상도·채도를 너무 올리니 도트의 매력이 사라짐", "큰 화면에서 스프라이트가 외로워 보여 타일 변형·색 밀도를 늘림")이 dg-village에 그대로 적용되는 교훈이다.

### Cited Findings
- 라그나로크 온라인은 3D 월드에 정면/뒷면/측면/대각 방향의 2D 캐릭터 스프라이트를 배치한 구조 — [Wikipedia: Ragnarok Online](https://en.wikipedia.org/wiki/Ragnarok_Online)
- 원작자 이명진이 2D-on-3D 하이브리드를 고집했다는 주장은 팬 사이트(shapes.inc)에만 있음, 1차 출처 아님 — [shapes.inc fandom guide](https://shapes.inc/fandom/ragnarok-online/author) (신뢰도 낮음)
- 라그나로크는 캐릭터·몬스터가 귀여운 SD 스타일이고, 풀 3D 건물은 "실척 사이즈"로 나와 궁전·피라미드가 제 크기로 보인다는 국내 리뷰 서술(원작/후속작 구분 모호) — [게임메카](https://www.gamemeca.com/view.php?gid=45417)
- HD-2D 정의: 동적 조명, 피사계심도(depth of field), 틸트시프트로 "디오라마" 같은 외관을 만드는 스타일. 포인트 라이트를 추가해 캐릭터가 환경에 그림자를 드리우게 함 — [Wikipedia: HD-2D](https://en.wikipedia.org/wiki/HD-2D)
- 옥토패스 개발진: 2D 아트에 극적인 조명만 더하는 것으로는 부족했고, 일부 3D 요소를 넣자 깊이감이 생겼다(Morimoto). 빛이 물체에 닿고 그림자가 떨어지는 방식이 돋보이도록 빛과 도트를 섞는 데 집중(Asano) — [Nintendo Everything 인터뷰](https://nintendoeverything.com/octopath-traveler-devs-on-character-origins-visual-style-initial-hd-rumble-plans-much-more/)
- 초기 버전은 깊이감이 부족했고, 다른 시도에서는 해상도와 채도를 너무 올려 도트의 매력을 잃었다. 큰 화면에서 스프라이트가 외로워 보이지 않도록 타일 변형과 색의 밀도를 높였다 — [Siliconera (검색 요약 경유; 원문 403으로 직접 확인 못함)](https://siliconera.com/?p=683405)
- 옥토패스 II 인터뷰: "3D 환경 속 2D 도트"를 Unreal로 소규모 팀이 구현 — [Unreal Engine developer interview](https://www.unrealengine.com/en-us/developer-interviews/octopath-traveler-ii-builds-a-bigger-bolder-world-in-its-stunning-hd-2d-style)
- Eastward: 리드 아티스트 Hong Moran의 구룡성채 기반 몬스터 기숙사 스케치에서 시작(조명 기법은 개발사 공식 설명을 못 찾음; 3D 라이팅 사용설은 itch.io 유저 추측뿐) — [Game Developer: Road to the IGF](https://www.gamedeveloper.com/disciplines/road-to-the-igf-pixpil-s-i-eastward-i-), [itch.io 포럼 추측](https://itch.io/post/9039152)

### Inferences
- dg-village는 이미 "라그나로크 구조"(3D 맵 + 2D 방향 스프라이트 + 고정 각도 카메라)다. 따라서 맵 아트는 "도트 타일을 그린다"보다 "저폴리 3D 지오메트리 + 도트풍 텍스처(Point 필터) + 조명/그림자"로 가는 것이 자연스럽다. 현재 바닥이 "평평한 2D처럼 보이는" 원인은 HD-2D 개발진이 말한 "깊이감 부족"과 같은 문제 — 높이차(단차, 계단, 화단 테두리), 실제 3D 오브젝트, 그림자, 약한 DoF/비네트로 해결 가능.
- HD-2D 교훈 "채도·해상도 과다 = 도트 매력 상실": 맵 텍스처 텍셀 밀도를 캐릭터와 맞추고(아래 5번 믹셀), 맵 채도를 캐릭터보다 낮게.
- "스프라이트가 외로워 보임" → 빈 바닥 대신 작은 소품(풀 포기, 돌, 꽃)으로 밀도를 높일 것.
- (일반 지식, 출처 미확인) 라그나로크 마을(프론테라=석조 성곽+분수 광장, 페이욘=숲속 목조, 알베르타=항구)은 마을마다 하나의 지배적 재질·색 테마가 있다. 이 "마을 = 한 가지 테마" 원칙은 채널/맵별 테마 설계에 차용할 만하다.

### Gaps
- 라그나로크/테일즈위버/투더문/메이플 마을의 팔레트·광원 방향·디테일 밀도에 대한 신뢰할 만한 아트 분석(인터뷰, 아트북 해설)을 찾지 못함. 한국어 검색에서도 프론테라 그래픽을 직접 다룬 기사 없음.
- Stardew Valley, Sea of Stars, Habbo, ZEP/Gather 자체 맵 아트의 스타일 분석 자료도 이번 조사에서는 확보 못함.

## 2. 아름답고 코지한 환경 아트 원칙 (가독성, 팔레트, 컬러 온도, 레이어, 앰비언트)

### Takeaway
가독성은 색상(hue)이 아니라 명도(value) 대비에서 나온다. 배경은 캐릭터보다 낮은 대비·낮은 채도로 "조용하게", 가장 강한 색과 대비는 캐릭터·상호작용 대상에 아껴 둔다. 팔레트는 hue-shift된 램프(어두울수록 차갑게/채도 높게, 밝을수록 따뜻하게/채도 낮게)로 구성하고, 코지함은 "따뜻하고 저대비인 색, 자연 재질, 둘러싸인 공간, 바깥이 보이는 창, 부드러운 전환"에서 나온다.

### Cited Findings
- 가독성은 거의 전적으로 명도 대비에서 나오며, 색상이 달라도 명도가 같으면 작은 크기에서 뭉개진다. 그레이스케일로 바꿔서 형태가 읽히는지 확인하는 테스트 권장 — [검색 요약: bugnet.io / wayline.io 계열 가이드](https://www.wayline.io/learn/pixel-art/2) (bugnet 원문 fetch 실패, 요약만 확인)
- 가장 흔한 문제는 너무 바쁘거나 화려한 배경. 배경은 더 조용하고 저대비로, 가장 강한 색과 대비는 플레이어가 상호작용하는 대상에 남겨 둘 것 — [wayline.io: Color and limited palettes](https://www.wayline.io/learn/pixel-art/2); 포럼 피드백 "배경이 전경 요소에 비해 너무 디테일해서 전경이 묻힌다, 배경 대비를 낮추면 해결" — [TouchArcade 포럼](https://toucharcade.com/community/threads/what-do-you-think-about-this-stage.109047/)
- 그림자는 차가운 색(파랑/보라)으로, 하이라이트는 따뜻한 색(노랑/주황)으로 밀 것; 장면 전체 팔레트는 8~32색 정도가 흔함 — [wayline.io](https://www.wayline.io/learn/color-palettes/1)
- Slynyrd(Raymond Schlitter): 램프 내 hue를 밝아질수록 따뜻하게 이동(positive shift), 스와치당 약 20°가 상한. hue shift 없는 "straight ramp"는 밋밋하고 조화시키기 어려움. 밝기가 올라갈수록 채도를 낮출 것, 채도는 램프 중간에서 최고, 0이나 100은 쓰지 않음. 최대 실수는 "고채도 + 고명도" 조합 — [Slynyrd Pixelblog 1: Color Palettes](https://slynyrd.com/blog/2018/1/10/pixelblog-1-color-palettes)
- Slynyrd의 Sly16: 하나의 게임 월드 전체를 그리기 위한 16색 제한 팔레트 — [Slynyrd Pixelblog 28](https://slynyrd.com/blog/2020/5/21/pixelblog-28-side-view-tiles)
- Lospec 팔레트 리스트 = 하드웨어 팔레트 + 아티스트 제작 팔레트 DB. 코지 계열: HoneyGB(따뜻한 색, 흰노랑~짙은 보라), Homeyhoney(12색, "오두막 같은" 따뜻함). 범용 64색: Resurrect 64(더 어두운 검정 #18141a 추가 팁), Endesga 64, Jehkoba64. Endesga 16은 Aseprite 기본 팔레트에 포함 — [Lospec tag: cheerful/earthy 등](https://lospec.com/palette-list/tag/earthy), [Lospec Endesga 64](https://lospec.com/palette-list/endesga-64), [itch.io 개발자 코멘트](https://itch.io/post/12552316)
- 코지함의 3요소: 안전(Safety), 풍족(Abundance), 부드러움(Softness). 시각적 단서: 음식·음료·따뜻함(부엌, 선술집, 카페, 침실), 부드러운 전환/그라데이션, 인간 스케일의 방과 물건, 따뜻하고 저대비인 색, 자연 재질, 둘러싸인 공간, 바깥을 보는 창. 코지함을 깨는 것: 위협, 책임, 방해 요소, 강한 자극, **광막한 거리(vast distances)**, 갇힘, 동의 없는 사회적 상황, 호화로움. 단, 이 요소가 "코지 공간 밖에" 안전하게 있으면 오히려 강화(창밖의 차가운 비 → 독서 공간이 더 따뜻하게) — [Game Developer: Designing for Coziness (Project Horseshoe)](https://www.gamedeveloper.com/design/designing-for-coziness)
- 코지 온라인 게임(Palia) GDC 2024 세션: 사회적 코지함을 게임 기반에 녹이는 것 — [GDC 2024 Palia session](https://schedule.gdconf.com/session/it-takes-a-village-cultivating-social-coziness-in-palia/899488)
- Gather 맵의 레이어 구조: 배경(아래 빵) / 캐릭터·오브젝트(속) / 전경(위 빵). 문틀 등을 전경 레이어로 두지 않으면 벽 위를 걷는 것처럼 보임 — [Gather Custom Map Overview](https://support.gather.town/hc/en-us/articles/15910430385172), [Ironistic guide](https://ironistic.com/insights/gather-town-maps-and-backgrounds)

### Inferences
- dg-village 캐릭터는 "밝은 고채도 + 진한 외곽선"이므로, 맵은 반대로 **외곽선 없음 또는 옅은 외곽선 + 중채도 + 중간 명도대**로 가면 캐릭터가 자연스럽게 튄다(라그나로크 스크린샷의 일반적 인상과도 일치하지만 출처 없음).
- 컬러 온도 설계: 광원(해) = 따뜻한 노랑·주황, 그림자 = 차가운 보라·파랑. Unity URP에서는 Directional Light 색을 약간 따뜻하게, Ambient(Environment Lighting)를 약간 푸르게 두면 hue-shift를 자동으로 흉내 낼 수 있음.
- 앰비언트 애니메이션(분수 물, 흔들리는 풀, 나비, 굴뚝 연기, 깃발)은 "살아 있는 공간" 느낌의 최저비용 수단 — 출처 기반 아님, 일반 관행.
- 코지 원칙 "광막한 거리"는 소셜 맵에 특히 중요: 맵을 너무 크게 만들면 사람이 흩어져 썰렁해 보임(3번 참고).

### Gaps
- "앰비언트 애니메이션이 체감 품질을 얼마나 올리는가"에 대한 정량 자료 없음.
- Saint11(Pedro Medeiros) 튜토리얼은 이번 조사에서 직접 인용할 페이지를 확보하지 못함.

## 3. 소셜 공간 레이아웃 (모임 장소, 동선, 2~30명 기준 맵 크기)

### Takeaway
Gather 공식 가이드는 "예상 동시 접속자 1인당 20~40타일(32px 타일)"을 권장한다. 30명 기준 600~1,200타일 ≈ 25×25~35×35타일. 스폰 지점에 여유를 두고, 통과 불가 오브젝트 사이에 길을 남기고, 허브(광장)에서 각 공간으로 이어지는 "중앙 + 포털" 구조가 정석이다. MMO 연구는 이런 공간이 Oldenburg의 "제3의 장소(third place)"로 기능한다고 본다.

### Cited Findings
- Gather: 32×32px 타일 그리드, 맵 최대 3200×3200px(100×100타일). **예상 게스트 1인당 약 20~40타일** 계획(200명 → 4,000~8,000타일 → 맵 여러 개). 동시 입장 인원을 고려해 기본 스폰 주변에 충분한 공간을 남기고 스폰 타일끼리 붙이지 말 것. 캐릭터는 30~36px 높이로 위 타일과 겹침 — [Gather Help: Sizes](https://support.gather.town/articles/9253868124-sizes)
- HyHyve(다른 플랫폼): 최대 50명 2200×1500px, 100명 3000×2000px, 200명 4400×3000px; "최대 인원 기준으로 크기 결정" — [검색 요약 경유, 원문 Notion은 fetch 불가](https://hyhyve.notion.site/HyHyve-Map-Design-Guidelines-278afee6844244aaab0d48fa76f8fccf)
- 통과 불가 오브젝트 사이에 아바타가 지나갈 틈을 남길 것 — [Icograms: Gather Town Map](https://icograms.com/usage-gather-town)
- ZEP: 타일 32×32px. 통과 불가 타일은 경계에만 배치하라는 성능 권장. 배경/앞화면 이미지 최대 4096px, 모바일 고려 2048px 이하 권장. 맵 최대 크기는 문서마다 다름(한국어 312×312 vs 영문 512×512타일) — [ZEP 공식 가이드: 맵 성능 최적화](https://docs.channel.io/zep-guide/ko/articles/%EB%A7%B5-%EC%84%B1%EB%8A%A5-%EC%B5%9C%EC%A0%81%ED%99%94-%EA%B0%80%EC%9D%B4%EB%93%9C-7229acdb), [ZEP Use Tools](https://docs.channel.io/zep-guide/en/articles/Use-Tools-6474aa88)
- Club Penguin: 섬 지도에서 방(주로 야외)으로 이동하는 허브 구조, 2011년 지도 개편 때 나무·호수·산 등 지형을 추가. Town Plaza는 서비스 종료일에 아바타가 몰린 모임 장소로 기록됨 — [Club Penguin Wiki: Map](https://clubpenguin.fandom.com/wiki/Map), [U of Toronto archive](https://omeka.ischool.utoronto.ca/items/show/697)
- 허브 영역 = 각 장소로의 포털이 있는 중심점, 위험 없이 자랑·거래·대화하는 저스트레스 공간 (일반 원칙, 출처 품질 보통) — [game-dev-skills: map-design](https://playbooks.com/skills/taozhuo/game-dev-skills/map-design)
- Steinkuehler & Williams(2006): MMO 공간은 Oldenburg의 "제3의 장소"(집·직장 외 비공식 사교 장소)로 기능, 특히 "bridging" 사회적 자본(얕지만 넓은 관계)에 적합 — [JCMC 논문](https://academic.oup.com/jcmc/article/11/4/885/4617703), [Illinois News](https://news.illinois.edu/some-online-video-games-found-to-promote-sociability-researchers-say/)
- Ducheneaut, Moore, Nickell의 Star Wars Galaxies "third places" 사교성 설계 연구가 존재(내용은 이번에 확인 못함) — [Christou 2011 참고문헌](https://ktisis.cut.ac.cy/bitstream/20.500.14279/4015/6/Christou2011-Designing_for_the_user_experience_of_sociability_in_massively_multiplayer_online_games.pdf)

### Inferences (dg-village 수치로 환산)
- dg-village 캐릭터 64px / 32 PPU → 캐릭터 1명 ≈ 2유닛 높이(발 아래 여백 포함). Gather 캐릭터(30~36px)가 32px 타일 1칸 기준이므로, "Gather 1타일 ≈ dg-village 1유닛 정도"로 근사하면(캐릭터 시각 크기는 dg-village가 약 2배라 다소 보수적):
  - 2~10명 방(카페, 방): 10×10~15×15유닛
  - 광장(최대 30명): 1인당 20~40 → 600~1,200유닛² ≈ **25×25~35×35유닛**. 현재 Ground 20×20(400유닛²)은 10~20명 규모.
- 현재 카메라 Ortho Size 5 → 화면 세로 10유닛(16:9이면 가로 약 17.8유닛, 35° 틸트로 바닥 방향 깊이는 더 길게 보임). 즉 광장 전체를 한 화면에 볼 수 없고 1/4~1/6 정도만 보임. 광장은 "화면 2~3개 분량"이 적당하고 그보다 크면 코지 원칙의 "광막한 거리"에 걸림.
- 레이아웃 정석(추론): 중앙 랜드마크(분수/큰 나무/종탑) → 그 주변 원형 모임 공간 → 벤치·테이블 클러스터 3~4개(2~6명용 소규모 대화 섬) → 가장자리에 건물 입구(카페·방 채널 포털 역할) → 외곽은 낮은 담·나무로 시선과 이동을 닫음(무한 바닥 대신 "둘러싸인 공간"). 스폰은 랜드마크 근처 넓은 빈 바닥.
- 현재 "Ground에 경계/충돌 없음"은 레이아웃 면에서도 문제: 맵 가장자리를 시각적 오브젝트(담, 수풀, 물)로 막는 것이 코지함(둘러싸임)과 기술적 경계를 동시에 해결.

### Gaps
- Animal Crossing 광장, Habbo 방 설계에 대한 신뢰할 만한 레이아웃 분석 자료를 찾지 못함.
- Gather 공식 "Best Practices for Space Design" 문서 자체는 찾지 못함(Sizes 문서로 대체).
- 2.5D 카메라 기준 "1인당 면적" 직접 자료 없음 — 위 환산은 추론.

## 4. 비전공자용 단계별 워크플로우 + 첫 "히어로 맵"(광장) 주말 범위

### Takeaway
"작은 팔레트를 먼저 고정 → 그레이스케일 블록아웃으로 명도·동선 확인 → 핵심 에셋 리스트 → AI 생성(PixelLab 타일/오브젝트, Meshy·Tripo 3D) → 같은 팔레트로 Aseprite 보정 → 통합 → 폴리시(조명/그림자/앰비언트)" 순서. 히어로 맵 첫 주말은 "중앙 랜드마크 1 + 바닥 재질 2~3 + 소품 5~8종 + 경계 처리 + 조명"으로 범위를 자른다.

### Cited Findings
- 그레이스케일(균등 간격 명도 램프)로 먼저 그려 읽히는지 확인한 뒤, Aseprite에서 명도를 유지하며 색을 덧칠하는 워크플로우 — [검색 요약: lemmasoft 포럼/itch.io](https://lemmasoft.renai.us/forums/viewtopic.php?p=551533)
- 작은 램프부터 만들어 보라; 큰 마스터 팔레트에서 작업별 작은 팔레트를 뽑아 쓰는 방식 — [Slynyrd Pixelblog 1](https://slynyrd.com/blog/2018/1/10/pixelblog-1-color-palettes)
- PixelLab "Create tiles (Pro)": 텍스트 설명으로 맵용 타일 변형 생성, top-down square/hex/isometric/octagon 지원, 지형 전환(transition)을 한 배치로 생성, 20~25 크레딧(스타일 레퍼런스 포함 시 약 20~40) — [PixelLab docs: Create tiles pro](https://www.pixellab.ai/docs/tools/create-tiles-pro)
- 탑다운 타일셋 프롬프트: 아래 지형/위 지형/전환을 따로 서술(2026-07 업데이트된 서드파티 위키) — [Sorceress wiki: PixelLab and sprites](https://sorceress.games/wiki/wizard-genie/pixellab-and-sprites)
- PixelLab isometric tile 생성 도구도 존재 — [PixelLab docs: create isometric tile](https://www.pixellab.ai/docs/tools/create-isometric-tile)
- 무료 CC0 참고 에셋: Screaming Brain Studios "400+ Isometric Town Tiles" — [OpenGameArt](https://opengameart.org/content/400-isometric-town-tiles)

### Inferences (추천 플랜)
1. **무드보드 (1~2시간)**: 라그나로크 프론테라/알베르타, 옥토패스(HD-2D), 투더문, 스타듀 마을 스크린샷 10~20장. "좋은 점 1줄"씩 메모. 같은 시간대(낮/노을)로 통일.
2. **팔레트 고정 (1시간)**: girl01/boy01 스프라이트에서 실제 쓰인 색을 Aseprite로 추출 → 거기에 맞는 Lospec 팔레트 하나 선택(코지 32~64색: Resurrect 64, Endesga 32/64 중 따뜻한 쪽만 사용하거나, 더 좁게 Homeyhoney류). 맵 전용으로 채도를 한 단계 낮춘 서브셋을 정의. 이후 모든 AI 결과물을 이 팔레트로 인덱스 변환(Aseprite: Sprite → Color Mode → Indexed, 또는 팔레트 적용).
3. **그레이박스 (반나절)**: Unity에서 큐브/플레인으로 광장 25×25~30×30유닛 블록아웃 — 랜드마크 위치, 대화 섬 3~4개, 길, 건물 입구, 외곽 경계. 캐릭터 여러 명을 세워 놓고 카메라로 보며 동선·밀도 확인. 그레이스케일 상태에서 캐릭터가 잘 읽히는지 확인.
4. **핵심 에셋 리스트 (히어로 맵 MVP)**: 바닥 2~3종(돌길, 잔디, 흙 + 전환), 랜드마크 1(분수), 벤치·가로등·화분·나무 2종·덤불·담장, 건물 외관 2~3(입구만 있어도 됨), 앰비언트 1~2(분수 물, 흔들리는 풀).
5. **생성/구매**: 바닥 = PixelLab 타일(또는 Point 필터 텍스처), 큰 구조물 = Meshy/Tripo 저폴리 3D + 도트풍 텍스처, 작은 소품 = PixelLab 오브젝트 스프라이트(캐릭터처럼 빌보드). 반드시 같은 텍셀 밀도(32 PPU) 유지.
6. **통합**: Alpha Clip 머티리얼, Point 필터, 그림자 켜기, 바닥에 높이차(단차, 연석) 추가.
7. **폴리시 패스 (순서대로 효과 큰 것부터)**: ① 조명(따뜻한 Directional + 차가운 Ambient) ② 캐릭터/소품 그림자(블롭 섀도라도) ③ 바닥 변형 타일·데칼로 반복 깨기 ④ 소품 밀도(빈 공간 메우기) ⑤ 앰비언트 애니메이션 ⑥ 가벼운 포스트(비네트, 약한 컬러 그레이딩; DoF/틸트시프트는 선택).
- **주말 1회 범위**: 토요일 = 1~3단계, 일요일 = 바닥 + 분수 + 벤치/나무 3~4종 통합 + 조명. 건물·앰비언트·포스트는 다음 주말.

### Gaps
- Meshy/Tripo 결과물을 "도트풍"으로 맞추는 검증된 파이프라인(텍스처 다운샘플·팔레트 양자화 등)에 대한 자료는 이번 조사에서 찾지 못함 — 별도 조사 필요.
- PixelLab 타일이 3D 바닥 텍스처로서 이음매(seamless) 품질이 어떤지 실사용 후기 없음.

## 5. 인디 도트 맵이 아마추어처럼 보이는 흔한 실수와 해결

### Takeaway
1순위는 **믹셀(mixels: 서로 다른 픽셀 크기 혼재)** — 에셋 축소/확대, 비정수 스케일, 서로 다른 에셋팩 혼용에서 생긴다. 그 외: 배경이 캐릭터보다 시끄러움, 고채도+고명도 남발, hue-shift 없는 밋밋한 램프, 균일한 바닥과 격자가 보이는 타일 반복, 빈 공간, 광원 방향 불일치.

### Cited Findings
- 믹셀 = 픽셀이 같은 그리드를 따르지 않는 현상, 두 요소가 서로 다른 픽셀 해상도일 때 발생 — [itch.io devlog: chasing a pixel perfect tail](https://squeakypig.itch.io/unicopia/devlog/787703/a-tale-about-chasing-a-pixel-perfect-tail), [ModDB Guard Duty retrospective](https://www.moddb.com/features/guard-duty-a-development-retrospective-dev-diary-4)
- 흔한 원인: 공들인 나무를 배경용으로 50% 축소 → 배경 픽셀이 전경의 절반 크기. 해결: 작은 나무가 필요하면 작은 나무를 새로 그린다. 기준 해상도를 정하고 모든 에셋을 1:1로 — [wayline: fixing asset pack art clashes](https://www.wayline.io/blog/developer-checklist-fixing-asset-pack-art-clashes)
- 비정수 스케일은 픽셀 비율을 망가뜨리는 "재앙"; 일부 스프라이트에만 정수 배율을 적용해도 외곽선 두께가 안 맞음. Michael Azzi "Pixel Logic": 모든 도트는 같은 그리드, 외곽선 두께 동일 — [itch.io devlog](https://squeakypig.itch.io/unicopia/devlog/787703/a-tale-about-chasing-a-pixel-perfect-tail), [wayline: avoid pitfalls mixing art styles](https://www.wayline.io/blog/avoid-pitfalls-mixing-game-art-styles)
- 서로 다른 아티스트의 에셋팩 혼용, 선명한 고해상 텍스처와 흐린 저해상 스프라이트 혼용 → 즉각적인 시각적 부조화 — [wayline](https://www.wayline.io/blog/avoid-pitfalls-mixing-game-art-styles)
- 서브픽셀 이동은 캐릭터가 배경 그리드와 어긋나게 함; 혼합 해상도를 잘 쓰는 현대 게임도 있으나 절제해서 사용 — [ModDB Guard Duty](https://www.moddb.com/features/guard-duty-a-development-retrospective-dev-diary-4)
- 바쁜/화려한 배경이 가장 흔한 문제 — [wayline](https://www.wayline.io/learn/pixel-art/2)
- 고채도+고명도 조합이 최대 실수, hue shift 없는 straight ramp는 밋밋 — [Slynyrd](https://slynyrd.com/blog/2018/1/10/pixelblog-1-color-palettes)
- HD-2D: 해상도·채도 과다로 도트 매력 상실, 타일 변형 밀도 부족 시 스프라이트가 외로워 보임 — [Siliconera via 검색 요약](https://siliconera.com/?p=683405)

### Inferences (실수 → dg-village 해결책)
| 실수 | 증상 | 해결 |
|---|---|---|
| 믹셀 | 캐릭터 픽셀과 맵 텍스처 픽셀 크기가 다름 | 모든 텍스처 32 PPU 기준 텍셀 밀도 통일. 3D 모델 UV도 "1유닛 = 32텍셀"로. 오브젝트를 Transform scale로 키우지 말 것(정수배만 허용) |
| 카메라/화면 비정수 배율 | 픽셀이 들쭉날쭉 | URP **Pixel Perfect Camera**는 3D 틸트 카메라와 궁합이 제한적이므로, 대안으로 저해상 RenderTexture에 그린 뒤 정수배 업스케일하는 방식 검토(추가 조사 필요) |
| 배경이 시끄러움 | 캐릭터가 묻힘 | 맵 채도·대비 한 단계 낮춤, 외곽선 없음/옅게, 그레이스케일 테스트 |
| 평평한 균일 바닥 | "2D처럼 보임"(현재 상태) | 재질 2~3종 + 전환, 데칼(얼룩, 풀, 낙엽), 단차·연석, 그림자 |
| 격자가 보이는 타일 반복 | 같은 무늬가 바둑판처럼 반복 | 변형 타일 3~4종 랜덤 배치, 큰 데칼로 반복 깨기, 소품을 격자에서 살짝 어긋나게 |
| 빈 공간 | 썰렁, 캐릭터가 외로움 | 맵 크기를 인원에 맞춰 줄이고, 가장자리·모서리에 소품 클러스터 |
| 광원 방향 불일치 | AI 에셋마다 하이라이트 방향이 다름 | 광원 규칙 1개(예: 좌상단)를 정하고 생성 프롬프트에 명시, Aseprite에서 반대 방향 하이라이트 수정. 3D 오브젝트는 Unity 라이트로 통일 |
| 에셋 스타일 혼용 | 팩/생성기마다 화풍이 다름 | 모든 결과물을 같은 팔레트로 인덱스 변환, 외곽선 규칙 통일 |
| 고채도 남발 | 눈 아프고 싸 보임 | 밝은 색일수록 채도 낮춤, 순수 원색 금지 |

### Gaps
- Unity URP에서 "3D 틸트 오쏘 카메라 + 도트 픽셀 퍼펙트" 구현 모범 사례는 이번 조사 범위에서 확인하지 못함(별도 기술 조사 권장).
- r/PixelArt 크리틱 스레드 원문은 확보하지 못함.
