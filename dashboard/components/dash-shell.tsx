import { AccountMenu } from "@/components/account-menu";
import { DashNav } from "@/components/dash-nav";
import { SignIn } from "@/components/sign-in";
import { getAccount } from "@/lib/session";

/* The frame every dashboard page sits in, and the admin gate.

   Three outcomes, as before:
     not signed in          → sign in with Steam
     signed in, not admin   → told so plainly, with a way out
     admin                  → the page

   It lives in a component rather than app/layout.tsx because the gate needs to
   know which page was asked for: `returnTo` is where Steam sends somebody back
   to, and a layout cannot early-return a sign-in card for the route below it.

   Checked on the server on every request (each page sets force-dynamic), so a
   revoked session or a removed admin flag takes effect at once rather than on
   the next reload. */
export async function DashShell({ page, title, children }: {
  page: "/servers" | "/news";
  title: string;
  children: React.ReactNode;
}) {
  const account = await getAccount();

  if (!account) return <SignIn returnTo={page} />;

  /* Signed in but not an admin. The sign-out button matters here: without it
     somebody who signed in with the wrong Steam account would be stuck, since
     Steam remembers them and signing in again lands in the same place. */
  if (!account.isAdmin) {
    return (
      <SignIn
        returnTo={page}
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
            <h1 className="display h1 mt-4">{title}</h1>
          </div>
          <AccountMenu account={account} />
        </header>

        <DashNav />
        {children}
      </section>
    </main>
  );
}
