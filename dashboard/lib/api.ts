/* Shared with both server and client components, so nothing here may import
   next/headers. Reading the session on the server lives in lib/session.ts. */

/* Where the FLICKED API lives. The dashboard is deployed next to it, so in
   production this is usually the same domain behind a reverse proxy. */
export const API = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5165";

/* Sends the browser to Steam through the backend.

   `returnTo` is where to come back to once Steam has confirmed who you are. It is
   a path, never a full URL: the backend only accepts its own dashboard addresses,
   so a link cannot bounce somebody to another site after signing in. */
export function steamLoginUrl(returnTo = "/") {
  return `${API}/auth/web/login?returnTo=${encodeURIComponent(returnTo)}`;
}

export type Account = {
  id: number;
  name: string;
  steamId: string | null;
  avatarUrl: string | null;
  rating: number;
  division: string;
  isAdmin: boolean;
};

/* Signing out, from the browser. `credentials: "include"` is required: the
   session is a cookie, and browsers only attach cookies cross-origin when asked. */
export async function signOut(): Promise<void> {
  await fetch(`${API}/auth/logout`, { method: "POST", credentials: "include" });
}
