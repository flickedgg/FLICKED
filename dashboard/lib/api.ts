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

/* Who is signed in, or null.

   `credentials: "include"` is the whole point: the session is an HttpOnly cookie,
   which the browser only attaches when asked to. Without it every call here would
   look signed out. */
export async function fetchAccount(): Promise<Account | null> {
  try {
    const res = await fetch(`${API}/auth/me`, {
      credentials: "include",
      cache: "no-store",
    });
    return res.ok ? (await res.json() as Account) : null;
  } catch {
    return null;   // API not running, or no network: treated as signed out
  }
}

export async function signOut(): Promise<void> {
  await fetch(`${API}/auth/logout`, { method: "POST", credentials: "include" });
}
