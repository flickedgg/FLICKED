import { AccountMenu } from "@/components/account-menu";
import { SignIn } from "@/components/sign-in";
import { getAccount } from "@/lib/session";

/* The dashboard front door. Three outcomes:
     not signed in          → sign in with Steam
     signed in, not admin   → told so plainly, with a way out
     admin                  → the dashboard itself

   Checked on the server on every request (no caching), so a revoked session or a
   removed admin flag takes effect at once rather than on the next reload. */
export const dynamic = "force-dynamic";

export default async function Home() {
  const account = await getAccount();

  if (!account) return <SignIn />;

  /* Signed in but not an admin. The sign-out button matters here: without it
     somebody who signed in with the wrong Steam account would be stuck, since
     Steam remembers them and signing in again lands in the same place. */
  if (!account.isAdmin) {
    return (
      <SignIn
        account={account}
        reason={`Signed in as ${account.name}, which is not an admin on this instance.`}
      />
    );
  }

  return (
    /* data-tone picks the background mood (see components/ambient.tsx); a
       dashboard wants the quiet one, not the landing page's hero glow */
    <main>
      <section data-tone="quiet" className="mx-auto max-w-frame px-6 py-16 lg:px-10">
        <header className="flex flex-wrap items-center justify-between gap-6">
          <div>
            <p className="eyebrow">Dashboard</p>
            <h1 className="display h1 mt-4">Servers</h1>
          </div>
          <AccountMenu account={account} />
        </header>

        <p className="mt-10 text-[15px] text-muted">
          Nothing here yet. Server registration comes next.
        </p>
      </section>
    </main>
  );
}
