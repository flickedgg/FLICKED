import { useEffect, useState } from "react";
import { invoke, isTauri } from "@tauri-apps/api/core";
import type { Mode } from "../data/demo";
import type { Queue } from "./useQueue";
import type { Party } from "./useParty";
import { PREFS_EVENT, readPrefs } from "../lib/prefs";

type PresenceData = {
  details: string;
  state: string;
  party?: [number, number];
  startedAt?: number; // ms; Discord shows the elapsed clock itself
};

// "time in the launcher" for the lobby clock
const OPENED_AT = Date.now();

function describe(queue: Queue, party: Party, mode: Mode): PresenceData {
  const size = 1 + party.members.length;
  const who = size > 1 ? `Party of ${size}` : "Solo";
  const seats: [number, number] = [size, mode.size];

  switch (queue.phase) {
    case "searching": return { details: `Searching · ${queue.label}`, state: who, party: seats, startedAt: queue.startedAt };
    case "found":     return { details: `Match found · ${queue.label}`, state: who, party: seats };
    case "vote":      return { details: `Picking the map · ${queue.label}`, state: who, party: seats };
    case "connecting":return { details: `${queue.label} · ${queue.map ?? ""}`, state: "Joining the server" };
    default:          return { details: "In the lobby", state: `${mode.name} · ${who}`, party: seats, startedAt: OPENED_AT };
  }
}

function useActivityEnabled() {
  const [on, setOn] = useState(() => readPrefs().discordActivity !== false);
  useEffect(() => {
    const sync = () => setOn(readPrefs().discordActivity !== false);
    window.addEventListener(PREFS_EVENT, sync);
    return () => window.removeEventListener(PREFS_EVENT, sync);
  }, []);
  return on;
}

/* Keeps the player's Discord activity in step with the launcher. Rust owns the
   connection (src-tauri/src/presence.rs); this only sends when the text changes,
   and waits a moment so quick changes (party filling up) collapse into one update:
   Discord rate-limits presence updates. */
export function usePresence(queue: Queue, party: Party, mode: Mode) {
  const enabled = useActivityEnabled();
  const data = describe(queue, party, mode);
  const key = JSON.stringify(data);

  useEffect(() => {
    if (!isTauri()) return;
    const id = window.setTimeout(() => {
      invoke(enabled ? "set_presence" : "clear_presence", enabled ? { data } : undefined).catch(() => {});
    }, 800);
    return () => clearTimeout(id);
  }, [key, enabled]); // `key` carries everything in `data`
}
