"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { signOut, type Account } from "@/lib/api";

/* Who you are signed in as, and the way out.

   A client component because signing out is an action: it calls the API, then
   asks Next to re-render the page from the server, which now sees no cookie and
   shows the sign-in card. router.refresh() rather than a full reload keeps it
   quick and avoids a flash of the whole page. */
export function AccountMenu({ account }: { account: Account }) {
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
    <span className="flex items-center gap-3">
      {account.avatarUrl && (
        /* eslint-disable-next-line @next/next/no-img-element -- Steam CDN, not in next.config */
        <img src={account.avatarUrl} alt="" width={32} height={32} className="rounded-md" />
      )}
      <span className="leading-tight">
        <b className="block text-[14px] font-medium text-foreground">{account.name}</b>
        <small className="block font-mono text-[11px] text-faint">Admin</small>
      </span>
      <button onClick={leave} disabled={busy} className="btn btn-outline ms-2 h-9 px-4 text-[13px]">
        <span>{busy ? "Signing out…" : "Sign out"}</span>
      </button>
    </span>
  );
}
