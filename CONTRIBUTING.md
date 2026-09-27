# Contributing to FLICKED

Thanks for looking. This is a small project with a few strong opinions, and this
file is all of them in one place so you don't have to guess from a review.

The short version: **anything that changes behaviour comes with a test, comments
explain *why* rather than *what*, and a pull request says what you actually
verified.**

---

## Before you write code

**Open an issue first for anything substantial.** A bug fix or a small
improvement can arrive as a pull request. A new feature, a schema change, or
anything touching matchmaking is worth discussing first — not for ceremony, but
because a lot of design reasoning lives in [docs/](./docs) and it is a shame to
find out after you have written it.

**Read the part of the docs you are working near.** They exist to save you time:

| If you are touching… | Read first |
|---|---|
| Anything at all | [docs/ARCHITECTURE.md](./docs/ARCHITECTURE.md) |
| Matchmaking, queue, parties | [docs/PARTY.md](./docs/PARTY.md) |
| Ratings, stats, the leaderboard | [docs/RATING.md](./docs/RATING.md) |
| Anything the CS2 server sends | [docs/MATCHZY.md](./docs/MATCHZY.md) |
| The website or launcher UI | [docs/DESIGN.md](./docs/DESIGN.md) |
| Deployment and configuration | [docs/DEPLOY.md](./docs/DEPLOY.md) · [docs/DEPLOY-LINUX.md](./docs/DEPLOY-LINUX.md) |

**Looking for something to do?** The *Known gaps* section of
[docs/ROADMAP.md](./docs/ROADMAP.md) is an honest list of what is wrong today.

---

## Getting set up

You need [Docker](https://www.docker.com/products/docker-desktop/), the
[.NET 10 SDK](https://dotnet.microsoft.com/download), [Node.js](https://nodejs.org/)
and, for the launcher, [Rust](https://rustup.rs).

```bash
docker compose up -d                  # PostgreSQL

cd backend/Flicked.Api && dotnet run  # API on http://localhost:5165

cd launcher && npm install && npm run tauri dev
```

The website and dashboard are `npm install && npm run dev` in their own folders.

---

## Tests

### Running them

```bash
dotnet test backend
```

70 tests, about 30 seconds. They need **Docker running**, because they run against
a real PostgreSQL database — `flicked_test`, which the fixture creates and
migrates by itself on first run. You do not need to set it up.

### Why real Postgres and not an in-memory provider

Because the most important things in this codebase are races, and an in-memory
provider cannot express them:

- the server pool hands out servers with `FOR UPDATE SKIP LOCKED`, so two matches
  filling up at the same instant get different servers;
- two party invites accepted simultaneously are resolved by a unique index
  refusing the second insert;
- a match is finished by a conditional `UPDATE`, so a duplicate `series_end` — which
  the CS2 plugin genuinely sends — changes nothing the second time.

A test suite that cannot fail on any of those is decoration. Tests here open real
connections and mean it.

### Tests are expected for every meaningful change

Not a formality, and not "coverage". The rule is:

> If your change could break in a way a human would only notice during a match,
> write the test that notices it first.

**Needs a test:** new endpoints, changes to matchmaking, rating or the pool,
anything concurrent, anything parsing what the CS2 server sends, and every bug fix
(the test should fail before your fix and pass after — that is the proof the bug
was what you thought it was).

**Does not need one:** documentation, copy changes, styling, and pure refactors
where the existing tests already cover the behaviour.

**Write the test at the level the bug lives.** Most of this suite tests services
against a real database rather than HTTP, because that is where the logic is. If
you are fixing arithmetic, a pure unit test is better — see `RatingTests.cs`.
Follow whichever neighbour is closest to what you are changing.

### The other checks

```bash
cd launcher && npx tsc --noEmit        # launcher types
cd launcher/src-tauri && cargo check   # launcher Rust
cd website   && npx next build         # website builds
cd dashboard && npx next build         # dashboard builds
```

All of them should be clean before you open a pull request. One caution from
experience: `cargo check` occasionally reports `Finished` without recompiling
anything. If it returns suspiciously fast after a real edit, make it prove itself
with `cargo clean -p launcher && cargo check`.

---

## Style

**Comments explain why, not what.** This is the one thing most likely to come up
in review. The code says what it does; a comment earns its place by explaining a
decision, a trade-off, or a trap.

```csharp
// Bad: restates the code
// loop over the players and set their team

/* Good: explains the decision
   Parties are placed whole rather than drafted player by player, because the
   entire point of a party is landing on the same side. */
```

Read a few files near yours before writing. Match their density and their voice —
consistency matters more than any individual preference here.

**Naming and formatting** follow whatever the surrounding file does. There is no
separate style guide to memorise.

---

## Commits and pull requests

**Conventional commits**, with a scope:

```
feat(backend): the queue holds parties
fix(launcher): one build-time API address, not two
docs(roadmap): describe what shipped, and what is still wrong
test(backend): the rating maths, its edges, and the same match twice
```

A body is welcome when the *why* is not obvious from the subject. Several focused
commits beat one large one.

**In the pull request, say what you verified.** Something like:

> Ran `dotnet test backend` — 70 passed. Checked `cargo check` and `tsc`.
> Did not test against a real CS2 server.

That last line is the important one. **Never say something ran when it did not.**
"I could not test this part" is useful information; a confident claim that turns
out to be false costs the next person hours. Unverified work is welcome — it just
has to be labelled.

**Keep a pull request to one concern.** If you fixed a bug and noticed three other
things, that is four pull requests or one plus an issue.

---

## Security

**Never commit secrets.** `.env` is gitignored and should stay that way. If you
think you have committed a credential, say so immediately rather than quietly
force-pushing — pushing makes it public in the same moment, and the fix is to
rotate the credential, not to hide the commit.

**Do not open a public issue for a vulnerability.** Contact the maintainer
privately and give them time to fix it first.

Note that FLICKED self-hosts over plain HTTP by default, which is a deliberate
choice with consequences written down in
[docs/DEPLOY.md](./docs/DEPLOY.md#security). Changes that make the secure path
easier are very welcome.

---

## Licensing

By contributing, you agree your work is released under the same licence as the
project (see `LICENSE`). If you are porting code from elsewhere, say where it came
from and under what licence — FLICKED builds on other people's work and intends to
credit it properly.
