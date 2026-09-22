import { cookies } from "next/headers";
import { API, type Account } from "./api";

/* Server-side session reading.
   Kept apart from lib/api.ts because next/headers only exists on the server, and
   lib/api.ts is imported by client components too. */

/* Who is signed in, or null.

   The catch: this runs on the Next server, not in the browser. A plain fetch from
   here carries no cookies at all, so the API would answer 401 and the dashboard
   would show the sign-in card to somebody who is already signed in. The browser's
   cookies have to be read from the incoming request and passed along by hand. */
export async function getAccount(): Promise<Account | null> {
  const cookie = (await cookies()).toString();
  if (!cookie) return null;

  try {
    const res = await fetch(`${API}/auth/me`, {
      headers: { cookie },
      cache: "no-store",   // a session can be revoked at any time
    });
    return res.ok ? (await res.json() as Account) : null;
  } catch {
    return null;           // API not running: treated as signed out
  }
}
