/* Settings are saved in localStorage under one key. Settings.tsx owns editing
   them; other parts of the app read them and listen for PREFS_EVENT. */

export const PREFS_KEY = "flicked.prefs";

// fired on window whenever Settings saves
export const PREFS_EVENT = "flicked:prefs";

export function readPrefs(): Record<string, unknown> {
  try { return JSON.parse(localStorage.getItem(PREFS_KEY) ?? "{}"); }
  catch { return {}; }
}
