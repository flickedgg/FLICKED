# Hosting FLICKED

How to build FLICKED and run it somewhere your friends can reach, so a group can
queue against each other instead of one person testing alone.

This walks through the setup FLICKED was built for: **one Windows VPS running the
CS2 server, the backend and the dashboard together**. Everything works the same
spread across several machines; the addresses just stop being the same one.

> **Read [Security](#security) before you invite anyone.** The setup here runs
> over plain HTTP, which is fine for testing among people you know and not fine
> for anything else. What that costs you is written down plainly rather than left
> for you to find out.

---

## What you need

| | |
|---|---|
| A Windows VPS | the one already running your CS2 server is fine |
| .NET 10 SDK | on your development machine only — the VPS needs nothing |
| Node.js LTS | on both, if you want the dashboard |
| A Steam Web API key | free, from [steamcommunity.com/dev/apikey](https://steamcommunity.com/dev/apikey) |
| Your SteamID64 | so you can reach the dashboard as an admin |

---

## The three addresses

Nearly every deployment problem is one of these being wrong, and each fails
quietly rather than loudly. Worth understanding before you start.

```
  a friend's PC                    your VPS                       Steam
  ─────────────                    ────────                       ─────
  launcher  ──── http ────►  Flicked.Api :5165  ──── RCON ────►  CS2 + MatchZy
     │                              │  ▲                              │
     │                        Postgres  └────── match events ─────────┘
     │
     └──── steam://connect ──────────────────────────────────►  CS2 server
```

| Setting | Who reads it | What it must be | If it is wrong |
|---|---|---|---|
| `Steam:PublicUrl` | a friend's **browser**, signing in | your VPS address | sign-in sends them back to their own PC |
| `Api:PublicUrl` | the **CS2 server**, fetching a match config | your VPS address | the server fetches nothing and stays on its old map, with no error anywhere |
| `FLICKED_API` | the **launcher**, compiled in when built | your VPS address | the launcher looks for FLICKED on the player's own machine |

Replace `<vps-address>` below with the address your VPS answers on.

---

## Part 1 — Build

All three are built on your development machine. Nothing is compiled on the VPS.

### The API

```
dotnet publish backend/Flicked.Api -c Release -r win-x64 --self-contained
```

Output: `backend/Flicked.Api/bin/Release/net10.0/win-x64/publish/`

`--self-contained` bundles the .NET runtime, so the VPS needs no SDK installed.
It is about 115 MB the first time. **Later updates are two files** — see
[Upgrading](#upgrading).

### The launcher

The API address is compiled in, so a build made on your machine points at your
machine unless you say otherwise:

```
set FLICKED_API=http://<vps-address>:5165
cd launcher
npm install
npm run tauri build
```

Installers land in `launcher/src-tauri/target/release/bundle/` — an `.exe` (NSIS)
and an `.msi`, the same app either way. Send your friends whichever you prefer.

Leaving `FLICKED_API` unset builds against `http://localhost:5165`, which is what
you want while developing.

> **Check the build before handing it out.** One variable feeds both the Rust
> side and the webview so they cannot disagree, but confirming costs nothing:
>
> ```
> findstr /C:"<vps-address>" launcher\src-tauri\target\release\launcher.exe
> ```

Your friends will see **"Windows protected your PC"** when they run it. That is
SmartScreen reacting to an unsigned installer, not to anything being wrong:
*More info* → *Run anyway*. Signing needs a paid certificate.

### The dashboard

```
cd dashboard
npm install
set NEXT_PUBLIC_API_URL=http://<vps-address>:5165
npx next build
```

The build produces a standalone server. Assemble what the VPS needs:

```
.next/standalone/*   →  the server and its dependencies
.next/static/*       →  into <deploy>/.next/static
public/*             →  into <deploy>/public
```

That folder runs with Node alone — no `npm install` on the VPS.

**The dashboard has to run on the VPS**, not on your PC. Its session cookie is
`SameSite=Lax`, so a browser will not send it from `localhost:3000` to an API on
another host: you would sign in successfully and then have every request
rejected. Same host, different port is fine — cookies ignore ports.

---

## Part 2 — Deploy

### 2.1 Postgres

Docker Desktop on a Windows VPS usually cannot get the nested virtualization it
needs, so install Postgres natively with the **EnterpriseDB installer**. It runs
as a service and starts with the machine.

Then, in **SQL Shell (psql)**:

```sql
CREATE USER flicked WITH PASSWORD 'pick-a-real-password';
CREATE DATABASE flicked OWNER flicked;
```

Create no tables. The API applies its own migrations on startup, so the first run
builds the schema and seeds the map pool.

### 2.2 Copy the API across

Put the published folder anywhere on the VPS — `C:\flicked\api` in these
examples. Adjust the paths if you choose somewhere else.

### 2.3 Configure it

Create a file called `.env` **next to `Flicked.Api.exe`**. It is read on startup
and belongs in no repository.

```ini
# The database you just created.
ConnectionStrings:Flicked=Host=localhost;Port=5432;Database=flicked;Username=flicked;Password=<db-password>

# This machine, as the outside world sees it. Never localhost: one of these is
# read by a browser on someone else's PC, the other by the CS2 server.
Steam:PublicUrl=http://<vps-address>:5165
Api:PublicUrl=http://<vps-address>:5165

# Where the dashboard runs. Needed for CORS and for the sign-in redirect.
Dashboard:Url=http://<vps-address>:3000

# Listen on every interface. The default is localhost only, which leaves the API
# invisible from outside however the firewall is set.
urls=http://0.0.0.0:5165

STEAM_API_KEY=<your-steam-web-api-key>

# Comma-separated SteamID64s allowed into the dashboard.
FLICKED_ADMIN_STEAM_IDS=<your-steamid64>

# Encryption keys for stored RCON passwords. Back this folder up.
DataProtection:KeyPath=C:\flicked\keys

# Optional, while testing: play with fewer people than a real match needs.
# Matchmaking:CompetitivePlayers=2
```

#### About `DataProtection:KeyPath`

This folder holds the keys that encrypt stored RCON passwords.

- **Back it up.** Lose it and every stored RCON password becomes unreadable, and
  every server has to be added again.
- **It does not travel between machines.** If you copy a development database to
  the VPS, re-enter each server's RCON password in the dashboard afterwards. The
  symptom otherwise is `Server X has an unreadable RCON password; set it again`.

### 2.4 Open the ports

In an **administrator PowerShell** on the VPS:

```powershell
New-NetFirewallRule -DisplayName "FLICKED API" -Direction Inbound -Protocol TCP -LocalPort 5165 -Action Allow
New-NetFirewallRule -DisplayName "FLICKED Dashboard" -Direction Inbound -Protocol TCP -LocalPort 3000 -Action Allow
```

Many providers have a second firewall in their web panel. If a port still looks
closed, check there.

### 2.5 Start the API

Run `Flicked.Api.exe`. The first start applies the migrations; watch that it gets
through them without an exception.

Test it **from your own PC**, not from the VPS — a service can answer itself
while being unreachable from outside:

```
curl http://<vps-address>:5165/api/news
```

### 2.6 Start the dashboard

```powershell
cd C:\flicked\dashboard
$env:PORT=3000; $env:HOSTNAME="0.0.0.0"; node server.js
```

`HOSTNAME=0.0.0.0` matters for the same reason `urls` did — Next binds to
localhost otherwise.

### 2.7 Add your CS2 server

Open `http://<vps-address>:3000`, sign in with Steam, and add the server with its
host, port and RCON password. FLICKED handles the rest:

- it reloads MatchZy before each match, clearing the flag that otherwise makes a
  server refuse every match after its first;
- it sends the config URL and event settings over RCON, so `server.cfg` needs no
  FLICKED-specific lines.

A server that answers the pool's RCON ping shows as **Idle** and can host
matches. One that does not is **Offline** and is skipped until it answers.

### Keeping it running

A console window dies when you log out of the VPS. Install [NSSM](https://nssm.cc)
and register it as a service:

```
nssm install FlickedApi C:\flicked\api\Flicked.Api.exe
nssm set FlickedApi AppDirectory C:\flicked\api
nssm start FlickedApi
```

---

## Upgrading

After the first deployment you rarely need the whole 115 MB again. Unless the
dependencies changed, a new backend build is **two files**:

1. `dotnet publish` as above, on your machine
2. stop the API on the VPS
3. copy `Flicked.Api.dll` and `Flicked.Api.pdb` over the old ones
4. start it — any new migrations apply on startup

**Back up the database first when an upgrade migrates data.** From PowerShell —
not psql, since `pg_dump` is a program rather than a psql command:

```powershell
$pg = Get-ChildItem "C:\Program Files\PostgreSQL\*\bin\pg_dump.exe" | Select-Object -First 1
& $pg.FullName -U postgres -d flicked -f C:\backup.sql
```

**Update the API before handing out a launcher built against it.** The two talk
over endpoints that change together, so a launcher from one side of an update and
an API from the other will not agree.

---

## Security

FLICKED is a hobby project you host yourself. This section is what that honestly
means, so you can decide what you are comfortable with.

### This setup runs over plain HTTP

Everything above uses `http://`, and **that is not safe for anything beyond a
test among people you know**. It is written this way deliberately: it gets a
development setup working without a domain or certificates. It is not production
ready and should not be treated as though it were.

With no TLS:

- **Session tokens cross the internet in the clear.** Anyone positioned between a
  player and your VPS — the same café Wi-Fi, a compromised router, an ISP — can
  read a token and act as that player until it expires.
- **Dashboard cookies cross in the clear too**, and yours is an admin session.
  Whoever captures it can add, edit and remove servers.
- **RCON passwords are typed into an HTTP page.** They are encrypted once they
  reach the database, but the form carrying them is not protected in transit.
- **Nothing proves you are talking to your own server.** Without a certificate,
  neither a launcher nor a browser can tell your API from something answering in
  its place.

**Before anyone outside your circle uses this**, put a domain in front with
[Caddy](https://caddyserver.com), which obtains a certificate automatically. The
three addresses become `https://` and the launcher needs rebuilding. It is about
an hour, and it removes every point above.

### What FLICKED does protect

So you know where the line falls:

- **Session tokens are stored hashed.** The database holds a SHA-256 of a token
  rather than the token, so a database dump does not hand over live sessions.
- **RCON passwords are encrypted at rest**, with the keys kept outside the
  database in `DataProtection:KeyPath`. A stolen database alone does not yield
  them.
- **FLICKED never sees a Steam password.** Sign-in is Steam's own OpenID flow:
  you authenticate with Steam, and FLICKED is told who you are.
- **Admin actions are checked on the server** against `FLICKED_ADMIN_STEAM_IDS`.
  Hidden buttons are a courtesy to the user, not a control.

### What must never be committed

`.env` is in `.gitignore` and belongs there. If one of these reaches a commit,
treat it as public from the moment it is pushed — rewriting history does not
recall clones, forks, or anyone who already fetched:

| Leaked | What to do |
|---|---|
| `STEAM_API_KEY` | revoke and regenerate it at Steam |
| Database password | change it in Postgres and in `.env` |
| RCON passwords | change them on each CS2 server, then re-enter in the dashboard |
| `DataProtection` keys | rotate the folder, then re-enter every RCON password |

A public repository also publishes things that are not secrets but do describe
your setup: addresses, open ports, which services run where. None of that is a
credential, and a port scan finds most of it anyway — but a deployment guide
written around a live host collects it in one convenient place, which is why this
one uses `<vps-address>`.

### Your server address is not a secret

Worth saying plainly, because it surprises people: a CS2 server's address is
published to every player who connects, appears in their console, and is compiled
into the launcher you hand out. It cannot be kept private while people play on
it. What matters is that the credentials above stay out of the repository.

---

## When something does not work

Every one of these has happened during a real setup.

| Symptom | Cause |
|---|---|
| API unreachable from outside, firewall looks open | `urls=http://0.0.0.0:5165` missing — it bound to localhost |
| Server never loads the voted map, its console shows nothing | `Api:PublicUrl` is localhost or unreachable, so the server fetched its config from itself |
| Sign-in sends a friend back to their own PC | `Steam:PublicUrl` is localhost |
| News and leaderboard empty in the launcher, everything else fine | the launcher was built without `FLICKED_API` |
| Dashboard signs in, then every request fails | the dashboard is not on the same host as the API (`SameSite=Lax`) |
| `Server X has an unreadable RCON password` | the database moved between machines without its `DataProtection` keys |
| Match timers start part-way through, or sit at zero | the VPS clock or the player's is wrong; sync both |
| `invalid command \...` | you are in psql and typed a program name; programs belong in PowerShell |
| `The ampersand (&) character is not allowed` | two PowerShell commands on one line — separate them with `;` |
| `-U is not recognized` after a quoted path | PowerShell needs `& ` in front of a quoted path to run it |
