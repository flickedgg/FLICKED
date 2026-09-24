import { useMemo } from "react";
import type { Social } from "./useSocial";
import type { PartySeat } from "../lib/social";

export type Party = ReturnType<typeof useParty>;

/* The party, as the Play screen reads it.

   There is no state here any more. The party lives in the database and arrives
   with the friends list on one poll, so this only reshapes what is already
   there: your own seat out of the list (the screen draws it separately), the
   invites your party has out, and how many seats are spoken for.

   Leaving and kicking are the same call, because they are the same row: the
   server works out which one it is from who asked. */
export function useParty(social: Social) {
  const { me, party, leads } = social;

  const members = useMemo<PartySeat[]>(
    () => party?.members.filter(m => m.playerId !== me) ?? [],
    [party, me]);

  const pending = party?.invited ?? [];

  return {
    members,
    pending,
    leads,

    /// you, the members, and the seats held by invites nobody has answered yet
    taken: 1 + members.length + pending.length,

    invite: social.invite,
    cancelInvite: social.cancelInvite,
    kick: social.removeMember,
    leave: () => social.removeMember(me),
  };
}
