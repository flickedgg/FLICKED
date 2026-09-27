# Hosting FLICKED on Linux

The same deployment as [DEPLOY.md](./DEPLOY.md), on a Linux server. Read that one
for what the three addresses are and why they matter — this covers only what is
different, and it is different in ways that matter: systemd instead of a console
window, a non-root user, a package manager that has Postgres in it, and HTTPS
that takes about five minutes instead of an afternoon.

Examples use Debian or Ubuntu. On Fedora or Alma, swap `apt` for `dnf` and
`ufw` for `firewall-cmd`.

> **Unlike the Windows guide, this one puts HTTPS in the main path.** On Linux it
> is genuinely easy, and everything in that guide's security section stops
> applying once you have it. If you would rather run plain HTTP for a quick test,
> [skip the proxy](#option-a-plain-http-testing-only) — but read what it costs
> first.

---

## What runs where

```
              ┌─────────────────── your Linux server ───────────────────┐
  players ───►│  Caddy :443  ──►  Flicked.Api :5165                     │
              │       │                  │                              │
              │       └──────────►  Dashboard :3000                     │
              │                          │                              │
              │                     Postgres :5432  (localhost only)    │
              └────────────────────────────────────────────────────────┘
                                  │
  CS2 + MatchZy  ◄──── RCON ──────┘        (same box or another one)
```

Only Caddy faces the internet. The API, the dashboard and Postgres listen on
localhost, which means a firewall mistake cannot expose them.

---

## 1. A user for it

Never run this as root. It has no reason to write outside its own directory.

```bash
sudo useradd --system --home /opt/flicked --shell /usr/sbin/nologin flicked
sudo mkdir -p /opt/flicked/{api,dashboard,keys}
sudo chown -R flicked:flicked /opt/flicked
```

---

## 2. Postgres

```bash
sudo apt update
sudo apt install -y postgresql
sudo -u postgres psql
```

```sql
CREATE USER flicked WITH PASSWORD 'pick-a-real-password';
CREATE DATABASE flicked OWNER flicked;
\q
```

Create no tables: the API migrates itself on startup.

Postgres on Debian and Ubuntu listens on localhost only by default, which is what
you want — nothing outside the machine should reach 5432.

Docker works properly here, unlike on a Windows VPS, so `docker compose up -d`
with the repository's `docker-compose.yml` is a fine alternative if you prefer it.

---

## 3. Build and upload

The **API and dashboard** are built on your development machine, whatever it
runs, and copied across.

```bash
# API, targeting Linux
dotnet publish backend/Flicked.Api -c Release -r linux-x64 --self-contained

# dashboard
cd dashboard
npm install
NEXT_PUBLIC_API_URL=https://flicked.example.com npx next build
```

Copy them over:

```bash
rsync -a backend/Flicked.Api/bin/Release/net10.0/linux-x64/publish/ \
      you@server:/tmp/api/
rsync -a dashboard/.next/standalone/ you@server:/tmp/dashboard/
rsync -a dashboard/.next/static/    you@server:/tmp/dashboard/.next/static/
rsync -a dashboard/public/          you@server:/tmp/dashboard/public/
```

Then on the server:

```bash
sudo cp -r /tmp/api/.       /opt/flicked/api/
sudo cp -r /tmp/dashboard/. /opt/flicked/dashboard/
sudo chown -R flicked:flicked /opt/flicked
sudo chmod +x /opt/flicked/api/Flicked.Api
```

That last line is not optional and is easy to forget: the published binary
arrives without the execute bit, and systemd's complaint about it
(`Permission denied`) does not say why.

### The launcher is still built on Windows

`npm run tauri build` produces an installer for the machine it runs on. Your
players are on Windows, so the launcher is built there — see
[DEPLOY.md](./DEPLOY.md#the-launcher). Nothing about hosting the backend on Linux
changes that, and `FLICKED_API` must still point at this server:

```
set FLICKED_API=https://flicked.example.com
```

### What Linux needs that Windows did not

- **ICU.** .NET needs `libicu` for anything culture-aware. Debian and Ubuntu
  server images usually have it; minimal containers do not, and the failure is an
  exception on startup rather than a missing-package message.
  `sudo apt install -y libicu-dev` if you hit it.
- **Node**, for the dashboard: `curl -fsSL https://deb.nodesource.com/setup_lts.x | sudo -E bash - && sudo apt install -y nodejs`.

---

## 4. Configure

`/opt/flicked/api/.env`, next to the binary:

```ini
ConnectionStrings:Flicked=Host=localhost;Port=5432;Database=flicked;Username=flicked;Password=<db-password>

# Behind a proxy these are the public https names, not the local port.
Steam:PublicUrl=https://flicked.example.com
Api:PublicUrl=https://flicked.example.com
Dashboard:Url=https://dash.flicked.example.com

# Localhost only: Caddy is the one thing listening publicly.
urls=http://127.0.0.1:5165

STEAM_API_KEY=<your-steam-web-api-key>
FLICKED_ADMIN_STEAM_IDS=<your-steamid64>
DataProtection:KeyPath=/opt/flicked/keys
```

This file holds your database password and Steam key, so do not leave it
world-readable:

```bash
sudo chown flicked:flicked /opt/flicked/api/.env
sudo chmod 600 /opt/flicked/api/.env
sudo chmod 700 /opt/flicked/keys
```

`/opt/flicked/keys` holds the keys that encrypt stored RCON passwords. **Back it
up**, and know that it does not move between machines — a database restored onto
a different server needs every server's RCON password entered again.

---

## 5. Run both as services

`/etc/systemd/system/flicked-api.service`:

```ini
[Unit]
Description=FLICKED API
After=network-online.target postgresql.service
Wants=network-online.target

[Service]
User=flicked
Group=flicked
WorkingDirectory=/opt/flicked/api
ExecStart=/opt/flicked/api/Flicked.Api
Restart=always
RestartSec=5

# It needs to read its own directory and write nothing else.
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/opt/flicked/keys

[Install]
WantedBy=multi-user.target
```

`/etc/systemd/system/flicked-dashboard.service`:

```ini
[Unit]
Description=FLICKED dashboard
After=network-online.target flicked-api.service

[Service]
User=flicked
Group=flicked
WorkingDirectory=/opt/flicked/dashboard
Environment=PORT=3000
Environment=HOSTNAME=127.0.0.1
ExecStart=/usr/bin/node server.js
Restart=always
RestartSec=5
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now flicked-api flicked-dashboard
sudo systemctl status flicked-api --no-pager
```

The first start applies the migrations. Watch them:

```bash
journalctl -u flicked-api -f
```

`ProtectSystem=strict` makes the whole filesystem read-only except what you name,
which is why `ReadWritePaths=/opt/flicked/keys` is there — the key ring is the one
thing the API writes. If you move `DataProtection:KeyPath`, move that line too, or
the API starts and then fails the first time it encrypts anything.

---

## 6. HTTPS

### Option A — plain HTTP (testing only)

Set `urls=http://0.0.0.0:5165`, open the ports, and stop here:

```bash
sudo ufw allow 5165/tcp
sudo ufw allow 3000/tcp
```

You are then running the setup the Windows guide describes, with the costs listed
in its [security section](./DEPLOY.md#security): session tokens and your admin
cookie crossing the internet in the clear, RCON passwords typed into an
unprotected form, and no way for a launcher to tell your API from something
answering in its place. Fine among friends for an evening. Not otherwise.

### Option B — Caddy (recommended)

Two DNS records pointing at the server, then:

```bash
sudo apt install -y caddy
```

`/etc/caddy/Caddyfile`:

```caddyfile
flicked.example.com {
    reverse_proxy 127.0.0.1:5165
}

dash.flicked.example.com {
    reverse_proxy 127.0.0.1:3000
}
```

```bash
sudo systemctl reload caddy
sudo ufw allow 80/tcp && sudo ufw allow 443/tcp
```

Caddy obtains and renews certificates by itself. Nothing else needs configuring,
and the API keeps listening on localhost.

Two things to know:

- **The dashboard's session cookie is marked `Secure` only when the request looks
  secure**, and behind a proxy it does not: the API is reached over http on
  loopback. The API therefore reads `X-Forwarded-Proto`, but **only from a proxy
  on this machine** — those headers are trivial to forge, so they are ignored from
  anywhere else. Nothing to configure if Caddy and the API share a host, as
  above. If your proxy is on a *different* machine, add its address to
  `KnownProxies` in `Program.cs` or the cookie silently loses its `Secure` flag.
- **Rebuild the launcher** once the addresses are `https://`. The address is
  compiled in, so an old build keeps calling the old one.

---

## 7. The CS2 server

MatchZy, Metamod and CounterStrikeSharp all run on Linux, and the backend does not
care which the server is:

```bash
sudo -u steam /opt/steamcmd/steamcmd.sh +force_install_dir /opt/cs2 \
     +login anonymous +app_update 730 validate +quit
```

Two Linux-specific notes:

- **`gameinfo.gi` is restored by `validate`**, exactly as on Windows. Re-add the
  Metamod search path after every update or the plugins silently stop loading.
- **The RCON port must be reachable from the API.** Same box means
  `127.0.0.1:27015` and no firewall rule; another box means opening 27015 to this
  server's address only, not to the world.

Add the server in the dashboard with its host, port and RCON password. FLICKED
reloads MatchZy before each match and sends everything else over RCON, so
`server.cfg` needs no FLICKED-specific lines.

---

## 8. Keep the clock right

Match timers are drawn from timestamps, so a drifting clock shows a countdown
that starts part-way through or sits at zero:

```bash
timedatectl status          # want: "System clock synchronized: yes"
sudo timedatectl set-ntp true
```

---

## Upgrading

```bash
# on your machine
dotnet publish backend/Flicked.Api -c Release -r linux-x64 --self-contained
rsync -a backend/Flicked.Api/bin/Release/net10.0/linux-x64/publish/ you@server:/tmp/api/

# on the server
sudo -u postgres pg_dump flicked > ~/flicked-$(date +%F).sql   # before a data migration
sudo systemctl stop flicked-api
sudo cp -r /tmp/api/. /opt/flicked/api/
sudo chown -R flicked:flicked /opt/flicked/api
sudo chmod +x /opt/flicked/api/Flicked.Api
sudo systemctl start flicked-api
journalctl -u flicked-api -n 50 --no-pager
```

Unless the dependencies changed, only `Flicked.Api.dll` and `Flicked.Api.pdb`
actually differ, so copying those two is enough.

**Update the API before handing out a launcher built against it** — the two talk
over endpoints that change together.

---

## When something does not work

Everything in [DEPLOY.md's troubleshooting table](./DEPLOY.md#when-something-does-not-work)
still applies. These are the Linux-only ones.

| Symptom | Cause |
|---|---|
| `Permission denied` starting the service | the published binary has no execute bit — `chmod +x` |
| Service starts, then fails encrypting something | `ProtectSystem=strict` without `ReadWritePaths` for the key folder |
| Culture or globalization exception on startup | `libicu` missing, typical of minimal images |
| Reachable on `:5165` but not through Caddy | the API is bound to `127.0.0.1` and Caddy is proxying a different port |
| Dashboard cookie rejected behind the proxy | dashboard and API are on hostnames that are not the same site — cookies are `SameSite=Lax` |
| `FATAL: password authentication failed` | the password in `.env` does not match the `flicked` role |
| Everything works until reboot | the services were never `systemctl enable`d |
