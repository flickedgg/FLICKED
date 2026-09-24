# How FLICKED fits together

Four programs and a database. The backend owns every decision; everything else
asks it. This document is the map: what talks to what, in what order, and which
piece is trusted with what.

If a diagram disagrees with the code, the code is right and this file is stale.

---

## 1. The whole system

```mermaid
graph TB
    subgraph players["Players"]
        launcher["<b>Launcher</b><br/>Tauri · Rust + React<br/>queue, friends, history"]
    end

    subgraph admins["Admins"]
        dashboard["<b>Dashboard</b><br/>Next.js<br/>manage the server pool"]
    end

    subgraph public["Anyone"]
        website["<b>Website</b><br/>Next.js<br/>marketing, docs"]
    end

    backend["<b>Backend</b><br/>ASP.NET Core · C#<br/>every rule lives here"]
    db[("<b>PostgreSQL</b><br/>players, matches,<br/>friendships, servers, queue")]

    subgraph games["Community hardware"]
        cs2["<b>CS2 servers</b><br/>Metamod + CounterStrikeSharp<br/>+ MatchZy"]
    end

    steam["<b>Steam</b><br/>OpenID + Web API"]

    launcher -->|"HTTPS · bearer token"| backend
    dashboard -->|"HTTPS · session cookie"| backend
    website -.->|"static, no API calls yet"| backend
    backend --> db
    backend -->|"RCON · TCP 27015"| cs2
    cs2 -->|"match events · HTTPS"| backend
    backend -->|"verify sign-in, read profiles"| steam
    launcher -->|"sign in, in the system browser"| steam
```

**The rule that shapes everything:** the backend decides, the clients display.
No client is trusted with identity, match results, or who may do what.

---

## 2. Inside the backend

```mermaid
graph TB
    subgraph controllers["Controllers · HTTP"]
        auth["AuthController<br/>sign-in, sessions, /me"]
        queue["QueueController<br/>join, accept, vote"]
        friends["FriendsController"]
        party["PartyController<br/>invite, accept, kick"]
        social["SocialController<br/>/api/social/state, polled"]
        matches["MatchesController<br/>your history"]
        news["NewsController<br/>LeaderboardController"]
        adminSrv["AdminServersController<br/>the pool, admin only"]
        srvApi["MatchServerController<br/>ServersController<br/>called by CS2 servers"]
    end

    subgraph services["Services · the rules"]
        currentPlayer["CurrentPlayer<br/>token or cookie → Player"]
        serverAuth["ServerAuth<br/>X-Server-Token → GameServer"]
        steamOpenId["SteamOpenId · SteamProfile"]
        matchmaker["Matchmaker<br/>queue → matches, phases"]
        partySvc["Parties<br/>who may join, leave, invite"]
        socialSvc["Social<br/>party state for a screen"]
        pool["ServerPool<br/>claim · release · sweep"]
        starter["MatchStarter<br/>token + RCON command"]
        rcon["Rcon<br/>Source RCON client"]
        secrets["Secrets · ServerSecrets<br/>hash what you check,<br/>encrypt what you replay"]
    end

    subgraph loops["Background loops"]
        poolJanitor["PoolJanitor · 30s<br/>expire leases, check servers"]
        mmJanitor["MatchmakerJanitor · 2s<br/>form, advance, start,<br/>sweep expired invites"]
    end

    db[("PostgreSQL · EF Core")]

    auth --> steamOpenId
    auth --> currentPlayer
    queue --> currentPlayer
    friends --> currentPlayer
    party --> currentPlayer
    party --> partySvc
    party --> socialSvc
    social --> currentPlayer
    social --> socialSvc
    queue --> partySvc
    matches --> currentPlayer
    adminSrv --> currentPlayer
    adminSrv --> starter
    adminSrv --> secrets
    srvApi --> serverAuth
    srvApi --> pool

    mmJanitor --> matchmaker
    mmJanitor --> partySvc
    mmJanitor --> pool
    mmJanitor --> starter
    poolJanitor --> pool
    pool --> rcon
    starter --> rcon
    starter --> secrets

    controllers --> db
    services --> db
    loops --> db
```

Controllers are thin: authenticate, validate, call a service, shape a response.
Anything with a rule in it (who may claim a server, what a fair match is, when a
lease has expired) lives in a service, so the matchmaker and an admin pressing a
button go through exactly the same code.

---

## 3. Signing in

Two clients, two ways to carry a session, one `Sessions` table.

```mermaid
sequenceDiagram
    participant L as Launcher (Rust)
    participant B as Browser
    participant API as Backend
    participant S as Steam

    Note over L: the launcher can hold a secret,<br/>a browser cannot
    L->>L: listen on 127.0.0.1:<free port>
    L->>B: open /auth/steam/login?port=…
    B->>API: GET /auth/steam/login
    API->>B: redirect to Steam
    B->>S: sign in (password never touches FLICKED)
    S->>B: redirect back with an assertion
    B->>API: GET /auth/steam/callback
    API->>S: check_authentication (server to server)
    S-->>API: is_valid:true
    API->>API: find or create Player by Steam ID
    API->>B: redirect to 127.0.0.1:<port>?code=…
    B->>L: the one-time code
    L->>API: POST /auth/exchange { code }
    API-->>L: session token (shown once)
    L->>L: store in the OS keychain
```

**Why a code rather than the token:** URLs end up in browser history and logs.
The code is single-use, expires in two minutes, and the token only ever travels
in a request the launcher makes itself.

**The dashboard** uses the same verification but ends differently: the backend
sets an **HttpOnly cookie** and redirects back, because a browser cannot keep a
token safely. `CurrentPlayer` accepts either.

**Verification is server to server.** Everything arriving from the browser is a
claim; only Steam confirming it makes it true.

---

## 4. A match, start to finish

```mermaid
sequenceDiagram
    participant P as 10 launchers
    participant API as Backend
    participant MM as MatchmakerJanitor (2s)
    participant Pool as ServerPool
    participant CS as CS2 server + MatchZy

    P->>API: POST /api/queue
    Note over MM: every 2 seconds
    MM->>MM: FormMatchesAsync<br/>group by rating window
    MM-->>P: match found (polled)
    P->>API: POST /api/queue/accept ×10
    MM->>MM: all accepted → Voting
    P->>API: POST /api/queue/vote ×10
    MM->>MM: most votes wins → Pending

    MM->>Pool: ClaimAsync(mode, matchId)
    Note over Pool: SELECT … FOR UPDATE SKIP LOCKED<br/>so two matches cannot take one server
    Pool-->>MM: a server, now Reserved
    MM->>CS: RCON: matchzy_loadmatch_url <url> X-Match-Token <token>
    CS->>API: GET /api/matches/{id}/config (with that token)
    API-->>CS: roster, map, and where to report
    CS->>API: series_start
    CS->>API: going_live
    API->>Pool: MarkHosting (lease 5m → 90m)
    Note over CS: the match is played
    CS->>API: map_result (score + per-player stats)
    CS->>API: series_end
    API->>Pool: ReleaseAsync → Idle
```

**The config carries its own reporting settings**, so a stock MatchZy needs
nothing configured by hand beyond an RCON password.

**MatchZy never retries and never deduplicates.** So every handler is idempotent,
and the lease is the safety net: a lost `series_end` cannot strand a server.

---

## 5. Two state machines

```mermaid
stateDiagram-v2
    direction LR
    [*] --> Accepting: ten players found
    Accepting --> Voting: everyone accepted
    Accepting --> Cancelled: someone declined<br/>or the 20s ran out
    Voting --> Pending: map chosen (15s)
    Pending --> Live: going_live
    Live --> Finished: map_result / series_end
    Pending --> Cancelled: no server, or nobody connected
    Finished --> [*]
    Cancelled --> [*]
```

```mermaid
stateDiagram-v2
    direction LR
    [*] --> Offline: registered by an admin
    Offline --> Idle: answers RCON (checked every 30s)
    Idle --> Offline: stops answering
    Idle --> Reserved: claimed for a match
    Reserved --> Hosting: going_live
    Hosting --> Idle: series_end
    Reserved --> Idle: lease expired (20m)
    Hosting --> Idle: lease expired (90m)
```

The lease arrows are the ones that matter. Without them, one crashed server
leaves the pool silently and forever.

---

## 6. The data

```mermaid
erDiagram
    Players ||--o{ Sessions : "signs in"
    Players ||--o{ LoginCodes : "one-time"
    Players ||--o| PartyMembers : "is in at most one"
    Parties ||--|{ PartyMembers : "has"
    Parties ||--o{ PartyInvites : "has pending"
    Parties ||--o| QueueEntry : "waits, as one"
    Players ||--o{ MatchPlayers : "plays"
    Players ||--o{ Friendships : "requester / addressee"
    Matches ||--|{ MatchPlayers : "has ten"
    Matches ||--o| Servers : "is hosted on"

    Players {
        int Id PK
        string SteamId UK "null for seeded demo rows"
        string Name "refreshed from Steam"
        int Rating
        bool IsAdmin
    }
    Matches {
        int Id PK
        string Map "chosen by vote"
        int ScoreA_ScoreB
        enum Status "Accepting→Voting→Pending→Live→Finished"
        string ConfigTokenHash "one match only"
    }
    MatchPlayers {
        int MatchId PK_FK
        int PlayerId PK_FK
        int Team "0 or 1"
        int Kills_Deaths_Adr "from map_result"
        datetime AcceptedAt
        string MapVote
    }
    Servers {
        int Id PK
        string Host_Port UK
        string RconPasswordEncrypted "encrypted, not hashed"
        string TokenHash
        enum Status
        datetime LeaseUntil "the safety net"
    }
    QueueEntry {
        int PartyId UK "one queue at a time"
        enum Mode
        datetime JoinedAt "window widens from here"
    }
    Parties {
        int Id PK
        int LeaderId FK "only the leader queues and invites"
        datetime CreatedAt
    }
    PartyMembers {
        int PartyId FK
        int PlayerId UK "the database enforces one party each"
        datetime JoinedAt "display order, and who leads next"
    }
    PartyInvites {
        int PartyId FK
        int ToPlayerId "unique with PartyId"
        int FromPlayerId
        datetime ExpiresAt "swept by MatchmakerJanitor"
    }
```

**Every queue entry is a party**, and somebody playing alone is a party of one.
There is no second shape of queue entry for solo players, so there is no solo
path for the party path to disagree with: the matchmaker only ever asks how many
seats a row takes and what it is rated.

**Derived, never stored:** a player's rank, win rate, K/D and ADR; a match's
result; whether a server is free. Anything stored twice eventually disagrees
with itself.

---

## 7. Who is trusted with what

| Credential | Held by | Stored as | Proves |
|---|---|---|---|
| Session token | Launcher, in the OS keychain | SHA-256 hash | you are this player |
| Session cookie | Browser, HttpOnly | the same `Sessions` row | you are this admin |
| Server token | A CS2 server's config | SHA-256 hash | this is that server |
| Match token | Sent in the RCON command | SHA-256 hash | this is that one match |
| RCON password | The backend | **encrypted** (Data Protection) | lets FLICKED command a server |

**Hash what you only check; encrypt what you have to replay.** An RCON password
has to be sent again, so it cannot be hashed; everything else is only ever
compared, so it is never stored in a form anyone could use.

**The launcher's webview never sees a token.** Rust holds it, makes the
authenticated calls, and hands the interface names and numbers only, so a
scripting bug in the UI cannot walk off with a session.

---

## 8. Inside the launcher

```mermaid
graph LR
    subgraph webview["Webview · React + TypeScript"]
        views["Views<br/>Play · Matches · Friends<br/>Leaderboard · News"]
        hooks["Hooks<br/>useSession · useSocial<br/>useQueue · useParty · usePresence"]
        api["lib/api.ts<br/>public endpoints"]
    end

    subgraph rust["Rust · src-tauri"]
        authrs["auth.rs<br/>Steam sign-in, keychain"]
        apirs["api.rs<br/>authenticated calls"]
        presence["presence.rs<br/>Discord rich presence"]
    end

    backend["Backend"]

    views --> hooks
    hooks --> api
    hooks -->|"invoke()"| authrs
    hooks -->|"invoke()"| apirs
    api -->|"news, leaderboard"| backend
    apirs -->|"+ bearer token"| backend
    authrs --> backend
```

**The split:** public data (news, leaderboard) is fetched straight from the
webview; anything about *you* goes through Rust, because that is where the token
lives. Each authenticated endpoint gets its own command rather than one general
"call this path", so a compromised webview cannot borrow the token freely.

---

## 9. Inside the dashboard

```mermaid
graph LR
    page["app/page.tsx<br/>server component<br/>admin gate"]
    session["lib/session.ts<br/>forwards the cookie"]
    panel["components/servers-panel.tsx<br/>client component"]
    servers["lib/servers.ts<br/>credentials: include"]
    backend["Backend<br/>/api/admin/servers"]

    page --> session --> backend
    page --> panel --> servers --> backend
```

**Two gotchas worth knowing**, both of which cost time here:

- A **server component's** fetch does not carry the browser's cookies. They have
  to be read with `next/headers` and forwarded by hand, or every request looks
  signed out.
- A **client component's** fetch needs `credentials: "include"`, and the backend
  must name the dashboard's origin in CORS: a wildcard is refused once
  credentials are involved.

The admin check runs on the server on every request, with no caching, so a
revoked session or a removed admin flag takes effect immediately.

---

## 10. What runs on a timer

| Loop | Every | Does |
|---|---|---|
| `MatchmakerJanitor` | 2s | form matches, advance accept/vote phases, start ready matches |
| `PoolJanitor` | 30s | reclaim expired leases, check each server over RCON |

Both open a fresh DI scope per pass (a `BackgroundService` is a singleton and
must never hold a `DbContext`) and swallow exceptions, because one bad pass must
not end the loop for the life of the process.

---

## Where the seams are

Things deliberately left as boundaries, so they can be replaced without touching
the rest:

- **The pool answers one question:** "give me a server for this match". Today the
  answer comes from a table of registered servers; automatic provisioning would
  be another way a row appears, and matchmaking would never notice.
- **MatchZy sits behind two endpoints** (a config and an event feed). A custom
  CounterStrikeSharp plugin would speak the same two.
- **Presence and the queue are polled today.** SignalR would replace the polling
  without changing what the screens render.
