import { useCallback, useEffect, useRef, useState } from "react";
import {
  acceptFriend, addFriend, fetchFriends, fetchRequests, removeFriend, removeRequest,
  searchPlayers, type Friend, type FriendRequest, type SearchResult,
} from "../lib/friends";

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

  const reload = useCallback(async () => {
    const [list, pending] = await Promise.all([fetchFriends(), fetchRequests()]);
    if (list === null || pending === null) { setSignedOut(true); return; }
    setSignedOut(false);
    setFriends(list);
    setRequests(pending);
  }, []);

  useEffect(() => { reload().catch(() => setError("Could not reach the server.")); }, [reload]);

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
      await reload();
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
