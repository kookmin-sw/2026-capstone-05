-- Core schema for GameServer (PostgreSQL)
-- 9 core tables:
-- 1) users
-- 2) refresh_tokens
-- 3) items
-- 4) player_currencies
-- 5) player_inventory_items
-- 6) player_progress
-- 7) match_sessions
-- 8) match_participant_results
-- 9) reward_ledger

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- 1) users
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

-- 2) refresh_tokens
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

-- 3) items (master)
CREATE TABLE IF NOT EXISTS items (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    item_code       varchar(64) NOT NULL,
    name            varchar(100) NOT NULL,
    item_type       smallint NOT NULL,
    rarity          smallint NOT NULL,
    max_stack       int NOT NULL DEFAULT 1,
    is_tradeable    boolean NOT NULL DEFAULT false,
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_items_max_stack CHECK (max_stack > 0)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_items_item_code ON items(item_code);
CREATE INDEX IF NOT EXISTS idx_items_item_type ON items(item_type);
CREATE INDEX IF NOT EXISTS idx_items_is_active ON items(is_active);

-- 4) player_currencies
CREATE TABLE IF NOT EXISTS player_currencies (
    id              bigserial PRIMARY KEY,
    user_id         uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    currency_type   smallint NOT NULL,
    amount          bigint NOT NULL DEFAULT 0,
    updated_at      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_player_currencies_amount CHECK (amount >= 0)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_player_currencies_user_currency
    ON player_currencies(user_id, currency_type);
CREATE INDEX IF NOT EXISTS idx_player_currencies_user_id ON player_currencies(user_id);

-- 5) player_inventory_items
CREATE TABLE IF NOT EXISTS player_inventory_items (
    id              bigserial PRIMARY KEY,
    user_id         uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    item_id         uuid NOT NULL REFERENCES items(id) ON DELETE RESTRICT,
    quantity        int NOT NULL DEFAULT 0,
    acquired_at     timestamptz NULL,
    updated_at      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_player_inventory_items_quantity CHECK (quantity >= 0)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_player_inventory_user_item
    ON player_inventory_items(user_id, item_id);
CREATE INDEX IF NOT EXISTS idx_player_inventory_user_id ON player_inventory_items(user_id);
CREATE INDEX IF NOT EXISTS idx_player_inventory_item_id ON player_inventory_items(item_id);

-- 6) player_progress
CREATE TABLE IF NOT EXISTS player_progress (
    id                  bigserial PRIMARY KEY,
    user_id             uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    progressed_day      date NOT NULL,
    chapter             int NOT NULL DEFAULT 1,
    stage               int NOT NULL DEFAULT 1,
    last_cleared_stage  int NULL,
    updated_at          timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_player_progress_chapter CHECK (chapter > 0),
    CONSTRAINT ck_player_progress_stage CHECK (stage > 0),
    CONSTRAINT ck_player_progress_last_cleared_stage CHECK (last_cleared_stage IS NULL OR last_cleared_stage >= 0)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_player_progress_user_id ON player_progress(user_id);
CREATE INDEX IF NOT EXISTS idx_player_progress_progressed_day ON player_progress(progressed_day);

-- 7) match_sessions
CREATE TABLE IF NOT EXISTS match_sessions (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    room_name       varchar(100) NOT NULL,
    host_user_id    uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    status          smallint NOT NULL DEFAULT 0, -- 0=open, 1=closed, 2=settled
    started_at      timestamptz NOT NULL,
    ended_at        timestamptz NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_match_sessions_time CHECK (ended_at IS NULL OR ended_at >= started_at)
);

CREATE INDEX IF NOT EXISTS idx_match_sessions_host_user_id ON match_sessions(host_user_id);
CREATE INDEX IF NOT EXISTS idx_match_sessions_status ON match_sessions(status);
CREATE INDEX IF NOT EXISTS idx_match_sessions_started_at ON match_sessions(started_at);

-- 8) match_participant_results
CREATE TABLE IF NOT EXISTS match_participant_results (
    id                  bigserial PRIMARY KEY,
    match_id            uuid NOT NULL REFERENCES match_sessions(id) ON DELETE CASCADE,
    user_id             uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    score               int NOT NULL DEFAULT 0,
    is_win              boolean NOT NULL DEFAULT false,
    reported_by_host    boolean NOT NULL DEFAULT false,
    created_at          timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_match_participant_results_match_user
    ON match_participant_results(match_id, user_id);
CREATE INDEX IF NOT EXISTS idx_match_participant_results_user_id ON match_participant_results(user_id);

-- 9) reward_ledger
CREATE TABLE IF NOT EXISTS reward_ledger (
    id                  bigserial PRIMARY KEY,
    user_id             uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    match_id            uuid NULL REFERENCES match_sessions(id) ON DELETE SET NULL,
    reward_type         smallint NOT NULL, -- 1=currency, 2=item
    currency_type       smallint NULL,
    item_id             uuid NULL REFERENCES items(id) ON DELETE SET NULL,
    delta_amount        bigint NOT NULL,
    reason              varchar(50) NOT NULL,
    idempotency_key     varchar(100) NOT NULL,
    created_at          timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_reward_ledger_reward_type CHECK (reward_type IN (1, 2)),
    CONSTRAINT ck_reward_ledger_currency_fields CHECK (
        (reward_type = 1 AND currency_type IS NOT NULL AND item_id IS NULL)
        OR
        (reward_type = 2 AND currency_type IS NULL AND item_id IS NOT NULL)
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_reward_ledger_idempotency_key ON reward_ledger(idempotency_key);
CREATE INDEX IF NOT EXISTS idx_reward_ledger_user_id_created_at ON reward_ledger(user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_reward_ledger_match_id ON reward_ledger(match_id);
