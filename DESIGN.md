# FLICKED Design System

The visual language for everything FLICKED ships: the website (`website/`), the
launcher (Rust + Tauri), and anything that comes later. The website is the
reference implementation. Its tokens live in `website/app/globals.css`.

---

## 1. Principles

1. **Clean and quiet.** Dark, neutral, lots of space. Aim for the calm of a
   Valve product page, not a gaming RGB setup.
2. **One accent.** Orange is the only saturated colour. If everything is
   orange, nothing is.
3. **The product is the picture.** Show real UI (scoreboards, queues, server
   status) instead of decoration. No stock art, no abstract 3D.
4. **Plain words.** Anyone should understand a label without knowing CS
   jargon. Spell things out.
5. **Honest.** FLICKED is alpha. Say so. Never fake numbers, users or features.

### Things we tried and removed

These looked "cool" and made the product feel cheap. Don't bring them back:

- Laser beams, light rays, glowing blobs behind content
- Custom cursors (crosshairs etc.)
- Text scramble / decrypt effects, letter-by-letter headline animations
- Italic or slanted headings, skewed buttons
- Animated counters, spotlight-on-hover cards
- Floating overlapping cards and "collage" hero layouts
- Em dashes (—) in copy

---

## 2. Colour

Dark only. There is no light theme.

### Neutrals

| Token | Hex | Use |
|---|---|---|
| `bg` | `#0c0c0d` | App / page background |
| `surface` | `#131315` | Cards, panels |
| `elevated` | `#19191c` | Things above a surface: menus, dialogs, the match room |
| `foreground` | `#ffffff` | Headings, primary text, important values |
| `muted` | `#b4b4b8` | Body text |
| `subtle` | `#8d8d93` | Secondary text, captions, inactive tabs |
| `faint` | `#5a5a60` | Decoration only: dividers, disabled states, placeholder glyphs. **Not for text people need to read** (2.85:1) |

### Accent

| Token | Hex | Use |
|---|---|---|
| `primary` | `#f65600` | Sampled from the logo. Primary buttons, active states, live dots, progress fills, highlighted heading words |
| `primary-light` | `#ff7a33` | Orange **text** on dark (links, positive deltas, small highlights), focus ring |
| `slate` | `#7d8aa0` | The "other team" colour when two sides need telling apart |

### Status

| State | Colour |
|---|---|
| Live / active | `primary` dot with a soft ring: `box-shadow: 0 0 0 3px rgba(246,86,0,.18)` |
| Starting / pending | `#e0b341` |
| Finished / off | `faint` |
| Warning text (logs) | `#e0b341` |

### Lines and fills (white at low alpha)

| Purpose | Value |
|---|---|
| Hairline divider inside a card | `rgba(255,255,255,.05)` |
| Card border | `rgba(255,255,255,.08)` |
| Card border, hover | `rgba(255,255,255,.14)` |
| Outline button border | `rgba(255,255,255,.14)`, hover `.28` |
| Subtle hover fill | `rgba(255,255,255,.03)` |
| Inset "well" (a box inside a card) | `rgba(0,0,0,.18)` with a `.06` border |
| Orange tint (icon tiles, current step) | `rgba(246,86,0,.07)` to `.10`, border `rgba(246,86,0,.45)` |

### Contrast (WCAG)

| Text colour | on `bg` | on `surface` | on `elevated` |
|---|---|---|---|
| `foreground` | 19.6 | 18.6 | 17.5 |
| `muted` | 9.5 | 9.0 | 8.5 |
| `subtle` | 5.9 | 5.6 | 5.3 |
| `primary-light` | 7.5 | 7.1 | 6.8 |
| `primary` | 5.8 | 5.5 | 5.2 |
| `slate` | 5.6 | 5.3 | 5.0 |
| `faint` | **2.9** | **2.7** | **2.6** (fails, decoration only) |

**Text on an orange button is near-black `#0c0c0d` (5.8:1), never white (3.4:1).**

---

## 3. Typography

| Role | Font | Notes |
|---|---|---|
| Display | **Barlow Condensed** 700 | Headings only. UPPERCASE, upright, `letter-spacing: -0.005em`, tight line height (0.9 to 1) |
| UI / body | **Inter** 400 / 500 / 600 | Everything else: body, buttons, labels, card titles |
| Data | **JetBrains Mono** 400 / 500 | Numbers, IDs, logs, commands, small uppercase labels |

The **FLICKED wordmark** is the only italic text anywhere. It matches the
logo's slant.

### Scale

| Name | Size | Weight / style |
|---|---|---|
| Hero | `clamp(3.1rem, 8.5vw, 6rem)` | Display, line-height 0.92 |
| Section heading (h2) | `clamp(2.4rem, 4.6vw, 3.7rem)` | Display, line-height 0.95 |
| Card title | 18px | Inter 600, `-0.01em`, sentence case |
| Body large | 16.5 to 17px | Inter 400, line-height 1.65, `muted` |
| Body | 14.5px | Inter 400, `muted` |
| UI / button | 14 to 15px | Inter 500 to 600 |
| Caption | 12 to 12.5px | Inter, `subtle` |
| Label (eyebrow) | 12px | Mono 500, UPPERCASE, `letter-spacing: .12em`, `subtle` |
| Tiny label | 10.5px | Mono, UPPERCASE, `.14em` |

### Rules

- Headings are two beats: a statement in white, then a short payoff in
  `primary`. Example: "Everything a pug night needs. **Nothing rented.**"
- Card titles and UI labels are **sentence case**, not uppercase.
- Any number that updates or lines up in a column uses
  `font-variant-numeric: tabular-nums`.
- Keep body lines under ~60 characters (`max-width: 48ch` to `60ch`).

---

## 4. Layout and spacing

- **Base unit: 4px.** Common steps: 8, 12, 16, 24, 28, 40, 56, 64, 96.
- **Frame:** content max-width `1400px`, side padding 24px (mobile) / 40px (desktop).
- **Section padding:** 96px vertical on mobile, 128px on desktop.
- **Card padding:** 28px. **Grid gap:** 14px between cards.
- **Breakpoints:** 640 / 768 / 1024 / 1100px.

### Corner radius

| Radius | Used for |
|---|---|
| 3 to 4px | Tags, tiny chips, round-history bars |
| 6px | Buttons, inputs, nav items, map tiles |
| 8px | Wells inside cards, list rows, stat tiles |
| 10px | Icon tiles, header bar, dialogs |
| 12px | Cards and panels |
| 999px | Only for dots, avatars, progress bars |

### Signature details (keep these)

- **Frame rails:** two 1px vertical hairlines at the edges of the 1400px
  column (`rgba(255,255,255,.06)`, fading out at top and bottom). Desktop only.
- **Ambient field:** three very soft radial colour fields (orange, cool blue,
  amber) behind the content, drifting slowly. Each section sets a tone and the
  tint crossfades as you scroll. Alpha stays between 0.04 and 0.2, with a 5%
  noise overlay against banding.
- **Section seams:** 1px horizontal hairline between sections, faded at both ends.

---

## 5. Surfaces and elevation

Depth comes from **surface colour and borders, not glow**. Shadows are black
and soft. Coloured shadows and glows are off-brand.

| Plane | Background | Border | Shadow |
|---|---|---|---|
| 0: page | `bg` | none | none |
| 1: card | `surface` | `rgba(255,255,255,.08)` | none |
| 2: raised (menus, dialogs, hero product UI) | `elevated` | `rgba(255,255,255,.10)` | `0 20px 60px rgba(0,0,0,.5)` |

---

## 6. Components

### Buttons

| Variant | Style |
|---|---|
| Primary | `primary` fill, `#0c0c0d` text, Inter 600, no border. Hover `#ff6414` |
| Outline | transparent, white text, `.14` border. Hover: `.28` border + `.03` fill |

Sizes: default 42px tall (padding 0 18px, 14px text); large 48px (0 22px, 15px).
Radius 6px. Icons 16px with a 9px gap. **One primary button per view.**

### Cards

`surface` background, 12px radius, `.08` border, 28px padding. Order inside:

1. **Icon tile:** 40×40, 10px radius, `rgba(246,86,0,.1)` fill, `primary-light` icon
2. **Title:** 18px Inter 600
3. **One sentence:** 14.5px `muted`
4. **A small labelled picture** of the feature, pushed to the bottom

Hover only brightens the border. No lifts, no glows.

### Labels and chips

- **Eyebrow** above a heading: mono, uppercase, `subtle`. No decorations.
- **Tag:** mono 11.5px, 3px radius, `.09` border, optional 6px `primary` dot.

### Tabs

Text tabs, 13.5px Inter 500. Inactive `subtle`, hover `muted`, active white
with a 2px `primary` underline inset 16px from each side.

### Lists and tables

Rows are 10px vertical padding, separated by `.05` hairlines, no zebra
striping. Column headers are 11.5px `subtle` (or mono uppercase 10.5px for
dense data tables). Numbers are right-aligned and tabular.

### Status

A 7px dot plus a word. Never colour alone: always say "Live", "Starting",
"Finished".

### Progress

4px tall, fully rounded, `.08` track, `primary` fill.

### Steps

A row of boxes. Done steps show a ✓ in a neutral circle; the current step
gets an orange border and tint and a filled orange number.

### Inputs (for the launcher; not on the website yet)

Follow the button geometry: 42px tall, 6px radius, `rgba(0,0,0,.18)` fill,
`.10` border, white text, `subtle` placeholder. Focus is a `primary-light`
1.5px outline with 3px offset (the same focus ring used everywhere).

---

## 7. Icons

- Line icons on a 24px grid, **1.7px stroke**, round caps and joins, no fill.
  [Lucide](https://lucide.dev) matches this style.
- 20px inside a 40px tile for features; 16px inline in buttons.
- Brand logos (tech stack) use Simple Icons (`react-icons/si`), monochrome.

---

## 8. Motion

Motion is quiet and never needed to understand the UI.

- **Durations:** 150 to 250ms for hovers and state changes; up to 850ms for a
  section fading in.
- **Easing:** `cubic-bezier(.22,.61,.36,1)`.
- **Allowed:** fade-up on scroll (18px), tab content crossfade, the slow
  ambient drift, live values ticking.
- **Always respect `prefers-reduced-motion`:** everything must look complete
  with no motion at all.

---

## 9. Writing

- Plain language. Say "Damage per round", not "ADR". Say "Match replays", not "Demos".
- When a CS term is the right word (map veto), keep it and explain it once
  nearby: "Captains take turns banning maps. The last one left is the map you play."
- No em dashes. Use a full stop, comma or colon.
- Sentence case for buttons and labels: "Get started", "Report an issue".
- Short headings with a payoff, one-sentence descriptions.
- Be upfront about alpha status and missing features (anti-cheat is planned,
  not shipped).

---

## 10. Launcher notes (Tauri)

The launcher renders web UI, so the CSS tokens below apply directly.

- **Density:** it's a tool, not a landing page. Use the component sizes above,
  but section padding drops to 24 to 32px and headings to the h3 size or smaller.
  Hero-size display type belongs on the website only.
- **Window:** dark custom titlebar in `bg` with a `.06` bottom hairline;
  `data-tauri-drag-region` on the bar; window controls at 40×32. Keep the
  frame rails and ambient field off inside the app, or at half strength at most.
- **Navigation:** left sidebar (`surface`, `.08` right border) with 36px nav
  items, same active styling as tabs (white text plus a 2px `primary` marker).
- **Primary action:** "Find match" / "Accept" / "Connect" is the one orange
  button on screen.
- **Native feel:** `user-select: none` on chrome (not on content), no
  rubber-band scroll, no hover-only controls, and everything reachable by
  keyboard with the shared focus ring.
- **Match found / accept:** the one place a bigger moment is fine. Use a plane-2
  dialog with the countdown and the ten player slots (filled `primary` when
  accepted). Keep it solid, with no glows or animation tricks.

---

## 11. Tokens

### CSS custom properties

```css
:root {
  /* neutrals */
  --bg: #0c0c0d;
  --surface: #131315;
  --elevated: #19191c;
  --foreground: #ffffff;
  --muted: #b4b4b8;
  --subtle: #8d8d93;
  --faint: #5a5a60;

  /* accent */
  --primary: #f65600;
  --primary-light: #ff7a33;
  --slate: #7d8aa0;
  --warning: #e0b341;

  /* lines */
  --line-soft: rgba(255, 255, 255, 0.05);
  --line: rgba(255, 255, 255, 0.08);
  --line-strong: rgba(255, 255, 255, 0.14);

  /* shape */
  --radius-sm: 4px;
  --radius-control: 6px;
  --radius-well: 8px;
  --radius-tile: 10px;
  --radius-card: 12px;

  /* depth */
  --shadow-raised: 0 20px 60px rgba(0, 0, 0, 0.5);

  /* motion */
  --ease: cubic-bezier(.22, .61, .36, 1);

  /* type */
  --font-display: "Barlow Condensed", system-ui, sans-serif;
  --font-sans: "Inter", system-ui, sans-serif;
  --font-mono: "JetBrains Mono", ui-monospace, monospace;
}
```

### Tailwind v4

```css
@theme {
  --color-bg: #0c0c0d;
  --color-surface: #131315;
  --color-elevated: #19191c;
  --color-foreground: #ffffff;
  --color-muted: #b4b4b8;
  --color-subtle: #8d8d93;
  --color-faint: #5a5a60;
  --color-primary: #f65600;
  --color-primary-light: #ff7a33;
  --color-slate: #7d8aa0;
  --shadow-plane2: 0 20px 60px rgba(0, 0, 0, 0.5);
}
```

---

## 12. Brand assets

- **Logo:** `assests/icon.png` (also `website/public/icon.png`). An orange
  slanted F with a player silhouette. Don't recolour, outline or rotate it.
  Minimum size 20px tall.
- **Wordmark:** "FLICKED" in Barlow Condensed 700 italic, white, 23px next to
  a 30px logo in headers.
- **Favicon / app icon:** the logo alone (`website/app/icon.png`).

---

## Known gaps on the website

- A few small labels (`.stat-k`, table headers in the match room and feature
  cards, footer headings) use `faint` for text, below the 4.5:1 contrast minimum. They
  should move to `subtle`.
