import { useCallback, useEffect, useRef, useState } from "react";
import { MAP_CODE, MAP_NAME } from "../data/demo";
import {
  connectToMatch, queueAccept, queueDecline, queueJoin, queueLeave, queueState, queueVote,
  type QueueState,
} from "../lib/queue";

export type Phase = "idle" | "searching" | "found" | "vote" | "connecting";

export const ACCEPT_SECONDS = 20;
export const VOTE_SECONDS = 15;

export type Queue = ReturnType<typeof useQueue>;

/* The real matchmaking state, polled from the backend.

   The backend owns every decision here: who you are matched with, whether the
   vote has ended, which map won. This hook only asks and renders. It polls
   rather than holding a live connection, which is enough at this size; SignalR
   would replace the polling without changing anything a screen reads.

   Maps travel as server codes ("de_nuke") and are shown as names ("Nuke"), so
   the conversion happens here rather than in five components. */
export function useQueue() {
  const [state, setState] = useState<QueueState | null>(null);
  const [error, setError] = useState<string | null>(null);

  // remembered across phases: the backend only names the mode while searching
  const label = useRef("Competitive");

  /* Matches already connected to, so CS2 is launched once per match rather than
     on every poll. Remembered by match id, not a boolean, so a second match in
     the same session still connects. */
  const connected = useRef(new Set<number>());

  const pull = useCallback(async () => {
    try {
      const next = await queueState();
      setState(next);
      if (next?.mode) label.current = next.mode;
    } catch {
      /* A failed poll is not worth showing: the next one is a second away, and
         a flickering error while queueing would be worse than silence. */
    }
  }, []);

  /* Polls once a second while anything is happening, and every five seconds when
     idle. Idle is the common case (the launcher sits open for hours), and there
     is nothing to learn from asking quickly. */
  useEffect(() => {
    let alive = true;
    let timer: number;

    const loop = async () => {
      if (!alive) return;
      await pull();
      const busy = state?.phase && state.phase !== "idle";
      timer = window.setTimeout(loop, busy ? 1000 : 5000);
    };

    loop();
    return () => { alive = false; clearTimeout(timer); };
  }, [pull, state?.phase]);

  // every action returns the new state, so the screen updates without waiting
  // for the next poll
  const act = useCallback(async (run: () => Promise<QueueState | null>) => {
    setError(null);
    try {
      setState(await run());
    } catch (e) {
      setError(String(e));
    }
  }, []);

  /* When a server is ready, hand it to Steam. Nobody should have to copy an
     address out of a launcher: if CS2 is open it joins, and if it is not, Steam
     starts it. The address is still shown, for when that does not work. */
  useEffect(() => {
    const id = state?.matchId;
    const address = state?.connect;
    if (!id || !address || connected.current.has(id)) return;
    if (state?.phase !== "connecting" && state?.phase !== "live") return;

    connected.current.add(id);
    connectToMatch(address, state.connectPassword ?? null).catch(e => setError(String(e)));
  }, [state?.matchId, state?.connect, state?.phase, state?.connectPassword]);

  const phase: Phase = state?.phase === "live" ? "connecting" : (state?.phase as Phase) ?? "idle";
  const since = state?.since ? Date.parse(state.since) : 0;

  return {
    phase,
    label: label.current,
    error,

    /// when the current phase began; the UI counts up from it
    startedAt: since,
    /// the vote ends this long after it started
    voteEndsAt: since + VOTE_SECONDS * 1000,

    matchId: state?.matchId ?? null,
    accepted: state?.accepted ?? 0,
    needed: state?.needed ?? 10,
    youAccepted: state?.youAccepted ?? false,

    // display names, because that is what the screens show
    myVote: state?.yourVote ? MAP_NAME[state.yourVote] ?? state.yourVote : null,
    map: state?.map ? MAP_NAME[state.map] ?? state.map : null,
    votesFor: (name: string) => state?.votes?.[MAP_CODE[name] ?? name] ?? 0,
    votesCast: Object.values(state?.votes ?? {}).reduce((a, b) => a + b, 0),

    /// "77.83.242.101:27015" once a server is holding the match
    connect: state?.connect ?? null,

    /// join again by hand, if Steam did not pick it up the first time
    joinServer: () => {
      if (state?.connect) connectToMatch(state.connect, state.connectPassword ?? null).catch(e => setError(String(e)));
    },

    start: (mode: string) => act(() => queueJoin(mode)),
    cancel: () => act(queueLeave),
    accept: () => act(queueAccept),
    decline: () => act(queueDecline),
    vote: (name: string) => act(() => queueVote(MAP_CODE[name] ?? name)),
  };
}
