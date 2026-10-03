# Running the CS2 server

Keeping the game side alive: the plugin stack, what breaks when Valve ships an
update, and how to tell which link in the chain failed.

This is the part FLICKED does not own. FLICKED coordinates servers — it does not
install or provision them — so when a match will not start, the cause is usually
here rather than in the backend. Everything below has been hit in practice on a
real server.

---

## The chain

Four things have to load, in order, and each one fails differently:

```
  gameinfo.gi  ──►  Metamod  ──►  CounterStrikeSharp  ──►  MatchZy
      │                │                  │                   │
  a search path    a VDF loader      a .NET runtime      the match plugin
      │                │                  │                   │
  restored by      removed if the    breaks on CS2       refuses a second
  "validate"       folder is gone    updates (offsets)   match per process
```

Learning this chain is most of the job: **the symptom tells you which link
broke**, and each has a different fix.

| What the console says | Which link | Meaning |
|---|---|---|
| `Unknown command 'meta'` | gameinfo.gi | Metamod never loaded at all |
| `meta list` works, lists nothing useful | Metamod → CSS | the CSS loader is missing or failed |
| `Unknown command 'css_plugins'` | CounterStrikeSharp | CSS did not load — usually a CS2 update |
| `css_plugins list` works, no MatchZy | MatchZy | plugin missing, or failed to load |
| MatchZy listed as `LOADED` | — | the stack is healthy |

---

## Health check

Three commands in the server console, in this order. Stop at the first one that
fails — that is your link.

```
meta list              // is Metamod there, and does it list CounterStrikeSharp?
css_plugins list       // is MatchZy LOADED?
status                 // which map, how many players, is the port right
```

### What FLICKED's dashboard can and cannot tell you

**A server showing `Idle` does not mean the plugins work.** The pool's liveness
check is a bare RCON `echo`, which succeeds on a server with no plugins at all.
`Idle` proves three things only: the machine is up, the port is open, and the
RCON password is right.

Whether MatchZy exists is a question only the console answers. Worth remembering
before debugging the backend for an hour.

---

## Updating after a CS2 patch

CS2 updates itself for players. A dedicated server never does, and the symptom
is **"Your client is out of date"** when someone tries to connect.

### 1. Finish or cancel any live match

FLICKED needs nothing from you here. After about ninety seconds of failed RCON
pings the pool marks the server **Offline** and stops handing it matches; it
returns to **Idle** by itself once the server answers again.

### 2. Update

```powershell
& C:\steamcmd\steamcmd.exe +force_install_dir C:\cs2-server +login anonymous +app_update 730 validate +quit
```

```bash
# Linux
/opt/steamcmd/steamcmd.sh +force_install_dir /opt/cs2 +login anonymous +app_update 730 validate +quit
```

### 3. Put the Metamod search path back

**`validate` restores `gameinfo.gi` to stock, every time**, which removes the
line that loads Metamod. This is the single most common reason a server comes
back from an update with no plugins.

Check it before starting:

```powershell
Select-String -Path C:\cs2-server\game\csgo\gameinfo.gi -Pattern metamod
```

Nothing printed means the line is gone. Add it **above** `Game csgo`:

```
		SearchPaths
		{
			Game				csgo/addons/metamod
			Game				csgo
			Game				core
```

The order matters. Below `csgo` it will not load.

### 4. Check the stack came back

`meta list`, then `css_plugins list`. If `meta` works but `css_plugins` does not,
read on.

---

## CounterStrikeSharp and CS2 updates

**Expect CSS to break after a significant CS2 update.** It reads game memory
through offsets and schema definitions that shift whenever Valve changes the
binaries, so a CSS build is tied to a game build.

When that happens:

1. Check CounterStrikeSharp's releases for a build naming the current CS2 version.
2. If there is none yet, the schema fix usually lands in its repository before a
   release is cut — building from source is a legitimate option and has been
   necessary here before.
3. Until CSS loads, **no match can run**. The backend will keep trying, failing
   and releasing the server, which is correct behaviour but looks alarming.

There is normally a lag of hours to days after a major update. Nothing to fix on
the FLICKED side; wait for the plugin ecosystem.

> Careful with the addons folder. Deleting `addons/metamod` also removes the
> `.vdf` file that loads CounterStrikeSharp, so Metamod comes back and CSS does
> not. If you reinstall Metamod, reinstall CSS afterwards.

---

## What FLICKED needs from a server

Less than you would expect. **`server.cfg` needs no FLICKED-specific lines.**

- **RCON working**, with the password you gave the dashboard. That is the only
  channel FLICKED uses to control the server.
- **MatchZy loaded**, nothing more. FLICKED sends the match config URL, the
  authentication header and the event-reporting settings over RCON when it starts
  a match.

Two behaviours worth knowing, both verified against the plugin's source and
recorded in [MATCHZY.md](./MATCHZY.md):

- **MatchZy accepts one match per process.** It sets an internal flag when a
  config loads and never clears it, so a server that has hosted a match refuses
  every later one — keeping the old match and map while the backend believes a
  new one started. FLICKED therefore sends `css_plugins reload MatchZy` before
  every match. That is deliberate, not a workaround you should remove.
- **Events are never retried or deduplicated.** `series_end` can arrive twice or
  not at all, which is why the backend treats finishing a match as a claim on a
  row rather than a message it trusts.

### If you change the RCON password

Update it in the FLICKED dashboard as well. The stored copy is encrypted and
cannot be read back for comparison, so the only symptom of a mismatch is the
server dropping to **Offline** while being perfectly healthy.

---

## Errors seen in practice

| Error | Where to look |
|---|---|
| `Your client is out of date` on connect | the server is behind; update it |
| `Unknown command 'meta'` | the `gameinfo.gi` search path |
| `Unknown command 'css_plugins'` | CSS not loaded — usually a CS2 update |
| `FATAL ERROR: CAppSystemDict: Unable to create interface Source2ServerConfig001` | an invalid `gameinfo.gi` edit, or launching from the wrong directory. Compare the file against a stock copy before anything else |
| `Failed to initialize Steamworks SDK for gameserver` / `couldn't determine steam client install directory` | the server cannot find its Steam client files. Reinstall through SteamCMD into the same folder and launch from the server's own directory |
| Server window opens and closes instantly | run it from a console so the error survives, and on Windows check the **Visual C++ redistributable** is installed |
| MatchZy says a match is already running | the per-process flag; reload the plugin or restart the server |
| Match loads the wrong map | the server fetched a config it could not reach, or MatchZy was never reloaded. Check `Api:PublicUrl` is an address the *server* can resolve |

### A note on reading these

The backend logs its own view of the same events. When a match will not start,
compare the two: FLICKED's log says what it sent and whether RCON answered, and
the game console says what the server did with it. Most confusion comes from
looking at only one of them.

---

## Keeping it running

A console window dies when you log out of the machine.

**Windows** — register it as a service with [NSSM](https://nssm.cc), or create a
Task Scheduler task set to *Run whether user is logged on or not* with the
trigger *At startup*.

**Linux** — a systemd unit, the same shape as the one in
[DEPLOY-LINUX.md](./DEPLOY-LINUX.md).

Either way the server should come back by itself after a reboot, because it will
reboot eventually and you will not be watching.

---

## Worth keeping a copy of

These are the things an update or a reinstall destroys, and all of them are small:

- **the `gameinfo.gi` search path line** — restored to stock by every `validate`
- **`server.cfg`** and any other config you have edited
- **the `addons` folder**, or at least a note of which Metamod and CSS versions
  are installed
- **`cs2.bat` or your launch command**, including the GSLT token

A text file beside the server with the launch line and the version numbers saves
more time than it costs to write.

---

## Routine after every CS2 update

1. Update with SteamCMD
2. Re-add the Metamod line to `gameinfo.gi`
3. Start the server
4. `meta list` → `css_plugins list`
5. If CSS is broken, get a matching build before expecting matches to run
6. Confirm the dashboard shows the server **Idle** — then run one match and watch
   it load the map

Step 6 is the only one that proves the whole path. The first five prove the
server starts.
