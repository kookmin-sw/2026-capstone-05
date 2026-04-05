-- Auth-first sample queries (signup/login)

-- [A1] check duplicated email
SELECT 1
FROM users
WHERE email = $1
LIMIT 1;

-- [A2] signup
INSERT INTO users (email, password_hash, nickname, role, status)
VALUES ($1, $2, $3, 0, 1)
RETURNING id, email, nickname, role, created_at;

-- [A3] login lookup
SELECT id, email, password_hash, nickname, role, status
FROM users
WHERE email = $1
LIMIT 1;

-- [A4] update last login time
UPDATE users
SET last_login_at = now(),
    updated_at = now()
WHERE id = $1;

-- [A5] save refresh token (hashed)
INSERT INTO refresh_tokens (user_id, token_hash, expires_at)
VALUES ($1, $2, $3)
RETURNING id;

-- [A6] revoke refresh token
UPDATE refresh_tokens
SET revoked_at = now()
WHERE user_id = $1
  AND token_hash = $2
  AND revoked_at IS NULL;

-- [A7] validate refresh token
SELECT rt.id, rt.user_id, rt.expires_at, rt.revoked_at
FROM refresh_tokens rt
WHERE rt.token_hash = $1
LIMIT 1;
