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

```powershell
cd GameServer/deploy/docker
Copy-Item .env.example .env
```

2. 컨테이너 빌드 + 실행

```bash
docker compose up -d --build
```

3. 로그 확인

```bash
docker compose logs -f api
```


## 즉시 실행용 명령어 (스크립트 없이)

아래 3개만 순서대로 실행하면 **DB 연결 확인 + user 추가 + 결과 확인**을 바로 할 수 있습니다.

```powershell
cd GameServer/deploy/docker

# 1) DB 연결 확인
docker compose exec postgres psql -U postgres -d gameserver_db -c "SELECT current_database(), current_user;"

# 2) user 추가(이미 있으면 업데이트)
docker compose exec postgres psql -U postgres -d gameserver_db -c "INSERT INTO users (email, password_hash, nickname, role, status) VALUES ('test1@nunbora.local', 'test1234', 'tester1', 0, 1) ON CONFLICT (email) DO UPDATE SET password_hash = EXCLUDED.password_hash, nickname = EXCLUDED.nickname, updated_at = now() RETURNING id, email, nickname, role, status, created_at;"

# 3) 추가 결과 확인
docker compose exec postgres psql -U postgres -d gameserver_db -c "SELECT id, email, nickname, role, status, created_at, updated_at FROM users WHERE email = 'test1@nunbora.local';"
```

pgAdmin에서 같은 값 확인 SQL:

```sql
SELECT id, email, nickname, role, status, created_at, updated_at
FROM public.users
WHERE email = 'test1@nunbora.local';
```

### 같은 DB가 맞는지(어디에 저장되는지) 즉시 확인

지금 상황은 대부분 **서로 다른 PostgreSQL 인스턴스**를 보고 있을 때 발생합니다.
(예: `docker compose`의 `gameserver-postgres` 컨테이너 vs 로컬에 따로 설치된 PostgreSQL 18 서비스)

터미널과 pgAdmin에서 아래 "지문(fingerprint)" 쿼리를 **동일하게** 실행해 결과를 비교하세요.

```sql
SELECT
    current_database()      AS db_name,
    current_user            AS db_user,
    inet_server_addr()      AS server_addr,
    inet_server_port()      AS server_port,
    version()               AS pg_version,
    current_setting('data_directory') AS data_dir;
```

터미널(도커 컨테이너 내부) 실행 예시:

```powershell
docker compose exec postgres psql -U postgres -d gameserver_db -c "SELECT current_database() AS db_name, current_user AS db_user, inet_server_addr() AS server_addr, inet_server_port() AS server_port, version() AS pg_version, current_setting('data_directory') AS data_dir;"
```

추가로 컨테이너 자체 확인:

```powershell
docker compose ps
docker compose port postgres 5432
```

- fingerprint가 다르면, **저장 위치가 다른 DB**를 보고 있는 것입니다.

판독 예시(현재 질문 상황과 동일):
- 터미널 결과: `PostgreSQL 16.x (Debian)`, `data_dir=/var/lib/postgresql/data` -> **Docker 컨테이너 DB**
- pgAdmin 결과: `PostgreSQL 18.x (Windows)`, `data_dir=C:/Program Files/PostgreSQL/18/data` -> **로컬 Windows 서비스 DB**

즉, VS 포트 미연결 문제가 아니라 **서로 다른 DB 인스턴스에 각각 붙은 상태**입니다.

- pgAdmin 서버를 `Host=127.0.0.1`, `Port=5432`, `Database=gameserver_db`, `User=postgres`로 맞춰서 다시 연결하세요.
- pgAdmin 상단 탭 제목에 보이는 서버 별칭(예: `...@PostgreSQL 18`)이 로컬 서비스일 수 있으니, 기존 서버 엔트리를 지우고 새로 등록하는 것이 가장 빠릅니다.

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

## 트러블슈팅 (빌드 실패 + DB 연결 실패를 같이 점검)

아래 1~6을 순서대로 확인하면, **API 빌드/기동 실패**와 **pgAdmin 연결 실패**를 한 번에 정리할 수 있습니다.

### 1) compose 환경값 확인 (.env)

`GameServer/deploy/docker/.env`가 없거나 값이 비어 있으면 연결 오류가 연쇄적으로 납니다.

```bash
cd GameServer/deploy/docker
cp -n .env.example .env
cat .env
```

```powershell
cd GameServer/deploy/docker
if (!(Test-Path .env)) { Copy-Item .env.example .env }
Get-Content .env
```

최소한 아래 3개가 채워져 있어야 합니다.

```env
POSTGRES_DB=gameserver_db
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
```

### 2) 컨테이너 상태 + API 로그 확인

```bash
cd GameServer/deploy/docker
docker compose ps
docker compose logs --tail=200 postgres
docker compose logs --tail=200 api
```

- `postgres`가 `healthy`가 아니면 API가 DB 연결에 실패합니다.
- API 로그에 연결 문자열/인증 관련 오류가 보이면 `.env`와 pgAdmin 설정을 같이 재확인하세요.

### 3) pgAdmin의 `no password supplied` 오류 해결

스크린샷의 오류(`fe_sendauth: no password supplied`)는 대부분 **비밀번호 입력값이 실제로 전달되지 않은 상태**입니다.

pgAdmin 서버 등록/수정 시 아래를 정확히 맞추세요.

- Host name/address: `127.0.0.1`
- Port: `5432`
- Maintenance database: `gameserver_db`
- Username: `postgres`
- Password: `.env`의 `POSTGRES_PASSWORD` 값 (기본 `postgres`)

추가 체크:
- 비밀번호 입력창이 비어 있지 않은지 확인
- `Save Password`를 체크 후 재접속
- 동일 이름의 기존 서버 엔트리가 잘못된 비밀번호를 기억하고 있으면 삭제 후 재등록

### 4) "테이블이 15개로 보임" 문제 정리

이 프로젝트의 `001_core_schema.sql` 기준으로 **핵심 테이블은 9개**입니다. 
pgAdmin에서 15개로 보이는 경우는 보통 아래 둘 중 하나입니다.

1. 과거 볼륨 데이터가 남아 있어 이전 테이블이 같이 존재
2. 시스템/확장 테이블까지 포함해서 보고 있음

정확한 확인 쿼리(사용자 테이블만 카운트):

```sql
SELECT count(*) AS public_base_tables
FROM information_schema.tables
WHERE table_schema = 'public' AND table_type = 'BASE TABLE';
```

현재 DB/유저 확인:

```sql
SELECT current_database(), current_user;
```

`public` 스키마 테이블 목록 확인:

```sql
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
ORDER BY table_name;
```

기존 볼륨을 완전히 비우고 스키마를 다시 맞추려면 다음을 실행하세요.

```bash
cd GameServer/deploy/docker
docker compose down -v
docker compose up -d --build
```



### 5) Visual Studio `CS0006` (`GameServer.Infrastructure.dll` 메타데이터 파일을 찾을 수 없음)

스크린샷처럼 `CS0006`이 뜨는 경우, 대부분은 **선행 프로젝트(GameServer.Infrastructure) 빌드 실패의 결과 오류**입니다.
특히 출력창에 아래 에러가 먼저 나온 경우:

- `CS0117`: `'User'에는 'Email'에 대한 정의가 없음`
- `CS1061`: `'User'에는 'PasswordHash'에 대한 정의가 없음`

`GameServer.Domain/Entities/User.cs`의 최신 코드와 로컬 워킹트리가 어긋났거나, Visual Studio 캐시(`bin/obj/.vs`)가 꼬였을 가능성이 큽니다.

우선순위:
1. 위 1단계의 PowerShell 명령으로 `.env`부터 정상 생성
2. Visual Studio 종료 후 `.vs`, `bin`, `obj` 폴더 삭제
3. Visual Studio 재실행 후 `Build > Clean Solution` -> `Build > Rebuild Solution`
4. 계속 실패하면 `GameServer.Infrastructure` 프로젝트를 먼저 단독 빌드해 **첫 번째 실제 컴파일 에러**를 확인

CLI로 확인할 때:

```powershell
cd GameServer
git status
dotnet restore
dotnet build .\src\GameServer.Domain\GameServer.Domain.csproj
dotnet build .\src\GameServer.Infrastructure\GameServer.Infrastructure.csproj
dotnet build .\src\GameServer.Api\GameServer.Api.csproj
```

PowerShell에서 캐시 폴더를 지울 때:

```powershell
cd GameServer
Get-ChildItem -Recurse -Directory -Filter bin | Remove-Item -Recurse -Force
Get-ChildItem -Recurse -Directory -Filter obj | Remove-Item -Recurse -Force
Remove-Item .vs -Recurse -Force -ErrorAction SilentlyContinue
```

`CS0006` 자체는 2차 증상인 경우가 많으므로, 빌드 출력창의 **가장 먼저 나오는 에러 한 줄**을 기준으로 원인을 잡는 것이 가장 빠릅니다.



### 6) 터미널에서 user 추가 + pgAdmin 확인

> 현재 `PasswordHasher`가 평문 비교(`password == passwordHash`)이므로, DB에 넣는 `password_hash`도 평문으로 맞춰야 로그인 테스트가 됩니다.

PowerShell(프로젝트 루트 `/GameServer/deploy/docker` 기준):

```powershell
cd GameServer/deploy/docker
docker compose exec postgres psql -U postgres -d gameserver_db -c "INSERT INTO users (email, password_hash, nickname, role, status) VALUES ('test1@nunbora.local', 'test1234', 'tester1', 0, 1) ON CONFLICT (email) DO UPDATE SET password_hash = EXCLUDED.password_hash, nickname = EXCLUDED.nickname, updated_at = now() RETURNING id, email, nickname, role, status, created_at;"
```

추가 확인(터미널):

```powershell
docker compose exec postgres psql -U postgres -d gameserver_db -c "SELECT id, email, nickname, role, status, created_at, updated_at FROM users WHERE email = 'test1@nunbora.local';"
```

pgAdmin에서 확인:
1. `gameserver_db` -> Schemas -> public -> Tables -> `users` 우클릭
2. **View/Edit Data -> All Rows** 선택
3. 아래 쿼리 실행으로 동일 결과 확인

```sql
SELECT id, email, nickname, role, status, created_at, updated_at
FROM public.users
WHERE email = 'test1@nunbora.local';
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
