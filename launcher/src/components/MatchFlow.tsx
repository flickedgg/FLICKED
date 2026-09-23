import { useEffect, useRef, type CSSProperties } from "react";
import { MAP_CODE, MAP_POOL } from "../data/demo";
import { MAP_IMAGE } from "../lib/maps";
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
      <p className="mf-note">{queue.accepted} of {queue.needed} accepted</p>
      <div className="timer-bar" style={bar(ACCEPT_SECONDS)} />
      <div className="mf-actions">
        <button className="btn btn-outline" onClick={queue.decline}>Decline</button>
        <button ref={acceptRef} className="btn btn-primary btn-lg"
                onClick={queue.accept} disabled={queue.youAccepted}>
          {queue.youAccepted ? "Waiting for others…" : "Accept"}
        </button>
      </div>
    </div>
  );
}

function Vote({ queue }: { queue: Queue }) {
  // tallies come from the backend: every player's vote, counted server-side
  const count = (m: string) => queue.votesFor(m);
  const cast = queue.votesCast;
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
              {/* the art sits behind the card's own gradient, so the text on top
                  keeps its contrast whatever the screenshot looks like */}
              <img className="map-art" src={MAP_IMAGE[m]} alt="" loading="lazy" />
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
      {queue.connect && (
        /* CS2 is handed the server automatically; this is for when that does not
           take, or when the game was closed and Steam is still starting. */
        <div className="mf-join">
          <button className="btn btn-primary btn-lg" onClick={queue.joinServer}>Join the server</button>
        </div>
      )}
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
