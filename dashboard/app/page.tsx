import { SignIn } from "@/components/sign-in";
import { fetchAccount } from "@/lib/api";

/* The dashboard front door. Three outcomes:
     not signed in          → sign in with Steam
     signed in, not admin   → told so plainly
     admin                  → the dashboard itself

   Checked on the server on every request (no caching), so a revoked session or a
   removed admin flag takes effect at once rather than on the next reload. */
export const dynamic = "force-dynamic";

export default async function Home() {
  const account = await fetchAccount();

  if (!account) return <SignIn />;

  if (!account.isAdmin) {
    return (
      <SignIn
        reason={`Signed in as ${account.name}, which is not an admin account on this instance.`}
      />
    );
  }

  return (
    /* data-tone picks the background mood (see components/ambient.tsx); a
       dashboard wants the quiet one, not the landing page's hero glow */
    <main>
      <section data-tone="quiet" className="mx-auto max-w-frame px-6 py-16 lg:px-10">
        <header className="flex items-center justify-between gap-6">
          <div>
            <p className="eyebrow">Dashboard</p>
            <h1 className="display h1 mt-4">Servers</h1>
          </div>
          <span className="flex items-center gap-3">
            {account.avatarUrl && (
              /* eslint-disable-next-line @next/next/no-img-element -- Steam CDN, not in next.config */
              <img src={account.avatarUrl} alt="" width={32} height={32} className="rounded-md" />
            )}
            <span className="text-[14px] text-foreground">{account.name}</span>
          </span>
        </header>

        <p className="mt-10 text-[15px] text-muted">
          Nothing here yet. Server registration comes next.
        </p>
      </section>
    </main>
  );
}
