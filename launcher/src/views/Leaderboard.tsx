import { useEffect, useState } from "react";
import { fetchLeaderboard } from "../lib/api";
import { useSession } from "../hooks/useSession";
import type { LeaderRow } from "../data/demo";

export default function Leaderboard() {
  const [rows, setRows] = useState<LeaderRow[]>([]);
  const [failed, setFailed] = useState(false);
  const { account } = useSession();

  useEffect(() => {
    fetchLeaderboard().then(setRows).catch(() => setFailed(true));
  }, []);

  // matched on id, not name: two players can pick the same Steam name
  const you = account ? rows.find(r => r.playerId === account.id) : undefined;

  return (
    <div className="view">
      <header className="view-head">
        <p className="eyebrow">Season 1</p>
        <h1 className="display h1">Leader<span className="accent">board</span></h1>
      </header>

      <div className="table table-lb card card-flush" role="table" aria-label="Leaderboard">
        <div className="tr th" role="row">
          <span role="columnheader">#</span>
          <span role="columnheader">Player</span>
          <span role="columnheader">Rating</span>
          <span role="columnheader">Wins</span>
          <span role="columnheader">Win rate</span>
        </div>
        {rows.map(p => (
          <div
            key={p.playerId}
            className={`tr${p.rank <= 3 ? " is-top" : ""}${p.playerId === account?.id ? " is-you" : ""}`}
            role="row"
          >
            <span role="cell" className="mono rank">{String(p.rank).padStart(2, "0")}</span>
            <span role="cell" className="who">
              {p.name}{p.playerId === account?.id && <> <small className="tag">you</small></>}
            </span>
            <span role="cell" className="mono">{p.rating.toLocaleString()}</span>
            <span role="cell" className="mono">{p.wins}</span>
            <span role="cell" className="mono">{p.winRate}%</span>
          </div>
        ))}

        {/* only when you are signed in but ranked outside the rows above */}
        {account && !you && rows.length > 0 && (
          <div className="tr is-you" role="row">
            <span role="cell" className="mono rank">—</span>
            <span role="cell" className="who">{account.name} <small className="tag">you</small></span>
            <span role="cell" className="mono">{account.rating.toLocaleString()}</span>
            <span role="cell" className="mono">0</span>
            <span role="cell" className="mono">0%</span>
          </div>
        )}
      </div>

      {rows.length === 0 && (
        <p className="news-empty">{failed ? "Could not reach the server." : "No ranked players yet."}</p>
      )}
    </div>
  );
}
