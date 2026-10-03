import { redirect } from "next/navigation";

/* The dashboard front door.

   There is no page of its own here: the dashboard is a set of pages now, and an
   overview that only linked to them would be a click on the way to the one
   anybody actually opens. Servers is that one. The sign-in gate lives in
   DashShell, so this redirect happens whether or not anybody is signed in — the
   admin check then runs on /servers, where it can send them back afterwards. */
export default function Home() {
  redirect("/servers");
}
