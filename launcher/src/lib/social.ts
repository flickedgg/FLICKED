import { invoke } from "@tauri-apps/api/core";
import type { Friend, FriendRequest } from "./friends";

/* The side of the Play screen, from the UI's side: friends, requests, your
   party, and the invites waiting for you. One poll for all four, because they
   are drawn together and two polls would eventually draw two different moments.

   Everything here is about you, so it goes through Rust, which holds the session
   token. Each action returns nothing useful: the poll is refetched afterwards
   rather than patched by hand, because the server decides what a party became. */

/// Somebody in a party, or holding a seat in it while they answer.
export type PartySeat = {
  playerId: number;
  name: string;
  avatarUrl: string | null;
  rating: number;
  division: string;
};

export type PartyView = {
  id: number;
  leaderId: number;
  members: PartySeat[];
  invited: PartySeat[];   // seats held by invites nobody has answered yet
};

/// An invite waiting for you, and who asked.
export type PartyInvite = {
  partyId: number;
  fromPlayerId: number;
  fromName: string;
  fromAvatarUrl: string | null;
  expiresAt: string;
};

/* `unchanged` is the server saying 304: the state is what you already have.
   `etag` labels the state that came back and is handed to the next call so the
   server can answer 304 again. */
export type SocialState =
  | { unchanged: true }
  | {
      unchanged?: false;
      etag?: string;
      you: number;
      friends: Friend[];
      requests: FriendRequest[];
      party: PartyView | null;
      invites: PartyInvite[];
    };

export const fetchSocialState = (etag: string | null) =>
  invoke<SocialState | null>("social_state", { etag });

export const invitePlayer = (playerId: number) => invoke<unknown>("party_invite", { playerId });
export const acceptPartyInvite = (partyId: number) => invoke<unknown>("party_accept_invite", { partyId });
export const declinePartyInvite = (partyId: number) => invoke<unknown>("party_decline_invite", { partyId });

export const cancelPartyInvite = (partyId: number, playerId: number) =>
  invoke<unknown>("party_cancel_invite", { partyId, playerId });

/// Leaving, if it is you; removing somebody, if you lead the party.
export const removePartyMember = (playerId: number) =>
  invoke<unknown>("party_remove_member", { playerId });
