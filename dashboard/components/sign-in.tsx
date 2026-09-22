"use client"; // react-icons uses React context

import { useRouter } from "next/navigation";
import { useState } from "react";
import { SiSteam } from "react-icons/si";
import { Logo } from "@/components/brand";
import { signOut, steamLoginUrl, type Account } from "@/lib/api";

/* The sign-in card, and the "wrong account" card: the same thing with a different
   line of text, because they are the same dead end from the visitor's side.

   The Steam button is a link rather than a button with an onClick: signing in is a
   full navigation to Steam, not something fetched in the background. It also works
   before React has hydrated. */
export function SignIn({ returnTo = "/", reason, account }: {
  returnTo?: string;
  reason?: string;
  account?: Account;   // present when signed in as somebody who is not an admin
}) {
  const [busy, setBusy] = useState(false);
  const router = useRouter();

  const leave = async () => {
    setBusy(true);
    try { await signOut(); } finally {
      router.refresh();
      setBusy(false);
    }
  };

  return (
    <main className="grid min-h-screen place-items-center px-6">
      <div className="panel-1 w-full max-w-[420px] rounded-xl p-8 text-center">
        <div className="flex justify-center"><Logo size={44} /></div>

        <h1 className="display h2 mt-6">Dashboard</h1>
        <p className="mt-3 text-[15px] leading-relaxed text-muted">
          {reason ?? "Sign in with the Steam account that administers this instance."}
        </p>

        {account ? (
          /* Without this, signing in with the wrong account traps you: Steam
             remembers the session and sends you straight back here. */
          <button onClick={leave} disabled={busy} className="btn btn-outline btn-lg mt-8 w-full">
            <span>{busy ? "Signing out…" : "Sign out"}</span>
          </button>
        ) : (
          <a href={steamLoginUrl(returnTo)} className="btn btn-primary btn-lg mt-8 w-full">
            <span><SiSteam aria-hidden="true" />Sign in with Steam</span>
          </a>
        )}

        <p className="mt-6 font-mono text-[11.5px] text-faint">
          Admins only. Players use the launcher.
        </p>
      </div>
    </main>
  );
}
