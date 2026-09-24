/* Demo data until the backend exists. Everything the UI reads from here
   will come from the FLICKED API later — keep the shapes, swap the source. */

/* `soon` marks a mode that is shown but cannot be queued for. Kept visible
   rather than hidden so the plan is legible: the mode exists, it is not ready.
   The backend already sizes Wingman matches (Matchmaker.PlayersFor), so this is
   only about not offering a queue nobody can fill yet. */
export type Mode = { id: string; name: string; format: string; note: string; size: number; soon?: boolean };
export type Presence = "online" | "ingame" | "offline";
export type Stats = { matches: number; winRate: number; kd: number; adr: number };
export type Friend = {
  id: string; name: string; rating: number; division: string;
  presence: Presence; activity: string; stats: Stats;
};
export type RecentMatch = {
  id: number; map: string; result: "W" | "L"; score: string;
  kd: string; adr: number; delta: number; when: string;
};
/* What /api/matches returns: one player's view of a match. Unlike RecentMatch
   the map is a server code (de_mirage) and the time is a real timestamp. */
export type MatchRow = {
  id: number; map: string; result: "W" | "L"; score: string;
  kd: string; adr: number; delta: number; playedAt: string;
};
export type LeaderRow = {
  rank: number; playerId: number; name: string; rating: number; wins: number; winRate: number;
};
export type NewsCategory = "patch" | "update" | "event";
export type NewsPost = {
  id: string; category: NewsCategory; title: string; date: string;
  excerpt: string; body: string[];
  badge?: string; // big watermark on the featured card, e.g. a version number
};

export const PLAYER = {
  name: "viix0",
  rating: 1842,
  division: "Division II",
  toNext: 158, // points to the next division
  progress: 0.62,
  stats: { matches: 128, winRate: 57, kd: 1.21, adr: 84.6 } as Stats,
};

export const ACCOUNT = {
  steam: { persona: "viix0", id64: "76561198000000000" },
};

export const MODES: Mode[] = [
  { id: "comp",    name: "Competitive", format: "5v5 · MR12", note: "Ranked. Map veto, full match.", size: 5 },
  { id: "wingman", name: "Wingman",     format: "2v2 · MR8",  note: "Ranked. Short matches, small maps.", size: 2, soon: true },
];

export const FRIENDS: Friend[] = [
  { id: "f1", name: "Nyx",       rating: 1910, division: "Division II",  presence: "online",  activity: "Online",   stats: { matches: 164, winRate: 59, kd: 1.28, adr: 88.1 } },
  { id: "f2", name: "reload",    rating: 1765, division: "Division III", presence: "online",  activity: "Online",   stats: { matches: 97,  winRate: 52, kd: 1.04, adr: 76.3 } },
  { id: "f3", name: "m0th",      rating: 1698, division: "Division III", presence: "online",  activity: "In menus", stats: { matches: 211, winRate: 50, kd: 0.97, adr: 71.8 } },
  { id: "f4", name: "Halden",    rating: 2571, division: "Division I",   presence: "ingame",  activity: "Mirage · 8–5", stats: { matches: 420, winRate: 66, kd: 1.41, adr: 93.5 } },
  { id: "f5", name: "sprayz",    rating: 2402, division: "Division I",   presence: "ingame",  activity: "In queue", stats: { matches: 388, winRate: 62, kd: 1.33, adr: 90.2 } },
  { id: "f6", name: "patchnote", rating: 1520, division: "Division IV",  presence: "offline", activity: "Last seen 2h ago", stats: { matches: 61, winRate: 47, kd: 0.88, adr: 66.0 } },
  { id: "f7", name: "Brine",     rating: 2240, division: "Division I",   presence: "offline", activity: "Last seen yesterday", stats: { matches: 302, winRate: 59, kd: 1.19, adr: 83.4 } },
];

export const MAP_POOL = ["Mirage", "Inferno", "Nuke", "Ancient", "Anubis", "Dust II", "Train"];
export const MAP_CODE: Record<string, string> = {
  Mirage: "de_mirage", Inferno: "de_inferno", Nuke: "de_nuke", Ancient: "de_ancient",
  Anubis: "de_anubis", "Dust II": "de_dust2", Train: "de_train",
};

// the other way round: servers and the API talk in codes, screens show names
export const MAP_NAME: Record<string, string> = Object.fromEntries(
  Object.entries(MAP_CODE).map(([name, code]) => [code, name]),
);



export const NETWORK = { online: 1204, servers: 38, host: "demo.flicked.local" };

export const NEWS_CATEGORIES: [NewsCategory, string][] = [
  ["patch", "Patch notes"],
  ["update", "Updates"],
  ["event", "Events"],
];

// newest first
