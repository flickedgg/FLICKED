import { useCallback, useEffect, useState } from "react";
import { type Account, currentAccount, signIn, signOut } from "../lib/session";

export type Session = ReturnType<typeof useSession>;

/* The signed-in account, if there is one. Checked once on start-up: the token
   lives in the OS keychain and may have expired while the launcher was closed,
   so only the backend can say whether it still works. */
export function useSession() {
  const [account, setAccount] = useState<Account | null>(null);
  const [busy, setBusy] = useState(true);   // true until the first check finishes
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    currentAccount()
      .then(setAccount)
      .catch(() => {})            // offline at start-up is not worth an error here
      .finally(() => setBusy(false));
  }, []);

  const begin = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      setAccount(await signIn());
    } catch (e) {
      // Rust returns plain strings, already written for people to read
      setError(String(e));
    } finally {
      setBusy(false);
    }
  }, []);

  const end = useCallback(async () => {
    setBusy(true);
    try { await signOut(); } catch { /* the token is cleared locally regardless */ }
    setAccount(null);
    setBusy(false);
  }, []);

  return { account, busy, error, begin, end };
}
