# TODO

Found during the first multiplayer test on the VPS (24 September 2026), with two
players on distributed launcher builds. Ordered by what blocks a session.

---

## Blocking

### 1. News and leaderboard fail in a distributed build

**Reported as:** rank and news blocked, looks like CORS.

**Actual cause:** not CORS. `launcher/src/lib/api.ts` holds a second copy of the
API address, hard-coded to `http://localhost:5165`:

```ts
const API = "http://localhost:5165";
```

`auth.rs` takes its address from `FLICKED_API` at build time, but this file was
missed. News and leaderboard are the only two screens that call the backend with
`fetch` directly; everything else goes through Rust via `invoke` and therefore
uses the correct address. On a friend's machine those two calls reach their own
PC, where nothing is listening, and the webview reports it as a network or CORS
error.

**Fix:** one source for the address. Either expose the Rust constant to the
webview, or set it from the same environment variable at build time
(`import.meta.env.VITE_FLICKED_API`), and delete the literal. Whichever is
chosen, it must be impossible to build a launcher where the two disagree.

**Done when:** a launcher built on one machine shows news and leaderboard on
another machine, with no backend running locally.

### 2. Party invites do not work

**Reported as:** cannot invite friends to a party.

Unknown whether the invite fails to send, fails to arrive, or arrives and is not
rendered — issue 4 below means an arriving invite may simply not be drawn until
the view is remounted, so these may be the same defect.

**Fix:** determine which of the three it is before changing anything. Check that
the invite endpoint is reached and what it returns, then whether the recipient's
state contains it.

**Done when:** two players in separate launchers can form a party and queue
together.

---

## Wrong behaviour

### 3. The phase countdown is wrong by a few seconds

**Reported as:** stuck on 0 for a few seconds, or starting at 12 seconds.

**Probable cause:** clock skew between the VPS and the player's PC. The backend
sends the instant a phase began (`since`, in UTC) and the launcher counts up from
it using the local clock. If the server's clock is ahead, the timer starts partway
through; if behind, it sits at zero until local time catches up. Both reported
symptoms are the same bug with opposite signs, which is why it is not a rendering
problem.

**Fix:** stop subtracting two different clocks. Have the backend send elapsed or
remaining seconds for the current phase, or measure the offset once from a server
timestamp and apply it. The accept and vote windows are short enough that a
twelve-second error is most of the window.

**Done when:** two launchers on different machines show the same countdown to
within a second, and a phase always begins at 0.

### 4. Friend requests do not appear until the view is remounted

**Reported as:** an incoming request only shows after navigating away and back.

**Cause:** the friends list is fetched when the view mounts and not refreshed
afterwards, so an event that arrives while the page is open is never drawn.

**Fix:** short term, poll while the friends view is open, as the queue already
does. Properly, this is what pushed updates are for: the same connection would
serve friend requests, party invites and presence, and would remove the polling
in `useQueue` as well.

**Done when:** a request sent from one launcher appears in the other's list
without navigating.

---

## Interface

### 5. The persistent search panel is not centred

The queue panel shown while browsing other pages sits left of centre. It is
otherwise good and should stay as it is.

**Done when:** it is horizontally centred at every window width the launcher
supports.

### 6. Remove the game section from Settings

It configures what Steam already owns. Delete the section rather than disable it,
along with whatever it wrote, so nothing reads a setting that no longer has a
way to be changed.

**Done when:** Settings no longer shows it and nothing in the codebase reads its
values.

---

## Known gaps, not from this session

- **No match has ever finished.** `map_result` and `series_end` have never been
  observed, so results reaching the leaderboard and match history is untested.
  Next real match should be short and watched.
- **A match cannot be cancelled** except by editing the database, and a player
  can leave the queue but not a match.
- **Seeded demo players** (kovac, Halden, Nyx and the rest) appear on the
  production leaderboard alongside real people.
- **`ROADMAP.md` is several milestones stale** — the server pool, MatchZy
  integration and matchmaking all shipped and are not reflected.
