-- Auth-first schema (signup/login priority)
-- This script only includes users + refresh_tokens.

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS users (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    email           varchar(320) NOT NULL,
    password_hash   text NOT NULL,
    nickname        varchar(50),
    role            smallint NOT NULL DEFAULT 0,
    status          smallint NOT NULL DEFAULT 1,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    last_login_at   timestamptz NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_users_email ON users(email);
CREATE UNIQUE INDEX IF NOT EXISTS uq_users_nickname ON users(nickname) WHERE nickname IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_users_status ON users(status);

CREATE TABLE IF NOT EXISTS refresh_tokens (
    id              bigserial PRIMARY KEY,
    user_id         uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash      text NOT NULL,
    expires_at      timestamptz NOT NULL,
    revoked_at      timestamptz NULL,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_refresh_tokens_token_hash ON refresh_tokens(token_hash);
CREATE INDEX IF NOT EXISTS idx_refresh_tokens_user_id ON refresh_tokens(user_id);
CREATE INDEX IF NOT EXISTS idx_refresh_tokens_expires_at ON refresh_tokens(expires_at);
