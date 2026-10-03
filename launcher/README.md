# FLICKED Launcher

Desktop client for FLICKED: Rust + Tauri 2, React 19 UI, no UI framework.

## Run

```
cd launcher
npm install
npm run tauri dev
```

## Layout

```
src/
  main.tsx            entry: fonts, global styles
  App.tsx             shell: title bar, sidebar, view switch, match-found dialog
  styles.css          all styles; tokens match website/app/globals.css
  components/         TitleBar, Sidebar, StatusBar, MatchFlow, PartyCards, FriendsPanel, AccountCard, Icon, Elapsed
  hooks/useQueue.ts   matchmaking state (idle → searching → found → vote → connecting)
  hooks/useParty.ts   party state: members, pending invites, kick, disband
  lib/                openExternal (links open in the browser), motion (animation preference), prefs
  views/              Play (eager), Matches / Leaderboard / News / Settings (lazy-loaded)
  data/demo.ts        demo data until the backend exists
src-tauri/            Rust side, window config, capabilities
  app-icon.png        1024² source the icon set is generated from (see below)
  icons/              generated; do not edit by hand
  src/presence.rs     Discord Rich Presence (hooks/usePresence.ts decides what it shows)
```

## Icon

`src-tauri/icons/` is generated, never edited by hand. The source is
`src-tauri/app-icon.png`: 1024×1024, the mark from `assests/icon.png` centred on
transparency at 88% of the canvas. To rebuild the set after changing it:

```
npm run tauri icon src-tauri/app-icon.png
```

That rewrites every PNG size, the Windows `.ico` (16, 24, 32, 48, 64 and 256 in
one file) and the macOS `.icns`. It also writes `icons/android/` and
`icons/ios/`, which this project does not build — delete them.

**What it costs:** the mark is three separated bands on transparency, so below
roughly 48px it reads as three orange bars rather than a figure. That is the
price of shipping the brand asset exactly as drawn; a solid dark plate behind it
would keep its shape down to 16px, at the cost of no longer matching the icon
used everywhere else.

## Performance rules

The launcher runs next to CS2, so it should cost close to nothing while idle.

- No `backdrop-filter`, no animated backgrounds; animate `transform` / `opacity` only.
  One exception: the Play friends column eases its width for 0.3s when opened or closed
  (measured: ~20 layouts, ~17 ms of main-thread work in total, then zero while idle).
- Animations follow Windows' *Animation effects* by default (Settings → Animations:
  Follow Windows / On / Off, see `src/lib/motion.ts`).
- Running clocks (`<Elapsed>`) re-render one text node, not the view.
- Only the Play view is in the main bundle; other views load on first visit.
- Fonts are bundled (latin subsets only) — no network at startup.
- Frameless window with a dark `backgroundColor`, so no white flash on open.
- Release profile in `Cargo.toml`: LTO, one codegen unit, `panic = "abort"`, stripped.
