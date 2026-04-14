# Docker Desktop 실행 가이드

`GameServer`를 Docker Desktop 기준으로 바로 띄울 수 있도록 폴더를 아래처럼 구성했습니다.

## 폴더 구조

```text
GameServer/
├─ src/
│  └─ GameServer.Api/
│     └─ Dockerfile
├─ deploy/
│  └─ docker/
│     ├─ docker-compose.yml
│     ├─ .env.example
│     └─ README.md
└─ .dockerignore
```

## 실행 순서

1. 환경변수 파일 준비

```bash
cd GameServer/deploy/docker
cp .env.example .env
```

2. 컨테이너 빌드 + 실행

```bash
docker compose up -d --build
```

3. 로그 확인

```bash
docker compose logs -f api
```

## 현재 `gameserver_db`에 바로 접속 (새 DB 생성 없이)

`docker compose up -d` 로 이미 떠 있는 상태라면, 아래 명령으로 현재 DB에 바로 붙을 수 있습니다.

### PowerShell 스크립트 사용

```powershell
cd GameServer/deploy/docker
.\connect-current-db.ps1
```

단일 쿼리 실행:

```powershell
.\connect-current-db.ps1 -Query "SELECT current_database(), current_user;"
.\connect-current-db.ps1 -Query "SELECT table_name FROM information_schema.tables WHERE table_schema='public' ORDER BY table_name;"
```

### 스크립트 없이 직접 실행

```bash
docker compose exec postgres psql -U postgres -d gameserver_db
docker compose exec postgres psql -U postgres -d gameserver_db -c "SELECT current_database(), current_user;"
```

4. 종료

```bash
docker compose down
```

## 스키마/데이터가 다르게 보일 때 (초기화)

기존 `postgres_data` 볼륨에 과거 스키마가 남아 있으면, 레포의 `db/*.sql`과 다른 테이블 구조가 보일 수 있습니다.
아래 순서로 볼륨까지 초기화한 뒤 다시 올리세요.

```bash
cd GameServer/deploy/docker
docker compose down -v
docker compose up -d --build
```

초기화 후 확인:

```bash
docker compose exec postgres psql -U postgres -d gameserver_db -c "SELECT current_database(), current_user;"
docker compose exec postgres psql -U postgres -d gameserver_db -c "SELECT table_name FROM information_schema.tables WHERE table_schema='public' ORDER BY table_name;"
```

## 기본 접근 정보

- API: <http://localhost:8080>
- PostgreSQL: `localhost:5432`
  - DB: `gameserver_db`
  - USER: `postgres`
  - PASSWORD: `postgres`

## 참고

- `db/001_core_schema.sql` 파일만 PostgreSQL 최초 기동 시 자동 실행됩니다.
- `db/002_sample_queries.sql`, `db/011_auth_only_queries.sql` 는 운영/개발 참고용 쿼리 모음이며 초기화 시 자동 실행되지 않습니다.
- `Database__Postgres__*` 환경 변수로 API의 DB 연결 정보를 컨테이너 환경에서 덮어씁니다.
