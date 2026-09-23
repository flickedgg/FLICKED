import { invoke } from "@tauri-apps/api/core";

/* Matchmaking, from the UI's side. Everything goes through Rust, because every
   one of these is about you and the token lives there.

   Null from any of them means "not signed in". */

export type QueuePhase = "idle" | "searching" | "found" | "vote" | "connecting" | "live";

/* One shape for every phase, so the launcher renders from a single object
   rather than stitching four endpoints together. */
export type QueueState = {
  phase: QueuePhase;
  mode: string | null;
  since: string | null;        // when this phase began; the clock counts up from it
  matchId: number | null;
  accepted: number;
  needed: number;
  youAccepted: boolean;
  yourVote: string | null;     // map code, e.g. "de_nuke"
  map: string | null;          // the winner, once the vote is settled
  connect: string | null;          // host:port, once a server is holding the match
  connectPassword: string | null;  // the server's game password, if it has one
  votes: Record<string, number>;
};

export const queueState = () => invoke<QueueState | null>("queue_state");
export const queueJoin = (mode: string) => invoke<QueueState | null>("queue_join", { mode });
export const queueLeave = () => invoke<QueueState | null>("queue_leave");
export const queueAccept = () => invoke<QueueState | null>("queue_accept");
export const queueDecline = () => invoke<QueueState | null>("queue_decline");
export const queueVote = (map: string) => invoke<QueueState | null>("queue_vote", { map });

/* Hands the server to Steam, which passes it to CS2 if the game is running and
   starts it if it is not. */
export const connectToMatch = (address: string, password: string | null) =>
  invoke<void>("connect_to_match", { address, password });
