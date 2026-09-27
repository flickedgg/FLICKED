# Roadmap

Where FLICKED is going, and why it looks the way it does.

This is a solo project. The plan is shaped around that: one
working thing at a time, and no promises that need a team to keep.

## What this project has to work with

Worth stating plainly, because it explains most of the decisions below:

- **One person**, part time, learning some parts of the stack while building them.
- **One VPS.** Not a fleet, not a cloud budget. There is no second machine to test failover
  on, and no ten CS2 servers sitting idle waiting to be matched into.
- **No money for infrastructure.** Anything that only works when you can afford to leave
  servers running is out of reach, however good an idea it is.

So a system that provisions servers on demand could not be built here, and could not be
tested here either. Code that has never run against the thing it manages is not finished
code, it is a guess. Building the server pool instead means the part FLICKED owns can be
tested properly: rows in a table, claims, leases and releases, all verifiable on one machine,
with a single real CS2 server proving the end to end path.

Where real capacity is genuinely needed, it comes from the people who already have it.
Server owners run CS2 servers anyway. FLICKED coordinates them.

---

## Where it is, 27 September 2026

The whole path works end to end, on a real server, with real people: two players
signed in through Steam, queued, accepted, voted a map, and CS2 connected them
itself. The match finished and the result was written to Postgres.

Shipped and used:

- **Real data.** News, leaderboard and match history come from PostgreSQL. EF Core
  migrations apply on startup, so a self-hoster upgrades by replacing a binary.
- **Accounts.** Steam sign-in, with the session token held in Rust and never handed
  to the webview. A player's rating, record and history are their own.
- **A server pool.** Servers are registered in the dashboard, RCON passwords
  encrypted at rest. A match claims an idle server with `FOR UPDATE SKIP LOCKED`,
  holds it on a lease, and releases it when the match ends. A server that stops
  answering RCON is marked offline and skipped until it answers again.
- **Matches.** The matchmaker forms them on rating windows that widen with waiting
  time, runs accept and map-vote phases, tells the server what to load over RCON,
  and waits for the map to actually change before anyone is given the address.
  MatchZy needs no FLICKED-specific lines in `server.cfg`.
- **Results and rating.** The server reports back over an authenticated endpoint.
  Ratings, records and per-match figures update once per match — see
  [RATING.md](./RATING.md).
- **Parties.** Queueing with friends, drafted onto the same team — see
  [PARTY.md](./PARTY.md).
- **A dashboard** for admins, and a **deployment guide** for
  [Windows](./DEPLOY.md) and [Linux](./DEPLOY-LINUX.md).

Shipped but not yet proven in play, which is not the same thing:

- **Parties** and **rating** are merged, tested and deployed, but no party has
  formed between two real launchers and no rating has moved in a live match yet.

## Known gaps

Things that are wrong or missing today, written down so they are not rediscovered:

- **Declining a match does not requeue anyone.** The comment promises it; only the
  twenty-second timeout path actually does it. With parties it now also costs a
  party its place in the queue.
- **A cancelled Wingman match requeues its players into Competitive.** The mode is
  not stored on the match, so there is nothing to requeue them into correctly.
- **Wingman is disabled in the launcher.** The backend supports it; there is no
  Wingman server in the pool to send it to.
- **No live updates.** Friends, parties and the queue are polled. It works, and a
  push channel would replace the transport without changing what the screens read.
- **No presence.** Whether a friend is online, in menus or in a match is runtime
  state, and belongs with that push channel.
- **Demos are recorded but not linked** to a match anywhere a player can reach.
- **A stuck match needs SQL.** There is no admin action to cancel one, and a
  player can leave the queue but not a match.
- **Seeded demo players** share the leaderboard with real ones on any instance
  that ran the migrations.
- **The live instance runs over plain HTTP**, which is a deliberate choice for
  testing among friends and is not safe for anything else. The reasoning and the
  cost are in [DEPLOY.md](./DEPLOY.md#security).

## Planned

What the next updates are aimed at, roughly in order.

**A FLICKED plugin, built on MatchZy.** The decision below says MatchZy first and
a custom plugin later; this is the later, and "custom" turns out not to mean
"from scratch".

MatchZy is the reason matches work at all. Knife rounds, pauses, demo recording,
the veto flow and a working event pipeline are months of careful work that this
project did not have to do, and it is maintained properly — the behaviour
FLICKED depends on was verified against a real server and documented in
[MATCHZY.md](./MATCHZY.md) rather than guessed at.

What FLICKED wants is not a better plugin; it is a plugin that answers to one
backend. A general-purpose match plugin has to serve everyone's setup, which
means its event format is a compromise and its lifecycle assumes a human admin is
present. FLICKED is neither: it drives the server over RCON, has its own
database, and wants things a shared plugin has no reason to provide — per-match
admin control from the dashboard, moderation while a match is live, and stats
shaped the way FLICKED stores them instead of translated on arrival.

**MatchZy is MIT licensed**, so the sensible route is to build on it rather than
start again: fork it, keep what works, and change the parts that only matter to
FLICKED. That keeps the years of edge cases already handled and turns this from a
rewrite into a focused change — and whatever comes of it carries MatchZy's
copyright notice and credit, as the licence requires and as is plainly deserved.

Nothing starts until the current arrangement is genuinely the limiting factor.

**Bans.** Admins need to be able to remove someone from the servers and the
launcher. Two stages: first a ban on the Steam account, which is simple, honest
and enough for the usual case; later a hardware ban, so someone who returns on a
fresh Steam account is refused as well.

> This is moderation, not anti-cheat. It removes people an admin has decided to
> remove. It does not detect anything, and nothing here changes the
> [no anti-cheat](#2026-09-20--no-anti-cheat) decision.

Worth being clear-eyed about the second stage before building it: a hardware ban
means the launcher collecting identifiers from the player's machine, which is
something players deserve to be told about plainly rather than discover, and which
a determined person can change. It raises the cost of coming back; it does not
prevent it.

**Party codes.** A short code the leader can copy, share and let someone join
with, for the times an invite is the wrong shape — a Discord call, a friend not on
your friends list yet. The groundwork is done: parties, invites and membership are
real, and a code is one column and one endpoint on top of them. The placeholder
button that used to hint at this was removed when parties became real, because a
button that does nothing is worse than no button.

## Not planned

- **Anti-cheat.** See the decision below.
- **Creating or hosting CS2 servers.** FLICKED coordinates servers, it does not
  provision them. Not ruled out forever, but not planned. See "Maybe later" in the
  decision below.
- **Mobile apps, a web launcher, tournaments and brackets.** Maybe one day. Not
  promised.

---

# Decisions

Why things are the way they are, newest first. Each entry is what was decided, why, and
what it costs.

## 2026-09-20 · Server owners bring the servers

**Decided:** FLICKED keeps a pool of CS2 servers that owners register themselves. It claims
one for a match and releases it afterwards. It does not create, host or destroy servers.

**Why:** the README used to promise "dedicated-server orchestration". That is an
infrastructure product on its own: creating containers or machines on demand, allocating
ports, planning capacity, cleaning up dead servers. None of it is about CS2, and none of it
is what makes FLICKED interesting. Matchmaking, ratings and the launcher are.

It is also not testable here. Orchestration is judged on what happens when a machine dies
mid match, when a provider is slow to hand one over, or when ten matches start at once. With
one VPS and no budget for more, none of that could be tried, only hoped for. The server pool
can be tested in full on one machine.

**What it costs:** self-hosters have to run their own CS2 servers. That is normal for
community tooling, and most people setting up a platform like this already have one.

**Why it is not a dead end:** the backend always asks the same question, "give me a server
for this match". Today the answer comes from a table of registered servers. If automatic
provisioning is ever worth building, it becomes another way a row lands in that table, and
matchmaking never notices the difference.

**Maybe later:** creating and destroying servers on demand is still the better experience,
and the door is deliberately left open for it. It comes back when three things are true at
once: players are waiting in queue with no idle server to give them, there is money to keep
machines running, and there is somewhere to test it properly.
Until then it stays out of the README, because a promise nobody can keep
is worth less than a smaller thing that works. This is a note, not a plan.

## 2026-09-20 · MatchZy first, a custom plugin later

**Decided:** use MatchZy (built on CounterStrikeSharp) to run matches on the server, instead
of writing a plugin first.

**Why:** MatchZy already does match configs, knife rounds, pauses, demo recording and event
webhooks. Writing that again would take months and produce something worse. The backend only
needs to serve a match config and accept the events that come back, which is two endpoints.

**What it costs:** a dependency on MatchZy's config and event format. It is open source and
written in C#, so it can be read, and forked if it ever stops fitting.

## 2026-09-20 · No anti-cheat

**Decided:** drop the planned C++ anti-cheat. It is not on the roadmap.

**Why:** anti-cheat is a specialist field, it is adversarial, and doing it badly is worse
than not doing it, because it promises players a protection that isn't real. Leaving a
promise in the README that will not be kept is the same problem.

**What it costs:** cheating is left to the server owner, using community anti-cheat on their
own servers.

**Maybe later:** an anti-cheat worth trusting needs a team, a research budget and years of
it, because the people it is up against work full time on getting past it. One person
part time cannot keep up with that, and an anti-cheat that cannot keep up is just a claim on
a README. If FLICKED ever grows enough to have people and funding behind it, this is worth
reopening. Until then, honest silence beats a badge nobody should rely on.

## 2026-09-19 · PostgreSQL in Docker, not XAMPP's MySQL

**Decided:** PostgreSQL, run through `docker compose`.

**Why:** the leaderboard is a ranking query, and Postgres has the right tools for it
(window functions, and `FOR UPDATE SKIP LOCKED` for handing out servers without two matches
claiming the same one). EF Core's Npgsql provider is the best supported one. Docker matters
beyond the database: Redis has no maintained native Windows build, and self-hosting should be
one command rather than a list of installers.

**What it costs:** Docker Desktop and WSL2 have to be installed before anything runs locally.

## 2026-09-19 · C# for the backend

**Decided:** ASP.NET Core for the API, over Go, which was the other candidate.

**Why:** the CS2 side of this project is already C#. CounterStrikeSharp and MatchZy are C#,
so if FLICKED ever needs its own server plugin, it will be C# too. One language across the
backend and the plugin means one set of match and player models, shared as a library, instead
of two copies kept in step by hand. On top of that, SignalR covers the live queue and party
updates the launcher needs without hand-rolling WebSockets, and EF Core gives self-hosters
real migrations, so their database upgrades cleanly with each release.

**What it costs:** a heavier runtime than Go. Containers are around 100MB rather than 20MB,
and idle memory is higher, which matters a little for people self-hosting on a small VPS.

## Rust and Tauri for the launcher

**Decided:** Tauri, with the UI in React and TypeScript, over Electron.

**Why:** the launcher is something players keep open while they queue, so it has to be light.
Tauri uses the webview already installed on the system (WebView2 on Windows) instead of
shipping a browser, which is the difference between a download of a few megabytes and one of
a hundred, and between idling on a few hundred megabytes of memory and idling on far less.
It also has a native side, and that turns out to matter beyond size: a session token can live
in Rust and never enter the webview, and launching CS2 and reading files are things the UI
should not be doing anyway.

**What it costs:** Rust is the least familiar language in this project, so anything on that
side is slower to write and leans on help, while the React side is comfortable. Tauri's
ecosystem is also smaller than Electron's, so a problem is more likely to need reading the
source than finding an answer already written up.

---

### Writing a new decision

Copy this. Keep it short, and be honest about the cost. A decision with no trade-off usually
means the trade-off hasn't been found yet.

```markdown
## YYYY-MM-DD · Short title

**Decided:** what was chosen.

**Why:** the reason, in plain words.

**What it costs:** what is given up, or who has to do more work because of it.
```
