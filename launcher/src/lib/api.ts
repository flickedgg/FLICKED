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
// playerId is temporary: the backend picks the signed-in player once accounts exist
export const fetchMatches = (playerId = 1) => get<MatchRow[]>(`/api/matches?playerId=${playerId}`);