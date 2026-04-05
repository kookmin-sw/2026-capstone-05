-- Sample operational queries for GameServer core schema

-- [Q1] Signup user
INSERT INTO users (email, password_hash, nickname, role, status)
VALUES ($1, $2, $3, 0, 1)
RETURNING id;

-- [Q2] Login lookup by email
SELECT id, email, password_hash, role, status
FROM users
WHERE email = $1;

-- [Q3] Upsert player currency (grant/spend positive or negative handled by app logic)
INSERT INTO player_currencies (user_id, currency_type, amount, updated_at)
VALUES ($1, $2, $3, now())
ON CONFLICT (user_id, currency_type)
DO UPDATE SET
    amount = GREATEST(0, player_currencies.amount + EXCLUDED.amount),
    updated_at = now()
RETURNING amount;

-- [Q4] Safe spend currency (fails if insufficient)
UPDATE player_currencies
SET amount = amount - $3,
    updated_at = now()
WHERE user_id = $1
  AND currency_type = $2
  AND amount >= $3
RETURNING amount;

-- [Q5] Upsert inventory quantity
INSERT INTO player_inventory_items (user_id, item_id, quantity, acquired_at, updated_at)
VALUES ($1, $2, $3, now(), now())
ON CONFLICT (user_id, item_id)
DO UPDATE SET
    quantity = player_inventory_items.quantity + EXCLUDED.quantity,
    updated_at = now()
RETURNING quantity;

-- [Q6] Save / initialize progress
INSERT INTO player_progress (user_id, progressed_day, chapter, stage, last_cleared_stage, updated_at)
VALUES ($1, $2, $3, $4, $5, now())
ON CONFLICT (user_id)
DO UPDATE SET
    progressed_day = EXCLUDED.progressed_day,
    chapter = EXCLUDED.chapter,
    stage = EXCLUDED.stage,
    last_cleared_stage = EXCLUDED.last_cleared_stage,
    updated_at = now();

-- [Q7] Create match session (host opens room)
INSERT INTO match_sessions (room_name, host_user_id, status, started_at)
VALUES ($1, $2, 0, now())
RETURNING id;

-- [Q8] Upsert host-reported participant result
INSERT INTO match_participant_results (match_id, user_id, score, is_win, reported_by_host)
VALUES ($1, $2, $3, $4, true)
ON CONFLICT (match_id, user_id)
DO UPDATE SET
    score = EXCLUDED.score,
    is_win = EXCLUDED.is_win,
    reported_by_host = true;

-- [Q9] Idempotent reward ledger insert (prevents double reward)
INSERT INTO reward_ledger (
    user_id, match_id, reward_type, currency_type, item_id,
    delta_amount, reason, idempotency_key, created_at
)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8, now())
ON CONFLICT (idempotency_key) DO NOTHING
RETURNING id;

-- [Q10] Settlement transaction sketch (run in one transaction)
-- 1) lock session row
SELECT id, status
FROM match_sessions
WHERE id = $1
FOR UPDATE;

-- 2) close/settle match state
UPDATE match_sessions
SET status = 2,
    ended_at = COALESCE(ended_at, now())
WHERE id = $1
  AND status <> 2;

-- [Q11] Player snapshot for client sync
SELECT
    u.id AS user_id,
    p.progressed_day,
    p.chapter,
    p.stage,
    pc.currency_type,
    pc.amount,
    pii.item_id,
    pii.quantity
FROM users u
LEFT JOIN player_progress p ON p.user_id = u.id
LEFT JOIN player_currencies pc ON pc.user_id = u.id
LEFT JOIN player_inventory_items pii ON pii.user_id = u.id
WHERE u.id = $1;

-- [Q12] Reward audit for support/admin
SELECT
    rl.id,
    rl.user_id,
    rl.match_id,
    rl.reward_type,
    rl.currency_type,
    rl.item_id,
    rl.delta_amount,
    rl.reason,
    rl.idempotency_key,
    rl.created_at
FROM reward_ledger rl
WHERE rl.user_id = $1
ORDER BY rl.created_at DESC
LIMIT 100;
