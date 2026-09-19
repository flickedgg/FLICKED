import { useEffect, useRef, type CSSProperties } from "react";
import { MAP_CODE, MAP_POOL } from "../data/demo";
import { ACCEPT_SECONDS, VOTE_SECONDS, type Queue } from "../hooks/useQueue";
import { Countdown } from "./Elapsed";

/* Countdown bars are CSS animations: no timers, no re-renders while they run. */
const bar = (seconds: number) => ({ "--dur": `${seconds}s` } as CSSProperties);

function Found({ queue }: { queue: Queue }) {
  const acceptRef = useRef<HTMLButtonElement>(null);
  useEffect(() => { acceptRef.current?.focus(); }, []);

  return (
    <div className="mf panel-2">
      <p className="eyebrow">{queue.label}</p>
      <h2 id="mf-title" className="display mf-title">Match <span className="accent">found</span></h2>
      <div className="timer-bar" style={bar(ACCEPT_SECONDS)} />
      <div className="mf-actions">
        <button className="btn btn-outline" onClick={queue.decline}>Decline</button>
        <button ref={acceptRef} className="btn btn-primary btn-lg" onClick={queue.accept}>Accept</button>
      </div>
    </div>
  );
}

function Vote({ queue }: { queue: Queue }) {
  const count = (m: string) => queue.others.filter(v => v === m).length + (queue.myVote === m ? 1 : 0);
  const cast = queue.others.length + (queue.myVote ? 1 : 0);
  const top = Math.max(...MAP_POOL.map(count));

  return (
    <div className="vote panel-2">
      <div className="vote-head">
        <div>
          <p className="eyebrow">{queue.label} · map vote</p>
          <h2 id="mf-title" className="display vote-title">Pick the <span className="accent">map</span></h2>
        </div>
        <div className="vote-meta">
          <span className="vote-clock"><Countdown until={queue.voteEndsAt} />s</span>
          <span className="stat-k">{cast}/10 voted</span>
        </div>
      </div>
      <div className="timer-bar" style={bar(VOTE_SECONDS)} />

      <div className="maps" role="radiogroup" aria-label="Vote for a map">
        {MAP_POOL.map(m => {
          const n = count(m);
          const mine = queue.myVote === m;
          return (
            <button
              key={m}
              role="radio"
              aria-checked={mine}
              className={`map-card${mine ? " is-mine" : ""}${n > 0 && n === top ? " is-leading" : ""}`}
              onClick={() => queue.vote(m)}
            >
              <span className="map-code">{MAP_CODE[m]}</span>
              <b className="display map-name">{m}</b>
              <span className="map-votes">
                <span className="map-pips" aria-hidden="true">
                  {Array.from({ length: 10 }, (_, i) => <i key={i} className={i < n ? "on" : undefined} />)}
                </span>
                <span className="map-n">{n}</span>
              </span>
              {mine && <span className="map-mine">Your vote</span>}
            </button>
          );
        })}
      </div>
      <p className="vote-note">Most votes wins. A tie is settled at random.</p>
    </div>
  );
}

function Connecting({ queue }: { queue: Queue }) {
  return (
    <div className="mf panel-2">
      <p className="eyebrow">Map selected · {queue.map && MAP_CODE[queue.map]}</p>
      <h2 id="mf-title" className="display mf-title">{queue.map}</h2>
      <p className="mf-note"><i className="live-dot" />Starting your server and connecting all 10 players…</p>
    </div>
  );
}

export function MatchFlow({ queue }: { queue: Queue }) {
  return (
    <div className="overlay" role="dialog" aria-modal="true" aria-labelledby="mf-title">
      {queue.phase === "found" && <Found queue={queue} />}
      {queue.phase === "vote" && <Vote queue={queue} />}
      {queue.phase === "connecting" && <Connecting queue={queue} />}
    </div>
  );
}
