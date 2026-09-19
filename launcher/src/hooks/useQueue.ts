import { useCallback, useEffect, useRef, useState } from "react";
import { MAP_POOL } from "../data/demo";

export type Phase = "idle" | "searching" | "found" | "vote" | "connecting";

export const ACCEPT_SECONDS = 20;
export const VOTE_SECONDS = 15;
const PLAYERS = 10;

export type Queue = ReturnType<typeof useQueue>;

/* Matchmaking state: search → accept → map vote → connect.
   The timings and the other nine votes are simulated until the backend
   pushes real queue events; the phases and actions are what the UI will keep. */
export function useQueue() {
  const [phase, setPhase] = useState<Phase>("idle");
  const [startedAt, setStartedAt] = useState(0);
  const [label, setLabel] = useState("");            // what is being searched, e.g. "Competitive"
  const [voteEndsAt, setVoteEndsAt] = useState(0);
  const [others, setOthers] = useState<string[]>([]); // maps voted by the other players, one entry per vote
  const [myVote, setMyVote] = useState<string | null>(null);
  const [map, setMap] = useState<string | null>(null); // the vote winner

  const timers = useRef<number[]>([]);
  const later = (fn: () => void, ms: number) => { timers.current.push(window.setTimeout(fn, ms)); };
  const clear = () => { timers.current.forEach(clearTimeout); timers.current = []; };
  useEffect(() => clear, []);

  // latest votes for the end-of-vote timer, without restarting it on every vote
  const votes = useRef({ others, myVote });
  votes.current = { others, myVote };

  const start = useCallback((what: string) => {
    clear();
    setLabel(what);
    setStartedAt(Date.now());
    setPhase("searching");
    later(() => {
      setPhase("found");
      // nobody answered in time: back to idle
      later(() => setPhase("idle"), ACCEPT_SECONDS * 1000);
    }, 6000 + Math.random() * 8000);
  }, []);

  const cancel = useCallback(() => { clear(); setPhase("idle"); }, []);

  const finishVote = () => {
    const { others, myVote } = votes.current;
    const all = myVote ? [...others, myVote] : others;
    const count = new Map<string, number>();
    all.forEach(m => count.set(m, (count.get(m) ?? 0) + 1));
    const top = Math.max(0, ...count.values());
    const leaders = top ? [...count].filter(([, n]) => n === top).map(([m]) => m) : MAP_POOL;
    // ties (and no votes at all) are settled by a random pick among the leaders
    setMap(leaders[Math.floor(Math.random() * leaders.length)]);
    setPhase("connecting");
    later(() => setPhase("idle"), 5000);
  };

  const accept = useCallback(() => {
    clear();
    setOthers([]);
    setMyVote(null);
    setMap(null);
    setVoteEndsAt(Date.now() + VOTE_SECONDS * 1000);
    setPhase("vote");
    for (let i = 0; i < PLAYERS - 1; i++) {
      const pick = MAP_POOL[Math.floor(Math.random() * MAP_POOL.length)];
      later(() => setOthers(v => [...v, pick]), 800 + Math.random() * 9000);
    }
    later(finishVote, VOTE_SECONDS * 1000);
  }, []);

  const vote = useCallback((m: string) => setMyVote(m), []);

  return {
    phase, startedAt, label, voteEndsAt, others, myVote, map,
    start, cancel, accept, decline: cancel, vote,
  };
}
