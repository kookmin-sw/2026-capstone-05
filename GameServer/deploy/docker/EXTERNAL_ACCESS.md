# External Access Guide

This Docker setup publishes only the API to other machines by default.

## 1. Start the server

From the project root:

```powershell
cd GameServer/deploy/docker
Copy-Item .env.example .env -ErrorAction SilentlyContinue
docker compose up -d --build
```

The API is published as:

```text
0.0.0.0:8080 -> gameserver-api:8080
```

Other people on the same network should connect to:

```text
http://YOUR_PC_IP:8080
```

You can check your PC's IPv4 address with:

```powershell
Get-NetIPAddress -AddressFamily IPv4 |
  Where-Object { $_.IPAddress -notlike '127.*' -and $_.PrefixOrigin -ne 'WellKnown' } |
  Select-Object IPAddress, InterfaceAlias
```

Example:

```text
http://192.168.0.23:8080
```

## Outside the same router

If the client is not on the same router or Wi-Fi, `http://192.168.x.x:8080` will not work.
Use one of these two options:

### Option A: Router port forwarding

In your router admin page, forward:

```text
External TCP 8080 -> 192.168.1.8 TCP 8080
```

Then other people connect to:

```text
http://YOUR_PUBLIC_IP:8080
```

Your current public IP can be checked with:

```powershell
Invoke-RestMethod https://api.ipify.org
```

If the router WAN IP is different from this public IP, your ISP is likely using CGNAT. In that case, router port forwarding will not work unless the ISP gives you a real public IPv4 address.

### Option B: Cloudflare temporary tunnel

This works even when you cannot configure router port forwarding.

```powershell
cd GameServer/deploy/docker
docker compose -f docker-compose.yml -f docker-compose.tunnel.yml up -d --build
docker compose -f docker-compose.yml -f docker-compose.tunnel.yml logs -f cloudflared
```

Find the generated `https://...trycloudflare.com` URL in the logs, then write it to `auth-api-base-url.txt`.

From the project root, this command starts Docker, extracts the newest Cloudflare URL, writes it to the editor/runtime config file, and prints the URL:

```powershell
docker compose -f GameServer\deploy\docker\docker-compose.yml -f GameServer\deploy\docker\docker-compose.tunnel.yml up -d --build
Start-Sleep -Seconds 8
.\GameServer\deploy\docker\update-auth-api-base-url.ps1
```

Cloudflare quick tunnel URLs are temporary and may change after restart.

## 2. Open Windows firewall

Run PowerShell as Administrator and allow TCP port 8080:

```powershell
New-NetFirewallRule -DisplayName "NUNBORA GameServer API 8080" -Direction Inbound -Protocol TCP -LocalPort 8080 -Action Allow
```

Without this firewall rule, Docker can be running correctly but other PCs may still fail to connect.

## 3. Point the Unity client at the server

In the Unity Inspector, set `MainMenuAuthController.authApiBaseUrl` to:

```text
http://YOUR_PC_IP:8080
```

For a built client, place a text file named `auth-api-base-url.txt` next to the built executable:

```text
https://YOUR-TUNNEL.trycloudflare.com
```

To copy the current project-root config file next to a built executable:

```powershell
Copy-Item -Force .\auth-api-base-url.txt "C:\Path\To\Build\auth-api-base-url.txt"
```

For a same-network-only test, use your PC IP instead of the Cloudflare URL:

```text
http://YOUR_PC_IP:8080
```

The client reads one address file: `auth-api-base-url.txt` next to the executable. In the Unity editor, the same file is read from the project root. If the file is missing, the Unity Inspector value is used as a fallback.

## 4. Keep PostgreSQL private

PostgreSQL and pgAdmin are bound to `127.0.0.1` by default:

```env
POSTGRES_BIND_ADDRESS=127.0.0.1
POSTGRES_HOST_PORT=15432
PGADMIN_BIND_ADDRESS=127.0.0.1
PGADMIN_HOST_PORT=15050
```

That means other players connect only to the API, not directly to the database. This is the safer setup for login.

## 5. Optional port changes

Edit `GameServer/deploy/docker/.env`:

```env
API_BIND_ADDRESS=0.0.0.0
API_HOST_PORT=8080
```

Then restart:

```powershell
docker compose up -d
```
