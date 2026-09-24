import { useCallback, useEffect, useRef, useState } from "react";
import {
  acceptFriend, addFriend, fetchFriendsState, removeFriend, removeRequest,
  searchPlayers, type Friend, type FriendRequest, type SearchResult,
} from "../lib/friends";

/* How often to ask, in milliseconds.

   Nobody expects a friend request to arrive instantly, and the cost of asking is
   paid by the server once per client per interval, forever. Five seconds while
   you are looking at the launcher is quick enough to feel live; a launcher left
   open behind a game all evening backs off to a minute, which is the difference
   between one client costing 720 requests an hour and 60.

   An unchanged answer is a 304 with no body, so most of these cost a request and
   two indexed queries and nothing else. */
const ACTIVE = 5_000;
const IDLE = 60_000;

export type Friends = ReturnType<typeof useFriends>;

/* The friends list, pending requests, and searching for people to add.

   Every action reloads the list afterwards rather than patching state by hand:
   the server decides what a request became (asking back someone who already
   asked you makes you friends immediately), so asking it is more honest than
   guessing here. */
export function useFriends() {
  const [friends, setFriends] = useState<Friend[]>([]);
  const [requests, setRequests] = useState<FriendRequest[]>([]);
  const [signedOut, setSignedOut] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [query, setQuery] = useState("");
  const [results, setResults] = useState<SearchResult[]>([]);
  const [searching, setSearching] = useState(false);

  /* The ETag of the state on screen. Kept in a ref rather than state because
     changing it must never cause a render: it is bookkeeping for the next
     request, not something anyone sees. */
  const etag = useRef<string | null>(null);

  /* In flight already? Then skip this tick.

     A slow answer must not stack requests behind it, which is how a struggling
     server gets a queue of clients all asking again while it is busy. */
  const busy = useRef(false);

  const reload = useCallback(async (force = false) => {
    /* A poll may be skipped while another is in flight; an action may not. You
       pressed Accept, and the answer to "what is the list now" cannot be "a
       request was already running, so nothing happened". */
    if (busy.current && !force) return;
    busy.current = true;
    try {
      const state = await fetchFriendsState(force ? null : etag.current);

      if (state === null) { setSignedOut(true); return; }
      setSignedOut(false);

      // 304: what is on screen is current, so nothing is set and nothing renders.
      if (state.unchanged) return;

      etag.current = state.etag ?? null;
      setFriends(state.friends);
      setRequests(state.requests);
    } finally {
      busy.current = false;
    }
  }, []);

  /* Poll while the panel is mounted.

     The interval follows the window: a launcher nobody is looking at asks a
     fraction as often, and asks immediately when it comes back, so returning to
     the launcher shows current state rather than whatever the last slow tick
     left behind. */
  useEffect(() => {
    let alive = true;
    let timer: number;

    const tick = async () => {
      if (!alive) return;
      await reload().catch(() => setError("Could not reach the server."));
      if (!alive) return;
      timer = window.setTimeout(tick, document.hasFocus() ? ACTIVE : IDLE);
    };

    const wake = () => {
      clearTimeout(timer);
      tick();
    };

    tick();
    window.addEventListener("focus", wake);

    return () => {
      alive = false;
      clearTimeout(timer);
      window.removeEventListener("focus", wake);
    };
  }, [reload]);

  /* Search runs 250ms after typing stops.*/
  const timer = useRef<number>(undefined);
  useEffect(() => {
    const q = query.trim();
    clearTimeout(timer.current);

    if (q.length < 2) { setResults([]); setSearching(false); return; }

    setSearching(true);
    timer.current = window.setTimeout(() => {
      searchPlayers(q)
        .then(found => setResults(found ?? []))
        .catch(e => setError(String(e)))
        .finally(() => setSearching(false));
    }, 250);

    return () => clearTimeout(timer.current);
  }, [query]);

  const act = useCallback(async (run: () => Promise<unknown>) => {
    setError(null);
    try {
      await run();
      await reload(true);   // your own action: ask for the state, not a 304
      if (query.trim().length >= 2) {
        setResults(await searchPlayers(query.trim()) ?? []);
      }
    } catch (e) {
      setError(String(e));
    }
  }, [reload, query]);

  return {
    friends, requests, signedOut, error,
    query, setQuery, results, searching,
    add: (id: number) => act(() => addFriend(id)),
    accept: (id: number) => act(() => acceptFriend(id)),
    cancel: (id: number) => act(() => removeRequest(id)),
    remove: (id: number) => act(() => removeFriend(id)),
    reload,
  };
}
