"use client"; // react-icons uses React context

import { SiSteam } from "react-icons/si";
import { Logo } from "@/components/brand";
import { steamLoginUrl } from "@/lib/api";

/* The sign-in card.
   A link rather than a button with an onClick: signing in is a full navigation to
   Steam, not something fetched in the background, so the browser should treat it
   as one. It also works before React has hydrated. */
export function SignIn({ returnTo = "/", reason }: { returnTo?: string; reason?: string }) {
  return (
    <main className="grid min-h-screen place-items-center px-6">
      <div className="panel-1 w-full max-w-[420px] rounded-xl p-8 text-center">
        <div className="flex justify-center"><Logo size={44} /></div>

        <h1 className="display h2 mt-6">Dashboard</h1>
        <p className="mt-3 text-[15px] leading-relaxed text-muted">
          {reason ?? "Sign in with the Steam account that administers this instance."}
        </p>

        <a href={steamLoginUrl(returnTo)} className="btn btn-primary btn-lg mt-8 w-full">
          <span><SiSteam aria-hidden="true" />Sign in with Steam</span>
        </a>

        <p className="mt-6 font-mono text-[11.5px] text-faint">
          Admins only. Players use the launcher.
        </p>
      </div>
    </main>
  );
}
