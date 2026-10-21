# 라그나로크처럼, 도트 바닥 위에 소품을 세워라

dg-village 맵을 예쁘게 만드는 가장 현실적인 길은 **라그나로크 온라인이 실제로 썼던 구조를 그대로 따르는 것**이다. 바닥은 3D 저폴리 지오메트리로 만들되 32px 도트 타일 텍스처(Point 필터)를 깔고 단차와 그림자를 준다. 나무, 벤치, 가로등, 분수 같은 소품은 캐릭터와 똑같은 방식(32 PPU, Alpha Clip, X 35° 틸트)의 직립 도트 스프라이트로 세운다. 이 하이브리드(이하 "RO 하이브리드")는 지금의 캐릭터 파이프라인을 그대로 재사용하고, AI 도구가 가장 잘 만드는 것(이음매 없는 타일, 투명 배경 단일 소품)만 요구하며, **"1유닛 = 32텍셀"이라는 단일 픽셀 밀도**를 모든 에셋에 강제할 수 있다. 바닥이 지금 "평평한 2D"처럼 보이는 원인은 옥토패스 트래블러 개발진이 겪은 "깊이감 부족"과 같다. 해법도 같다. 높이차, 실제 3D 요소, 빛과 그림자, 그리고 빈 바닥을 채우는 소품 밀도다. 생성 도구는 캐릭터를 만든 PixelLab이 1순위다. 타일셋과 맵 오브젝트 모두 캐릭터와 같은 "low top-down" 시점 옵션을 지원한다. 기술적으로 먼저 고칠 것은 카메라 배율이다. 현재 Ortho Size 5에서는 텍셀 하나가 화면 픽셀 2.25개로 그려져 픽셀 폭이 들쭉날쭉해진다. Size 5.625로 바꾸면 내부 해상도가 640×360이 되어 720p/1080p/1440p에서 정수배가 된다. 첫 광장은 25×25~30×30유닛, "랜드마크 1개 + 바닥 재질 2~3종 + 소품 5~8종 + 외곽 경계 + 조명"으로 범위를 자르면 두 주말 안에 끝낼 수 있다.

## 네 가지 방식 중 RO 하이브리드가 1인 개발자에게 이긴다

라그나로크는 정확히 dg-village와 같은 문제를 이미 풀었다. 맵 데이터는 바닥 지오메트리·텍스처·라이트맵(GND), 높이와 통행 가능 여부(GAT), 3D 소품·물·조명 배치(RSW)로 나뉜다. 캐릭터와 몬스터는 8방향 2D 스프라이트이고, 3D 모델은 대부분 나무나 건물 같은 맵 소품이다 ([RagnarokFileFormats](https://github.com/rdw-archive/RagnarokFileFormats/blob/master/README.MD)). 예쁘게 보인 비결은 값싼 재료 몇 가지의 조합이다. 바닥은 **"32픽셀 텍스처 하나가 타일 하나"**인 작은 반복 텍스처다. 여기에 면마다 **8×8px짜리 저해상 라이트맵을 16단계로 포스터라이즈**해 얹고, 타일별 버텍스 컬러로 반복 무늬를 깨뜨린다. 절벽 벽면은 별도 모델이 아니라 이웃 타일의 높이 차이에서 자동으로 생긴다 ([GND spec](https://github.com/Duckwhale/RagnarokFileFormats/blob/master/GND.MD)). 그 흐릿하고 얼룩진 그림자가 "손으로 그린 듯한" 느낌을 만든다. 32px/타일이라는 밀도가 dg-village의 32 PPU와 정확히 같다는 점은 우연이 아니라 강한 근거다. 반대로 테일즈위버는 "불완전한 3D 대신 귀여운 SD 캐릭터를 표현하기 쉬운 2D에 특화"하는 길을 의도적으로 택했다 ([경향신문 2003](https://www.khan.co.kr/article/200304071552051)). 다만 그 맵 제작 파이프라인에 대한 1차 자료는 찾지 못했다.

HD-2D 개발진의 회고는 그대로 dg-village의 체크리스트가 된다. 옥토패스 초기 버전은 2D 아트에 조명만 더해서는 깊이감이 부족했고, 3D 요소를 넣고 나서야 깊이가 생겼다. 빛이 물체에 닿고 그림자가 떨어지는 방식을 보여 주는 데 집중했다고 한다 ([Nintendo Everything](https://nintendoeverything.com/octopath-traveler-devs-on-character-origins-visual-style-initial-hd-rumble-plans-much-more/)). 또 **해상도와 채도를 너무 올리면 도트의 매력이 사라졌고**, 큰 화면에서 스프라이트가 "외로워 보여" 타일 변형과 색 밀도를 늘렸다 ([Siliconera](https://www.siliconera.com/project-octopath-traveler-developers-answer-project-started-troubles-developing-hd-2d/)). 트라이앵글 스트래티지 프로듀서는 비율이 사실적일수록 픽셀이 작아져 일러스트 쪽으로 밀려나므로, 데포르메 캐릭터가 도트로서 매력을 유지해야 한다고 말한다 ([Nintendo Life](https://www.nintendolife.com/news/2022/05/triangle-strategy-producers-talk-hd-2d-and-why-other-devs-havent-used-it)). dg-village의 치비 캐릭터는 이미 그 조건을 만족한다.

| 방식 | 결과물 인상 | 1인 제작 난이도 | 캐릭터와 픽셀 일관성 | 판정 |
|---|---|---|---|---|
| (a) 3D 저폴리 + 도트 텍스처 (RO, HD-2D) | 깊이·그림자가 살아 있는 디오라마 | 중간 (ProBuilder 상자·평면 + 32px 타일) | 좋음, 텍스처를 32텍셀/유닛으로 맞추면 | 바닥·건물 몸체에 채택 |
| (b) 통짜 2D 페인팅/타일 배경 | 가장 "한국 2D 온라인게임"다움 | 가장 높음 (맵마다 한 장, AI 일관성 최악) | 좋음 | 기각 |
| (c) 저해상 RT 픽셀 퍼펙트 3D (A Short Hike, t3ssel8r) | 통일감 최고, 외곽선·밴드 조명 | 가장 높음 (render graph 커스텀 패스) | 자동 통일, 대신 캐릭터도 리샘플됨 | 나중의 실험 과제 |
| (d) 전부 직립 스프라이트 (Don't Starve류) | AI 친화적, 건물이 평면적 | 낮음 (전부 PNG) | 최고 | 소품에 채택 |

(b)는 고정 카메라라면 이론상 가능하다. 하지만 35° 시점에 맞춰 맵 전체를 한 장으로 그려야 하고, 캐릭터를 가리는 지붕이나 나무는 별도 오버레이로 떼어 내야 한다. RPG Maker 진영의 "parallax mapping"이 바로 이 방식인데, 맵을 외부 프로그램에서 통째로 그리는 노동집약적 기법이다 ([RPG Maker parallax guide](https://www.rpgmakerweb.com/a/rpg-maker-vxace-recommend/parallax-mapping-guide)). AI로 큰 한 장을 그리면 스타일 드리프트와 원근 오류가 가장 잘 드러난다. Unity Tilemap 기반 2D 타일은 탑다운 2D 카메라용이라, 쓰려면 씬 전체를 2D로 갈아엎어야 한다.

(c)는 결과가 가장 "아름답게 통일"되는 방식이다. A Short Hike 개발자는 "가능한 한 적은 픽셀로 아름답고 읽기 쉬운 3D 세계"를 목표로 했고, 저해상도가 "상상력이 디테일을 채우게" 한다고 말했다 ([PlayStation Blog](https://blog.playstation.com/2021/08/05/crafting-a-tiny-open-world-a-look-behind-the-scenes-at-the-creation-of-a-short-hike/)). 하지만 참고 사례들은 캐릭터도 3D 모델이고, t3ssel8r도 캐릭터를 3D로 렌더한다 ([Cascadeur 인터뷰](https://cascadeur.com/blog/general/making-an-animation-for-a-3d-pixel-art-game)). 기존 64×64 스프라이트를 저해상 버퍼에 함께 넣으면, 버퍼 밀도가 정확히 32px/유닛이고 카메라가 스냅되지 않는 한 픽셀이 먹히거나 두 배가 된다. 그래서 "RO 하이브리드를 먼저 완성하고, (c)는 그 위에 얹는 후처리 실험"으로 두는 것이 순서다. 이 판단은 출처가 아니라 분석에 기반한다.

## 32텍셀/유닛 하나로 모든 픽셀을 묶어야 아마추어 티가 안 난다

도트 맵이 싸 보이는 1순위 원인은 **믹셀(mixels)**, 즉 한 화면에 서로 다른 크기의 픽셀이 섞이는 현상이다. 공들여 그린 나무를 배경용으로 50% 축소하면 그 나무의 픽셀은 전경의 절반 크기가 된다. 처방은 "작은 나무가 필요하면 작은 나무를 새로 그려라"다 ([wayline](https://www.wayline.io/blog/developer-checklist-fixing-asset-pack-art-clashes)). 비정수 배율은 픽셀 비율을 망가뜨리고 외곽선 두께까지 어긋나게 한다. Michael Azzi의 *Pixel Logic* 원칙은 모든 도트가 같은 그리드 위에 있어야 한다는 것이다 ([itch.io devlog](https://squeakypig.itch.io/unicopia/devlog/787703/a-tale-about-chasing-a-pixel-perfect-tail)). dg-village에 적용하면 규칙은 세 줄이다. 모든 스프라이트는 32 PPU로 둔다(Unity의 PPU는 월드 1유닛당 스프라이트 픽셀 수다 ([Unity docs](https://docs.unity.cn/ru/ScriptReference/TextureImporter-spritePixelsPerUnit.html))). 모든 3D 메시 UV는 1유닛 = 32텍셀로 맞춘다. Transform scale로 소품을 키우지 않는다(정수배만 허용). 3D에서도 텍셀 밀도가 에셋마다 다르면 몰입이 깨진다는 것은 일반 원칙이다 ([RenderHub](https://www.renderhub.com/3d-tools/texel-density-calculator)).

카메라 배율은 지금 당장 고칠 수 있는 기술 부채다. 아래 계산은 노트의 분석이고 출처는 없다. Ortho Size 5는 화면 세로 10유닛이므로 720p에서는 유닛당 72화면픽셀이고, 32 PPU 텍셀 하나가 **2.25화면픽셀**로 그려진다. 비정수라서 카메라가 움직일 때 Point 필터 텍스처가 반짝이고(shimmer) 픽셀 폭이 고르지 않다. 깔끔한 답은 **Ortho Size를 5.625로 바꾸는 것**이다. 그러면 세로 11.25유닛 × 32 = 360텍셀이 되어 내부 해상도 640×360이 나오고, 이것이 720p(×2), 1080p(×3), 1440p(×4)에 모두 정수로 떨어진다. MIT 라이선스 Unity 6 URP 파이프라인인 bababuyyy의 기본 내부 해상도도 640×360이다 ([GitHub](https://github.com/bababuyyy/unity-isometric-pixel-pipeline)). 지금 보이는 시야보다 12.5% 넓어지는 정도라 플레이 감각 변화는 작다.

틸트 카메라라서 생기는 미묘한 믹셀도 하나 있다. 카메라가 35° 내려다보므로 바닥 텍스처는 화면에서 세로로 sin(35°) ≈ 0.57배 눌려, 32텍셀/유닛 바닥이 세로로는 약 18px/유닛으로 보인다. 카메라를 향해 세운 스프라이트는 눌리지 않는다. 라그나로크도 같은 현상이 있고 플레이어들은 받아들였다. 신경이 쓰이면 나중에 (c)의 저해상 버퍼가 모든 것을 한 그리드로 다시 래스터화하며 해결한다. 이 부분은 분석이며 출처 기반이 아니다.

## Unity URP 구현은 "깊이 쓰는 컷아웃 + 블롭 그림자 + 3D 조명"이 뼈대다

현재 설정(Universal 3D 렌더러, Alpha Clip 머티리얼, X 35° 고정 틸트 스프라이트)은 이미 올바른 기반이다. 스프라이트가 불투명 + 깊이 쓰기로 그려지므로 "나무 뒤로 걸어가기"는 Transparency Sort Mode나 Sorting Group 없이 **깊이 버퍼가 자동으로 해결**한다. 참고로 URP에서 Transparency Sort Mode는 Renderer 2D 에셋의 속성이고, Project Settings > Graphics 위치는 Built-in 파이프라인 시절 이야기다 ([Unity 6.3 URP manual](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/Setup.html)). 남는 문제는 하나다. 기울어진 스프라이트 사각형의 윗부분이 캐릭터 뒤쪽 벽이나 나무 메시를 파고든다. 노력 순으로 해법이 두 가지 있다. 첫째, 콜라이더로 캐릭터를 벽에서 1유닛쯤 떨어뜨린다. 둘째, bgolus의 "수직 빌보드 깊이 트릭"을 손으로 짠 HLSL 셰이더로 옮긴다. 이 트릭은 각 버텍스의 깊이를 "피벗을 지나는 수직 평면"의 깊이로 다시 계산해 더 가까운 값을 쓴다. 클립 공간에서 동작하므로 **Shader Graph로는 깔끔하게 옮길 수 없다**. 2025년 포스트들에 따르면 ShadowCaster/DepthOnly 패스까지 갖춘 lit 버전이 나왔지만, 받는 그림자 위치가 약간 어긋나는 문제는 미해결이다 ([Unity Discussions](https://discussions.unity.com/t/problem-solving-2d-billboard-sprites-clipping-into-3d-environment/743786?page=4)). 같은 아이디어를 구현한 Jiaquarium 셰이더도 있지만 URP 10 시대 코드라 이식이 필요하다. 이 셰이더를 쓰면 SpriteRenderer의 Cast/Receive Shadows가 **Debug 인스펙터 모드에서만 보인다**는 점도 기억해야 한다 ([GitHub](https://github.com/Jiaquarium/unity-URP-2.5D-lit-shader)). 코드에 강한 오너에게는 둘째 방법이 잘 맞는 과제지만, 첫 맵에서는 첫째로 충분하다.

그림자는 처음부터 실시간 섀도맵을 노리지 말고 **발밑 타원 블롭 섀도**로 시작하는 것이 옛 한국 RPG 감성에도 맞고 WebGL에도 안전하다. 기울어진 사각형이 드리우는 진짜 그림자는 늘어지고 찌그러지며, 자기 그림자가 자기 몸에 떨어지는 문제(self-shadowing) 때문에 빛 방향 전용 그림자 쿼드를 따로 두는 식의 처리가 필요하다 ([Unity Discussions](https://discussions.unity.com/t/shadowcaster-that-billboards-towards-light/881036)). 3D 건물·담장 같은 메시는 Directional Light 하나로 하드 섀도를 받게 하고, 스프라이트는 그림자를 "받기만" 하는 구성이 실용적이다.

렌더러는 **Universal(3D) Renderer를 유지**해야 한다. 2D Light는 Mesh Renderer에 영향을 주지 않는다 ([Unity 6.3 manual](https://docs.unity3d.com/Manual/urp/Lights-2D-intro.html)). 6.3에서 들어온 "3D를 2D 렌더러에서 그리기(Sort 3D As 2D)"는 오브젝트의 깊이를 지우고 3D 조명과도 함께 쓸 수 없다 ([Unity Discussions](https://discussions.unity.com/t/rendering-3d-as-2d-in-unity-6-3-beta/1683244)). URP Pixel Perfect Camera 역시 문서상 2D 렌더러 기능이고 3D 렌더러에서의 동작은 언급이 없으니, 지원되지 않는다고 보는 편이 안전하다 ([Unity manual](https://docs.unity3d.com/Manual/urp/2d-pixelperfect-ref.html)). 버전 함정도 크다. **Unity 6.4가 URP Compatibility Mode를 완전히 제거**했기 때문에, `RecordRenderGraph` 없이 `cmd.Blit`을 쓰는 2019~2023년 튜토리얼과 픽셀화 레포는 6000.6에서 그대로 돌아가지 않는다 ([Unity 6.4 upgrade guide](https://docs.unity3d.com/Manual/UpgradeGuideUnity64.html)). 나중에 (c)를 시도할 때 참고할 수 있는 Unity 6 + render graph MIT 레포는 두 개다. bababuyyy는 저해상 다운샘플, 깊이/노멀 외곽선, 카메라 텍셀 스냅 + 서브픽셀 UV 오프셋, 밴드 조명, 인스턴스 풀을 갖췄다. schyro/PixelPipeline은 Deferred+ 렌더러를 요구하고 모바일은 미지원이다 ([bababuyyy](https://github.com/bababuyyy/unity-isometric-pixel-pipeline), [PixelPipeline](https://github.com/schyro/PixelPipeline)). Deferred+ 요구는 WebGL 관점에서 경고 신호다. 저해상 버퍼를 도입하면 월드 공간 TMP 이름표까지 픽셀화되므로, 이름표를 업스케일 이후에 그리도록 옮겨야 한다는 점도 미리 알아 둘 일이다.

조명은 "따뜻한 해 + 차가운 그늘"이 핵심이다. Directional Light는 하나만 쓰고 소프트 섀도는 끈다. Directional 색은 약간 노랗게, Environment Lighting(Ambient)은 약간 푸르게 두면 도트 아티스트들이 손으로 하는 hue-shift를 엔진이 흉내 낸다. 여기에 URP Volume의 LUT 컬러 그레이딩과 약한 비네트를 더한다. 셋 다 싸다. WebGL2는 Unity가 지원하는 웹 그래픽스 API이고 ([Unity 6.6 manual](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/webgl/develop/graphics/web-gl2.md)) 컴퓨트 셰이더가 없다. 따라서 VFX Graph 대신 Shuriken 파티클을, 컴퓨트 풀 대신 `Graphics.RenderMeshInstanced`를 쓴다. 6.0~6.4에는 WebGL2에서 render pass가 잘못 그려지는 이슈(UUM-96046)가 있었으므로 ([Unity Issue Tracker](https://issuetracker.unity.com/issues/7188/glinvalidoperation-errors-and-render-passes-do-not-render-correctly-when-building-for-webgl-with-webgl-2-graphics-api)), 커스텀 패스는 실제 웹 빌드에서 검증해야 한다. 블룸, DoF, URP Decal 같은 포스트 효과의 WebGL2 비용을 정리한 공식 자료는 찾지 못했다. 직접 측정할 수밖에 없다.

맵 지오메트리는 Unity Terrain이 아니라 메시로 만든다. Terrain의 스플랫 블렌딩과 LOD는 도트 느낌과 싸우고 WebGL 비용을 올린다(분석). 바닥은 1×1유닛 타일 쿼드로 구성한다. Tilemap은 Orientation을 XZ로 둘 수 있고 ([Unity manual](https://docs.unity3d.com/2019.3/Documentation/Manual/class-Tilemap.html)), GameObject Brush로 그리드 칸에 프리팹을 찍을 수 있다 ([Tilemap Extras](https://docs.unity3d.com/Packages/com.unity.2d.tilemap.extras@8.0/manual/GameObjectBrush.html)). 이 둘로 배치하거나, 타일 맵 데이터에서 메시 하나를 생성하는 에디터 스크립트를 짠다. 드로우콜을 생각하면 후자가 낫다. 반복을 깨는 데는 데칼보다 버텍스 컬러가 싸다. 연석·화단 테두리·계단 같은 단차는 ProBuilder 상자로 만든다. 이동 제한은 지금 transform으로 움직이는 `PlayerMovement`에 CharacterController나 콜라이더 스윕을 붙이는 방식으로 한다. 서버에도 같은 walkable 마스크를 두면 서버 권위 경계까지 얻는다.

## PixelLab이 1순위, Meshy/Tripo는 보조, 에셋팩은 라이선스부터

PixelLab은 이미 캐릭터를 만든 도구라는 점에서 압도적으로 유리하다. 공개 OpenAPI 스펙 기준으로 정리하면 다음과 같다. `create-tileset`은 아래 지형/위 지형/전환을 서술하면 **16~25장짜리 Wang 타일셋**을 만들고, `view`에 캐릭터와 같은 `"low top-down"`을 지정할 수 있다. 타일 크기는 standard 모드에서 16/32px이다. `target_palette`(정확한 hex 목록 + 최대 색 수)와 `color_image`로 색을 고정할 수 있고, `lower_base_tile_id`로 잔디→흙, 흙→돌길처럼 타일셋을 이어 붙일 수 있다. `map-objects`는 32~400px 투명 배경 소품을 15~30초에 만든다. 시점(low top-down), 외곽선(single color outline), 셰이딩, 디테일 프리셋과 강제 팔레트를 지정할 수 있다. Tiles Pro는 길 오토타일과 벽·문·계단이 든 **건물 키트**까지 만든다. 다만 건물 키트는 스타일 레퍼런스와 함께 쓸 수 없다 ([PixelLab OpenAPI](https://api.pixellab.ai/v2/openapi.json)). 레퍼런스 이미지는 반드시 원본 해상도로 넣어야 한다. 스펙 스스로 "업스케일된 도트가 모든 모델을 혼란시키며, 실망스러운 결과의 가장 흔한 원인"이라고 경고한다 (같은 출처). 주의할 점이 하나 있다. low top-down 타일은 약 30% 깊이의 원근이 그려져 있다. 바닥 타일을 평면에 까는 것은 괜찮지만, 벽이나 나무처럼 원근이 그려진 수직물을 바닥에 눕혀 깔면 실제 35° 카메라 위에서 **원근이 두 번 적용**된다. 수직물은 반드시 세워진 스프라이트나 3D 메시로 처리한다(분석).

| 도구/팩 | 용도 | 비용 | 라이선스·주의 |
|---|---|---|---|
| PixelLab | 바닥 타일셋, 소품, 건물 키트 | 무료 체험 40회; Apprentice $12/월(2,000회), Artisan $24, Architect $50 ([Cinevva, 2026-09](https://app.cinevva.com/guides/ai-pixel-art-generators)) | 공식 가격 페이지 확인 불가, 다른 출처는 $10/1,000회라고 함. 티어별 상업 이용 조건 미확인 |
| Retro Diffusion | 보조 소품, 이미지 도트화(`rd_pro__pixelate`) | 이미지당 $0.03~$0.18, 타일셋 $0.10 ([GitHub](https://github.com/Retro-Diffusion/api-examples)); 크레딧 $5부터 | 웹/API 산출물 상업 라이선스 원문 미확인 |
| Meshy | 3D 블록아웃·레퍼런스 | Free 100크레딧/월, Pro ₩28,869/월 1,000크레딧 ([Meshy pricing](https://www.meshy.ai/pricing)) | **무료 플랜 산출물은 CC BY 4.0(크레딧 표기 의무)**. 도트 텍스처 모드는 확인되지 않음 |
| Tripo | 3D 블록아웃 | 2025년 기준 Professional $15.90/월 3,000크레딧 ([Sloyd](https://sloyd.ai/blog/3d-ai-price-comparison)) | 2026년 가격은 출처마다 다름(공식 페이지 403) |
| LimeZu Modern Exteriors | 현대풍 광장·카페 참고/보충 | 정가 $5, 2026-10 세일 $2.50, 16/32/48px ([itch.io](https://limezu.itch.io/modernexteriors)) | 상업 이용 가능, LimeZu 크레딧 필수, 재배포 금지 |
| Seliel Mana Seed | 판타지 마을 | 팩당 $7.99~$19.99 ([itch.io](https://seliel-the-shaper.itch.io/)) | **제작자가 생성형 AI 콘텐츠와 같은 제품에 쓰면 라이선스 위반이라고 답함** ([itch.io comment](https://itch.io/post/14276406)). dg-village에서는 사용하지 말 것 |

Meshy와 Tripo는 Unity용 포맷(.fbx, .glb)을 내보내지만, 어느 출처도 도트 텍스처를 직접 뽑는 모드를 확인해 주지 않는다. 고해상 PBR풍 텍스처를 그대로 쓰면 32 PPU 캐릭터 옆에서 즉시 믹셀이 된다. 그래서 역할을 "건물 볼륨 블록아웃과 형태 레퍼런스"로 한정한다. 실제 표면은 ProBuilder 상자에 PixelLab 타일을 입히거나, 텍스처를 64~128px로 줄인 뒤 Point 필터와 팔레트 양자화를 거친다. 검증된 파이프라인 자료는 찾지 못했으니 실험으로 취급해야 한다. 에셋팩은 LimeZu 32×32 버전을 32 PPU로 쓰면 픽셀 밀도는 맞는다. 하지만 "모던 코지" 화풍과 팔레트가 girl01의 고채도 색과 달라서, 주력보다는 참고나 빈칸 메우기용이다. 라그나로크·테일즈위버 화풍 그대로인 에셋팩은 찾지 못했다. 원작이 3D 맵이니 당연한 결과다. 덧붙여 Steam 출시 시에는 AI 생성 에셋 사용을 상점 페이지에 고지해야 한다 ([Cinevva](https://app.cinevva.com/guides/ai-pixel-art-generators)).

AI 에셋 사이의 일관성은 도구가 제공하는 레버를 전부 당겨서 얻는다. 같은 시점 프리셋, 같은 외곽선 규칙, 같은 팔레트(`target_palette`), 고정 seed, 같은 32px다. 타일셋의 흔한 실패는 가장자리가 어두워지는 AI "비네트" 때문에 생기는 이음매다. 이것은 반 칸 오프셋 후 덧칠하고 되돌리는 고전 기법으로 고친다 ([Sprite-AI](https://www.sprite-ai.art/blog/seamless-pixel-art-tiles)). 1px 어긋남, 테두리의 잡픽셀, 빠진 코너 변형도 자주 나온다 ([itch.io comments](https://rhosgfx.itch.io/rpg-overworld/comments)). 생성 직후 Unity 테스트 그리드에 4×4로 깔아 확인하는 습관이 필요하다.

## 예쁨은 명도 대비, hue-shift 팔레트, 그리고 "둘러싸인 작은 광장"에서 나온다

가독성은 색상(hue)이 아니라 **명도(value) 대비**에서 나온다. 가장 흔한 실패는 너무 바쁘고 화려한 배경이다. 처방은 배경을 조용하고 대비 낮게 두고, 가장 강한 색과 대비를 캐릭터와 상호작용 대상에 아껴 두는 것이다 ([wayline](https://www.wayline.io/learn/pixel-art/2)). dg-village 캐릭터는 "밝은 고채도 + 진한 단색 외곽선"이므로 맵은 반대로 간다. 바닥은 외곽선 없음(lineless)에 중채도·중간 명도로 두고, 소품만 캐릭터와 같은 외곽선 규칙을 따른다. 그러면 캐릭터가 저절로 떠오른다. 검증은 화면을 그레이스케일로 바꿔 캐릭터가 여전히 읽히는지 보는 것이다.

팔레트는 Slynyrd의 원칙을 따른다. 램프 안에서 밝아질수록 hue를 따뜻하게(스와치당 최대 약 20°) 옮긴다. 밝아질수록 채도를 낮추고, 채도는 램프 중간에서 최고로 둔다. **최대 실수는 "고채도 + 고명도"**이고, hue-shift 없는 직선 램프는 밋밋하다 ([Slynyrd Pixelblog 1](https://slynyrd.com/blog/2018/1/10/pixelblog-1-color-palettes)). 한 게임 세계 전체를 16색으로 그리는 Sly16 같은 예도 있다 ([Slynyrd Pixelblog 28](https://slynyrd.com/blog/2020/5/21/pixelblog-28-side-view-tiles)). 출발점으로는 Lospec의 Resurrect 64나 Endesga 64 같은 범용 64색 팔레트가 있고, 코지 계열로는 12색 Homeyhoney가 있다 ([Lospec](https://lospec.com/palette-list/tag/earthy), [Endesga 64](https://lospec.com/palette-list/endesga-64)). 실제 절차는 이렇다. girl01/boy01에서 쓰인 색을 Aseprite로 추출하고, 가장 가까운 Lospec 팔레트를 고른 뒤, 맵용으로 채도를 한 단계 낮춘 서브셋을 만든다. 이것을 모든 생성의 `target_palette`로 쓰고, 생성 후 Aseprite Indexed 변환으로 한 번 더 고정한다.

코지함은 안전(Safety), 풍족(Abundance), 부드러움(Softness)에서 나온다. 시각 단서는 음식과 따뜻함, 인간 스케일의 공간, 따뜻하고 대비 낮은 색, 자연 재질, 둘러싸인 공간, 바깥이 보이는 창이다. 반대로 코지함을 깨는 것 가운데 하나가 **"광막한 거리(vast distances)"**다 ([Game Developer, Designing for Coziness](https://www.gamedeveloper.com/design/designing-for-coziness)). 소셜 맵에서는 이것이 곧 맵 크기 문제가 된다. Gather는 32px 타일 기준 **예상 동시 접속자 1인당 20~40타일**을 권장한다. 스폰 주변에 여유를 두고 스폰 타일끼리 붙이지 말라고도 한다 ([Gather Help: Sizes](https://support.gather.town/articles/9253868124-sizes)). 이것을 "Gather 1타일 ≈ dg-village 1유닛"으로 근사하면, 30명 광장은 600~1,200유닛², 즉 **25×25~35×35유닛**이다. 현재 20×20 바닥은 10~20명 규모다. 캐릭터가 Gather보다 시각적으로 약 2배 크므로 이 환산은 보수적이다. Size 5.625 카메라는 세로 약 11유닛만 보여 주므로 광장은 "화면 2~3개 분량"이 적당하고, 그보다 크면 사람이 흩어져 썰렁해진다.

레이아웃 정석은 허브 구조다. MMO 공간이 집과 직장 밖의 비공식 사교 장소, 즉 "제3의 장소(third place)"로 기능한다는 연구가 있다 ([Steinkuehler & Williams, JCMC](https://academic.oup.com/jcmc/article/11/4/885/4617703)). Club Penguin의 Town Plaza는 서비스 종료일에 아바타가 몰린 모임 장소로 기록됐다 ([U of Toronto archive](https://omeka.ischool.utoronto.ca/items/show/697)). dg-village 광장의 배치는 다음과 같이 잡는다(분석). 중앙에 랜드마크(분수)를 두고 그 둘레에 원형 모임 공간과 스폰을 둔다. 2~6명용 대화 섬(벤치·테이블 클러스터) 3~4개를 놓고, 가장자리에 건물 입구를 둔다. 건물 입구는 나중에 다른 채널로 가는 포털 역할을 할 수 있다. 외곽은 낮은 담, 수풀, 물로 닫는다. 통과 불가 오브젝트 사이에는 아바타가 지나갈 틈을 남긴다 ([Icograms](https://icograms.com/usage-gather-town)). 외곽을 시각적 오브젝트로 막으면 "둘러싸인 공간"이라는 코지 요소와 "바닥에 경계가 없음"이라는 현재 기술 결함이 한 번에 해결된다. 라그나로크의 프론테라(석조+분수), 페이욘(숲속 목조), 알베르타(항구)처럼 "마을 하나 = 지배적 재질·색 테마 하나"로 가는 방식도 앞으로 채널별 맵 테마를 정할 때 쓸 만하다. 다만 이것은 일반 지식이고 출처로 확인하지는 못했다.

## 첫 광장은 두 주말, 아홉 단계로 끝낸다

아래 계획은 노트의 워크플로우("팔레트 고정 → 그레이박스 → 에셋 리스트 → 생성 → 보정 → 통합 → 폴리시")를 dg-village 현재 상태에 맞춰 구체화한 것이다. 폴리시 단계의 순서는 효과가 큰 것부터다. 에디터 클릭이 많은 반복 작업(바닥 메시 생성, 타일 배치)은 CLAUDE.md의 관례대로 **DgVillage 메뉴 스크립트**로 만드는 편이 낫다.

| 단계 | 할 일 | 산출물/완료 기준 | 시점 |
|---|---|---|---|
| 0. 픽셀 그리드 | Main Camera Ortho Size 5 → 5.625. 테스트 빌드를 720p/1080p로 확인 | 걸을 때 바닥 반짝임 감소, 텍셀 하나가 정수 화면픽셀 | 토 오전 |
| 1. 무드보드 | 프론테라·알베르타, 옥토패스, 투더문 마을 스크린샷 10~20장, 각 "좋은 점 1줄". 시간대를 하나로(낮 또는 노을) 통일 | 레퍼런스 폴더 | 토 오전 |
| 2. 팔레트 | girl01/boy01 색 추출 → Lospec 팔레트 1개 선택 → 맵용 저채도 서브셋 32~48색 | `.gpl`/hex 목록 (PixelLab `target_palette` 입력값) | 토 오전 |
| 3. 그레이박스 | 큐브/플레인으로 28×28유닛 광장: 중앙 분수 자리, 스폰, 대화 섬 3~4, 길, 건물 입구 2, 외곽 담/수풀. 플레이어 여러 명을 세워 놓고 동선·밀도 확인, 그레이스케일 확인 | 회색 광장 씬 | 토 오후 |
| 4. 바닥 타일 | PixelLab `create-tileset`: low top-down, 32px, lineless, 팔레트 고정. 잔디↔돌길, 돌길↔흙을 base tile로 연결. 변형 타일 3~4종. 이음매 점검·수정 | 32px 타일 PNG(Point, 무압축, 32 PPU) | 토 오후 |
| 5. 바닥 구성 | 에디터 스크립트로 타일 맵 → 바닥 메시 생성(1유닛 = 1타일 = 32텍셀). 버텍스 컬러 노이즈로 반복 깨기. ProBuilder로 연석·화단 테두리·분수 받침 단차 추가 | "평면" 대신 높이차 있는 바닥 | 일 |
| 6. 소품 | PixelLab `map-objects`(low top-down, single color outline, 같은 팔레트): 분수, 벤치, 가로등, 화분, 나무 2종, 덤불, 담장. Aseprite로 광원 방향(예: 좌상단)과 팔레트 보정. 캐릭터와 같은 importer·Alpha Clip·X 35° 틸트로 배치. 발밑 블롭 섀도 | 소품 5~8종 배치, 외곽 경계 콜라이더 | 일 |
| 7. 조명 | 따뜻한 Directional 1개(하드 섀도) + 푸른 Ambient + LUT 컬러 그레이딩 + 약한 비네트 | 그레이스케일 테스트 통과 | 일 저녁 |
| 8. 건물·밀도 | 건물 외관 2개(ProBuilder 몸체 + 도트 텍스처, 또는 PixelLab 건물 키트). 빈 바닥에 풀포기·돌·꽃 데칼 스프라이트 | 캐릭터가 "외로워 보이지 않는" 밀도 | 다음 주말 |
| 9. 앰비언트 | 분수 물(스크롤 텍스처), 풀·나무 스프라이트의 버텍스 흔들림(텍셀 단위로 양자화), Shuriken 나비/낙엽, 월드 XZ 기준 스크롤 구름 그림자 | 정지 화면에서도 살아 있는 느낌 | 다음 주말 |

예산은 PixelLab Apprentice 한 달(약 $12)이면 이 범위를 넉넉히 덮는다. 타일셋 하나와 맵 오브젝트 하나가 각각 생성 몇 회를 쓰는지는 공식 자료로 확인하지 못했다. 무료 체험 40회로 바닥 타일셋 하나와 소품 두세 개를 먼저 뽑아 보고, 그 결과를 보고 결제하는 것이 안전하다. Meshy를 무료 플랜으로 쓴다면 CC BY 4.0 크레딧 표기를 게임에 넣어야 한다.

## Conclusion

이 조사로 바뀐 핵심 인식은 이것이다. "맵을 예쁘게"는 그림 실력의 문제가 아니라 **규율의 문제**다. 라그나로크가 예뻐 보인 이유는 화려한 원화가 아니었다. 32px 작은 텍스처, 저해상 그림자, 버텍스 컬러라는 값싼 재료를 한 가지 픽셀 밀도로 묶었기 때문이다. HD-2D가 실패에서 배운 것도 "더 고해상·고채도"가 아니라 "깊이, 빛, 밀도"였다. 따라서 미술 초보인 오너에게 가장 큰 지렛대는 숫자로 강제할 수 있는 규칙이다. 32텍셀/유닛, 정수 카메라 배율, 고정 팔레트, 단일 광원 방향, 외곽선 규칙이 그것이고, 이 규칙들은 에디터 스크립트와 임포터로 코드화할 수 있다. 프로그래머의 강점이 그대로 아트 품질로 전환되는 지점이다.

남은 불확실성은 세 가지다. PixelLab 타일이 3D 바닥 텍스처로서 이음매 품질이 실제로 어떤지는 직접 써 본 후기가 없다. 저해상 RT 방식이 기존 64×64 스프라이트와 함께 쓰일 때의 품질과 WebGL2 동작은 검증된 사례가 없다. 생성 1회당 크레딧 비용도 확인되지 않았다. 셋 다 첫 주말의 작은 실험으로 답이 나오는 질문이다. RO 하이브리드는 나중에 (c)로 올라가는 길을 막지 않는다. 같은 32텍셀 밀도로 만든 에셋은 640×360 버퍼에 그대로 들어맞으므로, 지금의 선택이 미래의 업그레이드 경로이기도 하다.
