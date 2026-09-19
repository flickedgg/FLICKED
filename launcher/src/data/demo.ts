/* Demo data until the backend exists. Everything the UI reads from here
   will come from the FLICKED API later — keep the shapes, swap the source. */

export type Mode = { id: string; name: string; format: string; note: string; size: number };
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
export type LeaderRow = { rank: number; name: string; rating: number; wins: number; winRate: number };
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
  { id: "wingman", name: "Wingman",     format: "2v2 · MR8",  note: "Ranked. Short matches, small maps.", size: 2 },
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

export const RECENT: RecentMatch[] = [
  { id: 48213, map: "Mirage",  result: "W", score: "13–9",  kd: "21/12", adr: 97, delta: +24, when: "2h ago" },
  { id: 48190, map: "Inferno", result: "L", score: "10–13", kd: "15/17", adr: 72, delta: -19, when: "3h ago" },
  { id: 48122, map: "Nuke",    result: "W", score: "13–4",  kd: "18/8",  adr: 91, delta: +21, when: "Yesterday" },
  { id: 48077, map: "Ancient", result: "W", score: "13–11", kd: "17/15", adr: 80, delta: +18, when: "Yesterday" },
  { id: 47951, map: "Anubis",  result: "L", score: "7–13",  kd: "11/16", adr: 61, delta: -22, when: "2d ago" },
  { id: 47903, map: "Dust II", result: "W", score: "13–10", kd: "19/14", adr: 88, delta: +20, when: "3d ago" },
];

export const LEADERBOARD: LeaderRow[] = [
  { rank: 1, name: "kovac",     rating: 2614, wins: 311, winRate: 68 },
  { rank: 2, name: "Halden",    rating: 2571, wins: 287, winRate: 66 },
  { rank: 3, name: "Nyx",       rating: 2498, wins: 264, winRate: 64 },
  { rank: 4, name: "sprayz",    rating: 2402, wins: 240, winRate: 62 },
  { rank: 5, name: "quietus",   rating: 2366, wins: 198, winRate: 61 },
  { rank: 6, name: "reload",    rating: 2291, wins: 215, winRate: 59 },
  { rank: 7, name: "Brine",     rating: 2240, wins: 176, winRate: 59 },
  { rank: 8, name: "m0th",      rating: 2187, wins: 169, winRate: 58 },
  { rank: 9, name: "lowground", rating: 2105, wins: 151, winRate: 56 },
  { rank: 10, name: "patchnote", rating: 2050, wins: 143, winRate: 55 },
];

export const NETWORK = { online: 1204, servers: 38, host: "demo.flicked.local" };

export const NEWS_CATEGORIES: [NewsCategory, string][] = [
  ["patch", "Patch notes"],
  ["update", "Updates"],
  ["event", "Events"],
];

// newest first
export const NEWS: NewsPost[] = [
  {
    id: "n6", category: "patch", date: "18 Sep", badge: "0.2",
    title: "Alpha 0.2: parties and map vote",
    excerpt: "Queue with up to four friends, and pick the map together after everyone accepts.",
    body: [
      "Parties are here. Invite friends from the list on the Play screen, or share your party code so they can join directly. The leader queues for everyone.",
      "Map veto is replaced by a map vote. After all ten players accept, everyone has 15 seconds to vote. The map with the most votes is played; a tie is settled at random.",
      "Also in this release: the launcher starts faster, fonts ship with the app, and the window no longer flashes white on open.",
    ],
  },
  {
    id: "n5", category: "update", date: "15 Sep",
    title: "Self-host FLICKED on a single machine",
    excerpt: "A new guide walks through running the backend, database and one CS2 server on one box.",
    body: [
      "You do not need a cluster to run FLICKED. The new guide in the repository shows how to run the backend, PostgreSQL, Redis and a CS2 dedicated server on a single machine.",
      "It covers ports, the match config, and how to point the launcher at your own server.",
    ],
  },
  {
    id: "n4", category: "event", date: "12 Sep",
    title: "Community Cup #1: sign-ups open",
    excerpt: "Five-stack tournament, single elimination, played on community servers.",
    body: [
      "Sign-ups for the first FLICKED Community Cup are open. Teams of five, single elimination, best of one until the final.",
      "Matches run on community-hosted servers. Brackets are published the day before the first round.",
    ],
  },
  {
    id: "n3", category: "patch", date: "08 Sep", badge: "0.1.3",
    title: "Alpha 0.1.3: queue fixes",
    excerpt: "Fixes a case where a declined match kept you in queue, plus smaller stability fixes.",
    body: [
      "Declining a match now always returns you to the Play screen. Before, a declined match could leave you searching with no way to cancel.",
      "Reconnecting to a live match is faster, and the server log keeps the full match history.",
    ],
  },
  {
    id: "n2", category: "update", date: "03 Sep",
    title: "Every match now records a demo",
    excerpt: "Demos are saved on the server and can be downloaded from the match page.",
    body: [
      "Every match played on FLICKED now records a demo automatically. Demos are stored on the server that hosted the match.",
      "Server owners can set how long demos are kept.",
    ],
  },
  {
    id: "n1", category: "event", date: "29 Aug",
    title: "Weekly 5v5 night, Fridays at 20:00 CET",
    excerpt: "A standing night to find full stacks. Queue times drop, games get better.",
    body: [
      "Every Friday from 20:00 CET we play community 5v5s. More people in queue at the same time means shorter waits and closer matches.",
    ],
  },
];
