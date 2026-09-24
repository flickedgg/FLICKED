import { invoke } from "@tauri-apps/api/core";
import type { LeaderRow, MatchRow, NewsPost } from "../data/demo";

/* Every call goes through Rust, including these.

   News and the leaderboard need no session token, so they used to be fetched
   here in the webview. That put them under two rules nothing else in the
   launcher meets: Tauri serves the app from https://tauri.localhost on Windows,
   which makes a plain-http API mixed content and drops the request before it is
   sent, and being another origin it then needs CORS as well. Asking Rust avoids
   both, and leaves one way of reaching the backend instead of two that fail
   differently. */

export const fetchNews = () => invoke<NewsPost[]>("news");
export const fetchLeaderboard = () => invoke<LeaderRow[]>("leaderboard");
/* Match history is personal, so it goes through Rust, which holds the session
   token; the backend works out whose history it is from that token. Null means
   nobody is signed in. */
export const fetchMatches = () => invoke<MatchRow[] | null>("my_matches");