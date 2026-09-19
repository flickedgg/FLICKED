import { RECENT } from "../data/demo";

export default function Matches() {
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
        {RECENT.map(m => (
          <div key={m.id} className="tr" role="row">
            <span role="cell"><span className={`result is-${m.result}`}>{m.result}</span></span>
            <span role="cell" className="recent-map">{m.map}<small>{m.when}</small></span>
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
    </div>
  );
}
