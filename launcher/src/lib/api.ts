import type { NewsPost } from "../data/demo";
import type { LeaderRow } from "../data/demo";

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