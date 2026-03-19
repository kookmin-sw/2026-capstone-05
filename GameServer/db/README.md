# GameServer DB SQL

## 현재 우선순위 (Auth First)
- `010_auth_only_schema.sql`: 회원가입/로그인 우선용 스키마 (`users`, `refresh_tokens`)
- `011_auth_only_queries.sql`: 회원가입/로그인/리프레시토큰 샘플 쿼리

## 전체 스키마 (다음 단계)
- `001_core_schema.sql`: 9개 핵심 테이블 전체 DDL
- `002_sample_queries.sql`: 전체 기능 샘플 쿼리

권장:
1. 지금은 `010`, `011`만 적용
2. 아이템/진행/매치 구현 시 `001`, `002`로 확장
