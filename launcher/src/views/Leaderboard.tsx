import { LEADERBOARD, PLAYER } from "../data/demo";

export default function Leaderboard() {
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
        {LEADERBOARD.map(p => (
          <div key={p.rank} className={`tr${p.rank <= 3 ? " is-top" : ""}`} role="row">
            <span role="cell" className="mono rank">{String(p.rank).padStart(2, "0")}</span>
            <span role="cell" className="who">{p.name}</span>
            <span role="cell" className="mono">{p.rating.toLocaleString()}</span>
            <span role="cell" className="mono">{p.wins}</span>
            <span role="cell" className="mono">{p.winRate}%</span>
          </div>
        ))}
        <div className="tr is-you" role="row">
          <span role="cell" className="mono rank">412</span>
          <span role="cell" className="who">{PLAYER.name} <small className="tag">you</small></span>
          <span role="cell" className="mono">{PLAYER.rating.toLocaleString()}</span>
          <span role="cell" className="mono">{Math.round(PLAYER.stats.matches * PLAYER.stats.winRate / 100)}</span>
          <span role="cell" className="mono">{PLAYER.stats.winRate}%</span>
        </div>
      </div>
    </div>
  );
}
