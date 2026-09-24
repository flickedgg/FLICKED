# Hosting FLICKED

How to put FLICKED on a machine other people can reach, so a group of friends can
queue against each other instead of one person testing alone.

This walks through the setup FLICKED was built for: **one Windows VPS running both
the CS2 server and the backend**. Everything here works the same on a bigger
setup; the addresses just stop being the same machine.

```
  a friend's PC                    your VPS                       Steam
  ─────────────                    ────────                       ─────
  launcher  ──── http ────►  Flicked.Api :5165  ──── RCON ────►  CS2 + MatchZy
     │                              │  ▲                              │
     │                        Postgres  └────── match events ─────────┘
     │
     └──── steam://connect ──────────────────────────────────►  CS2 server
```

Three addresses have to be right, and they are the whole of what usually goes
wrong:

| Setting | Who reads it | What it must be |
|---|---|---|
| `Steam:PublicUrl` | a friend's **browser**, during sign-in | the VPS address |
| `Api:PublicUrl` | the **CS2 server**, fetching a match config | the VPS address |
| `FLICKED_API` | the **launcher**, compiled in | the VPS address |

A wrong one of these fails quietly rather than loudly. That is worth knowing in
advance: a bad `Api:PublicUrl` means the server fetches nothing and sits on its
old map, with no error anywhere.

---

## 1. Postgres

Docker Desktop on a Windows VPS usually cannot get the nested virtualization it
needs for WSL2, so install Postgres natively:

1. Download the **EnterpriseDB Postgres installer** and run it. It installs a
   Windows service that starts with the machine.
2. During setup, set a password for the `postgres` superuser.
3. Create FLICKED's database and user (pgAdmin comes with the installer, or use
   `psql`):

```sql
CREATE USER flicked WITH PASSWORD 'pick-a-real-password';
CREATE DATABASE flicked OWNER flicked;
```

You do not need to create any tables. The API applies its own migrations on
startup (`Database:AutoMigrate`, on by default), so the first run builds the
schema and seeds the map pool.

---

## 2. Configuration

Nothing secret belongs in `appsettings.json` — it is committed. Set these as
**system environment variables** on the VPS instead, where `__` stands for the
`:` in a setting name:

```
ConnectionStrings__Flicked = Host=localhost;Port=5432;Database=flicked;Username=flicked;Password=pick-a-real-password
Steam__PublicUrl           = http://77.83.242.101:5165
Api__PublicUrl             = http://77.83.242.101:5165
Steam__ApiKey              = your Steam Web API key
Dashboard__Url             = http://77.83.242.101:3000
DataProtection__KeyPath    = C:\flicked\keys
ASPNETCORE_URLS            = http://0.0.0.0:5165
```

Substitute your own VPS address for `77.83.242.101` throughout.

`ASPNETCORE_URLS` matters: the default binds to localhost only, and the API would
be invisible from outside no matter what the firewall says.

### About `DataProtection__KeyPath`

This folder holds the keys that encrypt stored RCON passwords. Two consequences:

- **Back it up.** Lose it and every stored RCON password becomes unreadable, and
  every server has to be added again.
- **It does not travel.** Keys are tied to the machine that made them, so RCON
  passwords encrypted on your PC cannot be decrypted on the VPS. If you copy your
  development database over, re-enter each server's RCON password in the
  dashboard afterwards. The symptom otherwise is
  `Server X has an unreadable RCON password; set it again` in the log.

---

## 3. Publish and run the API

On your development machine:

```
dotnet publish backend/Flicked.Api -c Release -r win-x64 --self-contained
```

`--self-contained` bundles the .NET runtime, so the VPS needs no SDK installed.
Copy `bin/Release/net10.0/win-x64/publish/` to the VPS, say to `C:\flicked\api`,
and run `Flicked.Api.exe`.

Open the port, in an **administrator** PowerShell on the VPS:

```powershell
New-NetFirewallRule -DisplayName "FLICKED API" -Direction Inbound -Protocol TCP -LocalPort 5165 -Action Allow
```

Some providers also have their own firewall in a web panel, separate from
Windows' — check there if the port still looks closed.

Check it from your own PC, not from the VPS (a service can answer itself while
being unreachable from outside):

```
curl http://77.83.242.101:5165/api/news
```

### Keeping it running

`Flicked.Api.exe` in a console window dies when you log out of the VPS. To keep
it up, install [NSSM](https://nssm.cc) and register it as a service:

```
nssm install FlickedApi C:\flicked\api\Flicked.Api.exe
nssm set FlickedApi AppDirectory C:\flicked\api
nssm start FlickedApi
```

It then starts with the machine and restarts if it crashes.

---

## 4. Build the launcher for your friends

The API address is compiled into the launcher, so a build made on your machine
points at your machine. Set the variable before building:

```
set FLICKED_API=http://77.83.242.101:5165
cd launcher
npm run tauri build
```

The installer lands in `launcher/src-tauri/target/release/bundle/`. Send that to
your friends. Leaving `FLICKED_API` unset keeps the old behaviour — a build
pointing at `http://localhost:5165` — which is what you want while developing.

Each friend signs in through Steam in their own browser; the sign-in hands the
token back to their own launcher on `127.0.0.1`, so nothing about it depends on
where they are.

---

## 5. Add the CS2 server

Open the dashboard, sign in as an admin, and add the server with its host, port
and RCON password. FLICKED does the rest of the setup itself:

- it reloads MatchZy before each match, which clears the flag that otherwise
  makes a server refuse every match after its first;
- it passes the config URL and the event-reporting settings over RCON, so
  `server.cfg` needs no FLICKED-specific lines.

The pool pings each server over RCON; a server that answers shows as **Idle** and
is available to matches. One that does not is marked **Offline** and skipped
until it answers again.

---

## Checklist before inviting anyone

- [ ] `curl http://<vps>:5165/api/news` returns JSON **from another machine**
- [ ] The dashboard shows your CS2 server as **Idle**
- [ ] A friend's launcher signs in with Steam and shows their real avatar
- [ ] A match forms, the server loads **the map that won the vote**, and everyone
      is connected by Steam without typing an address

## Known limits of this setup

**It is http, not https.** Sign-in tokens and session cookies cross the internet
in the clear, and anyone on the same network as one of your friends can read
them. That is an acceptable trade for a test among people you know, and not
acceptable for a public service. Putting [Caddy](https://caddyserver.com) in
front with a real domain gets a certificate automatically; the three addresses
above then become `https://` and the launcher needs rebuilding.

**CS2 and the backend share a machine.** They compete for CPU, and a busy match
can slow both. It is the cheapest way to try this, not the way to run it.
