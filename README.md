# dg-village

**아름다운 게임**을 목표로 하는 멀티플레이 게임. 2026-10-26에 처음부터 다시 시작했다 (이전 버전은 git 기록에만 남아 있음).

2D 도트 캐릭터가 3D 세계를 탐험한다.

- 맵은 3D — 땅은 Unity Terrain, 겉모습은 도트 텍스처
- 캐릭터·사물은 2D sprite (플레이어는 8방향)
- 충돌·중력은 Unity 물리 (`Collider`, `Rigidbody`)
- 고정 카메라 (회전 없음, Orthographic 검토 중)
- 처음부터 멀티플레이를 고려한 구조

## 구성 (예정)

```
dg-village/
├── unity/    # 게임 클라이언트 (Unity 6 · URP)
└── python/   # 게임 서버 (Python)
```

| 영역 | 기술 |
|---|---|
| 클라이언트 | Unity `6000.6.6f1` · Universal Render Pipeline |
| 서버 | Python — 기존 AWS Lightsail(시드니) 서버에 배포 |
| 플랫폼 | Mac · Windows 데스크톱 우선 |
