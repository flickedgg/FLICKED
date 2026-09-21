import { invoke, isTauri } from "@tauri-apps/api/core";

/* Signing in, from the UI's side. Rust owns the Steam flow and the session token
   (src-tauri/src/auth.rs); everything here works in names and ids only. There is
   deliberately no way to read the token from JavaScript. */

export type Account = {
  id: number;
  name: string;
  steamId: string | null;
  rating: number;
};

// Opens Steam in the browser and resolves once the player comes back.
export const signIn = () => invoke<Account>("steam_login");

// Who the stored token belongs to, or null. Rust asks the backend, so a revoked
// or expired session answers null rather than looking signed in.
export const currentAccount = () =>
  isTauri() ? invoke<Account | null>("current_account") : Promise.resolve(null);

export const signOut = () => invoke<void>("logout");
