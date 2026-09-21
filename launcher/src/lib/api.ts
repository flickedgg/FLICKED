import { invoke } from "@tauri-apps/api/core";
import type { LeaderRow, MatchRow, NewsPost } from "../data/demo";

/* Calls to the FLICKED backend. Public read-only endpoints live here;
   anything needing a session token will go through Rust instead. */
const API = "http://localhost:5165";

async function get<T>(path: string): Promise<T> {
  const res = await fetch(`${API}${path}`);
  if (!res.ok) throw new Error(`${path} failed: ${res.status} ${res.statusText}`);
  return await res.json() as T;
}

export const fetchNews = () => get<NewsPost[]>("/api/news");
export const fetchLeaderboard = () => get<LeaderRow[]>("/api/leaderboard");
/* Match history is personal, so it goes through Rust, which holds the session
   token; the backend works out whose history it is from that token. Null means
   nobody is signed in. */
export const fetchMatches = () => invoke<MatchRow[] | null>("my_matches");