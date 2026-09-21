import { useEffect, useState } from "react";
import { fetchMatches } from "../lib/api";
import { MAP_NAME, type MatchRow } from "../data/demo";
import { timeAgo } from "../lib/time";

export default function Matches() {
  const [matches, setMatches] = useState<MatchRow[]>([]);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    fetchMatches().then(setMatches).catch(() => setFailed(true));
  }, []);

  return (
    <div className="view">
      <header className="view-head">
        <p className="eyebrow">History</p>
        <h1 className="display h1">Matches</h1>
      </header>

      <div className="table card card-flush" role="table" aria-label="Match history">
        <div className="tr th" role="row">
          <span role="columnheader">Result</span>
          <span role="columnheader">Map</span>
          <span role="columnheader">Score</span>
          <span role="columnheader">K / D</span>
          <span role="columnheader">ADR</span>
          <span role="columnheader">Rating</span>
          <span role="columnheader">Match</span>
        </div>
        {matches.map(m => (
          <div key={m.id} className="tr" role="row">
            <span role="cell"><span className={`result is-${m.result}`}>{m.result}</span></span>
            {/* the API talks in map codes; the name is what players call it */}
            <span role="cell" className="recent-map">{MAP_NAME[m.map] ?? m.map}<small>{timeAgo(m.playedAt)}</small></span>
            <span role="cell" className="mono">{m.score}</span>
            <span role="cell" className="mono">{m.kd}</span>
            <span role="cell" className="mono">{m.adr}</span>
            <span role="cell" className={`mono delta ${m.delta > 0 ? "is-up" : "is-down"}`}>
              {m.delta > 0 ? "+" : "−"}{Math.abs(m.delta)}
            </span>
            <span role="cell" className="mono faint">#{m.id}</span>
          </div>
        ))}
      </div>

      {matches.length === 0 && (
        <p className="news-empty">{failed ? "Could not reach the server." : "No matches yet."}</p>
      )}
    </div>
  );
}
