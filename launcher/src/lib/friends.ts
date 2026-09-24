import { invoke } from "@tauri-apps/api/core";

/* Friends. Every call here is about you, so it goes through Rust, which holds
   the session token; the backend works out who you are from it.

   Each function returns null when nobody is signed in, and rejects with the
   server's own message ("You are already friends.") when an action is refused. */

export type Friend = {
  playerId: number;
  name: string;
  avatarUrl: string | null;
  rating: number;
};

export type FriendRequest = {
  playerId: number;
  name: string;
  avatarUrl: string | null;
  createdAt: string;
  incoming: boolean;   // true when they asked you, false when you asked them
};

// what the launcher should offer for this person
export type Relationship = "none" | "requested" | "incoming" | "friends" | "self";

export type SearchResult = Omit<Friend, never> & { relationship: Relationship };

/* The list itself is not fetched here: friends, requests and party state are
   drawn on one panel and arrive together from lib/social.ts. */

export const searchPlayers = (query: string) => invoke<SearchResult[] | null>("search_players", { query });

export const addFriend = (playerId: number) => invoke<unknown>("add_friend", { playerId });
export const acceptFriend = (playerId: number) => invoke<unknown>("accept_friend", { playerId });
export const removeRequest = (playerId: number) => invoke<unknown>("remove_friend_request", { playerId });
export const removeFriend = (playerId: number) => invoke<unknown>("remove_friend", { playerId });
