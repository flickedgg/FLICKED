/* Animation preference.
   "system" follows Windows (Settings → Accessibility → Visual effects →
   Animation effects), which WebView2 reports as prefers-reduced-motion.
   "on" / "off" override it. The CSS reads <html data-motion>. */

import { PREFS_KEY } from "./prefs";

export type Motion = "system" | "on" | "off";

export function applyMotion(m: Motion) {
  if (m === "system") delete document.documentElement.dataset.motion;
  else document.documentElement.dataset.motion = m;
}

// reads the saved choice; an old `reduceMotion: true` becomes "off"
export function storedMotion(): Motion {
  try {
    const p = JSON.parse(localStorage.getItem(PREFS_KEY) ?? "{}");
    return p.motion ?? (p.reduceMotion ? "off" : "system");
  } catch {
    return "system";
  }
}
