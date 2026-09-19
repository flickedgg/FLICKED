import { useCallback, useEffect, useRef, useState } from "react";
import { FRIENDS, type Friend } from "../data/demo";

export type Party = ReturnType<typeof useParty>;

/* Party state. You are always the leader for now. Invites are simulated
   (the friend accepts after a moment) until the backend sends party events. */
export function useParty() {
  const [members, setMembers] = useState<Friend[]>([]);   // excludes you
  const [pending, setPending] = useState<Friend[]>([]);   // invited, not answered yet
  const timers = useRef(new Map<string, number>());

  useEffect(() => () => timers.current.forEach(clearTimeout), []);

  const invite = useCallback((id: string) => {
    const friend = FRIENDS.find(f => f.id === id);
    if (!friend) return;
    setPending(p => [...p, friend]);
    timers.current.set(id, window.setTimeout(() => {
      timers.current.delete(id);
      setPending(p => p.filter(f => f.id !== id));
      setMembers(m => [...m, friend]);
    }, 1800 + Math.random() * 1500));
  }, []);

  const cancelInvite = useCallback((id: string) => {
    clearTimeout(timers.current.get(id));
    timers.current.delete(id);
    setPending(p => p.filter(f => f.id !== id));
  }, []);

  const kick = useCallback((id: string) => setMembers(m => m.filter(f => f.id !== id)), []);

  const disband = useCallback(() => {
    timers.current.forEach(clearTimeout);
    timers.current.clear();
    setMembers([]);
    setPending([]);
  }, []);

  // you + members + seats held by pending invites
  const taken = 1 + members.length + pending.length;

  return { members, pending, taken, invite, cancelInvite, kick, disband };
}
