# 배포 (운영 서버)

Caddy(HTTPS) → 게임 서버 → PostgreSQL 을 한 대의 VPS 에서 docker compose 로 띄운다.

```
인터넷 ──80/443──▶ caddy ──▶ server:8000 ──▶ postgres:5432
                   (공개)     (내부 전용)      (내부 전용)
```

- 서버 컨테이너는 뜰 때마다 `alembic upgrade head` 를 먼저 돌리고 uvicorn 을 띄운다. 마이그레이션은 따로 할 일이 없다.
- 방(채널 안 사람들 · 위치)은 메모리에 있으므로 **서버 프로세스는 반드시 하나**. 재시작하면 접속자는 모두 끊기고 다시 들어와야 한다 (계정 · 채널은 DB 라 남는다).
- `.env` 는 서버에만 둔다. git 에 올리지 않는다 (`.gitignore` 대상).

## 처음 띄우기

```bash
cd deploy
cp .env.example .env
# .env 편집: 비밀값 두 개는 openssl rand -hex 32 로 만든다
docker compose up -d --build
docker compose ps          # 셋 다 running, server 는 (healthy)
curl https://<DG_DOMAIN>/health
```

`DG_DOMAIN`:

| 값 | 결과 |
|---|---|
| `13-125-1-2.sslip.io` (VPS IP 의 점을 대시로) | 도메인 없이 HTTPS. Caddy 가 Let's Encrypt 인증서를 자동 발급 · 갱신 |
| 산 도메인 (`village.example.com`) | DNS A 레코드를 VPS IP 로 걸고 그 이름을 쓴다 |
| `http://localhost` | 내 맥에서 이 구성을 시험할 때. HTTPS 없이 80 포트 |

인증서 발급은 80/443 이 밖에서 열려 있어야 한다 (VPS 방화벽).

## 업데이트

`python/` · `deploy/` 를 바꾼 커밋이 main 에 push 되면 자동 배포된다 (아래). 손으로 할 때는:

```bash
deploy/deploy.sh   # git pull → docker compose up -d --build --wait → 옛 이미지 정리
```

## 자동 배포 (GitHub Actions)

`.github/workflows/deploy.yml`: main push (`python/**`, `deploy/**`, 워크플로우 파일 변경 시) → `test` (ruff + pytest) → `deploy` (SSH 로 `deploy/deploy.sh` 실행). Actions 탭의 **Run workflow** 로 수동 실행도 된다.

- 배포 전용 키 `~/.ssh/dg-village-deploy` (관리자 맥). 서버 `~/.ssh/authorized_keys` 에 `restrict,command="/home/ubuntu/dg-village/deploy/deploy.sh"` 로 등록 → 이 키로는 배포 스크립트만 돌고 셸 · 포트 포워딩은 안 된다.
- GitHub Secrets: `DEPLOY_SSH_KEY` (private key), `DEPLOY_HOST` (`13.54.176.157`), `DEPLOY_KNOWN_HOSTS` (서버 host key 한 줄, 접속 대상 검증용).
- 키 교체: 새 키 생성 → `authorized_keys` 의 그 줄 교체 → `gh secret set DEPLOY_SSH_KEY < 새키`.
- 서버 작업 트리를 손으로 고치면 `git pull --ff-only` 가 실패해 배포가 멈춘다. 서버에서는 `deploy/.env` 외에는 고치지 말 것.

## 백업

두 겹:

| 무엇 | 언제 | 막아주는 것 |
|---|---|---|
| `deploy/backup.sh` (pg_dump → `~/backups/`, 14일 보관) | 매일 03:00 KST (cron) | 실수로 지운 데이터, DB 깨짐 |
| Lightsail 자동 스냅샷 (인스턴스 디스크 전체) | 매일 (콘솔에서 켬) | 인스턴스 자체를 잃음 |

cron 등록 (서버에서 한 번):

```bash
(crontab -l 2>/dev/null | grep -v deploy/backup.sh; echo '0 18 * * * $HOME/dg-village/deploy/backup.sh >> $HOME/backups/backup.log 2>&1') | crontab -
```

서버 시계는 UTC (18:00 UTC = 03:00 KST). 확인: `crontab -l`, `tail ~/backups/backup.log`, `ls -lh ~/backups`.

복원 (서버에서, **현재 DB 를 덮어쓴다** — 먼저 `deploy/backup.sh` 로 지금 상태를 떠 둘 것):

```bash
cd ~/dg-village/deploy
sudo docker compose stop server caddy
sudo docker compose exec -T postgres pg_restore -U dg_village -d dg_village --clean --if-exists < ~/backups/dg_village-<시각>.dump
sudo docker compose up -d --wait
```

맥으로 가져오기: `scp -i ~/.ssh/dg-village ubuntu@13.54.176.157:backups/<파일> .`

## 자주 쓰는 명령

```bash
docker compose logs -f server                         # 서버 로그
docker compose restart server                         # 서버만 재시작
docker compose exec postgres psql -U dg_village       # DB 접속
docker compose exec postgres pg_dump -U dg_village dg_village > backup.sql   # 백업
docker compose down                                   # 정지 (데이터는 볼륨에 남는다)
```

`docker compose down -v` 는 **DB 와 인증서까지 지운다**. 운영에서는 쓰지 말 것.

## 내 맥에서 시험하기

개발용 `compose.yaml`(루트, Postgres 만)과 프로젝트 이름이 달라서(`dg-village-prod`) 같이 띄워도 섞이지 않는다.
`.env` 에 `DG_DOMAIN=http://localhost` 로 띄우면 `http://localhost` · `ws://localhost/ws` 로 접속된다.
다 봤으면 `docker compose down -v` 로 치운다.
