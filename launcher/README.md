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
  components/         TitleBar, Sidebar, StatusBar, MatchFound, PartyCards, FriendsPanel, Icon, Elapsed
  hooks/useQueue.ts   matchmaking state (idle → searching → found → connecting)
  hooks/useParty.ts   party state: members, pending invites, kick, disband
  views/              Play (eager), Matches / Leaderboard / Settings (lazy-loaded)
  data/demo.ts        demo data until the backend exists
src-tauri/            Rust side, window config, capabilities
```

## Performance rules

The launcher runs next to CS2, so it should cost close to nothing while idle.

- No `backdrop-filter`, no animated backgrounds; animate `transform` / `opacity` only.
- Running clocks (`<Elapsed>`) re-render one text node, not the view.
- Only the Play view is in the main bundle; other views load on first visit.
- Fonts are bundled (latin subsets only) — no network at startup.
- Frameless window with a dark `backgroundColor`, so no white flash on open.
- Release profile in `Cargo.toml`: LTO, one codegen unit, `panic = "abort"`, stripped.
