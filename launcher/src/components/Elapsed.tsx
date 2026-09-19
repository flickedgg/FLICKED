import { useEffect, useState } from "react";

export const mmss = (s: number) => `${Math.floor(s / 60)}:${String(s % 60).padStart(2, "0")}`;

/* These tick on their own so a running clock re-renders one text node, not the page. */
function useSecondTick() {
  const [, tick] = useState(0);
  useEffect(() => {
    const id = setInterval(() => tick(n => n + 1), 1000);
    return () => clearInterval(id);
  }, []);
}

export function Countdown({ until }: { until: number }) {
  useSecondTick();
  return <span className="tabular">{Math.max(0, Math.ceil((until - Date.now()) / 1000))}</span>;
}

export function Elapsed({ since }: { since: number }) {
  useSecondTick();
  return <span className="tabular">{mmss(Math.max(0, Math.floor((Date.now() - since) / 1000)))}</span>;
}
