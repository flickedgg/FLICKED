import { useEffect, useRef, useState } from "react";
import { MODES } from "../data/demo";
import type { Queue } from "../hooks/useQueue";
import type { Party } from "../hooks/useParty";
import { PartyCards } from "../components/PartyCards";
import { FriendsPanel } from "../components/FriendsPanel";
import { Elapsed } from "../components/Elapsed";
import { Icon } from "../components/Icon";

const FRIENDS_KEY = "flicked.friends";

export function Play({ queue, party }: { queue: Queue; party: Party }) {
  const [mode, setMode] = useState(MODES[0].id);
  const searching = queue.phase === "searching";
  const current = MODES.find(m => m.id === mode)!;
  const searchRef = useRef<HTMLInputElement>(null);

  // friends list open or collapsed to a rail; remembered between sessions
  const [friendsOpen, setFriendsOpen] = useState(() => {
    try { return localStorage.getItem(FRIENDS_KEY) !== "rail"; } catch { return true; }
  });
  useEffect(() => {
    try { localStorage.setItem(FRIENDS_KEY, friendsOpen ? "open" : "rail"); } catch { /* storage unavailable */ }
  }, [friendsOpen]);

  // an empty party seat opens the list and puts the cursor in search
  const wantSearch = useRef(false);
  useEffect(() => {
    if (friendsOpen && wantSearch.current) { wantSearch.current = false; searchRef.current?.focus(); }
  }, [friendsOpen]);
  const openFriendSearch = () => {
    if (friendsOpen) searchRef.current?.focus();
    else { wantSearch.current = true; setFriendsOpen(true); }
  };

  return (
    <div className={`view play${friendsOpen ? "" : " is-rail"}`}>
      <div className="play-main">
        <header className="play-head">
          <div className="view-head">
            <p className="eyebrow">Matchmaking</p>
            <h1 className="display h1">Find a <span className="accent">match</span></h1>
          </div>

          <div className="seg" role="radiogroup" aria-label="Game mode">
            {MODES.map(m => {
              // a party (pending invites included) cannot queue for a mode with fewer seats
              const tooBig = party.taken > m.size;
              return (
                <button
                  key={m.id}
                  role="radio"
                  aria-checked={mode === m.id}
                  className={`seg-btn${mode === m.id ? " is-on" : ""}`}
                  disabled={searching || tooBig}
                  title={tooBig ? `Too many players for ${m.name} (max ${m.size})` : m.note}
                  onClick={() => setMode(m.id)}
                >
                  {m.name}<small>{m.format.split(" · ")[0]}</small>
                </button>
              );
            })}
          </div>
        </header>

        <div className="stage">
          <PartyCards party={party} size={current.size} locked={searching} onInvite={openFriendSearch} />

          <div className={`launch${searching ? " is-searching" : ""}`}>
            {searching ? (
              <>
                <div className="launch-info">
                  <span className="launch-state"><i className="live-dot" />Searching</span>
                  <span className="launch-sub">{queue.label}</span>
                </div>
                <span className="launch-clock"><Elapsed since={queue.startedAt} /></span>
                <button className="btn btn-outline btn-lg" onClick={queue.cancel}>
                  <Icon name="x" size={16} />Cancel
                </button>
              </>
            ) : (
              <>
                <div className="launch-info">
                  <span className="launch-state">{current.name}</span>
                  <span className="launch-sub">
                    {party.pending.length
                      ? `Waiting for ${party.pending.map(f => f.name).join(", ")} to join`
                      : `${current.format} · ${party.members.length ? `party of ${1 + party.members.length}` : "solo"} · map vote after accept`}
                  </span>
                </div>
                <button className="btn btn-primary btn-xl" disabled={party.pending.length > 0} onClick={() => queue.start(current.name)}>
                  <Icon name="play" size={16} />Find match
                </button>
              </>
            )}
          </div>
        </div>
      </div>

      <aside className="play-side">
        <FriendsPanel
          party={party}
          canInvite={!searching && party.taken < current.size}
          searchRef={searchRef}
          collapsed={!friendsOpen}
          onToggle={() => setFriendsOpen(o => !o)}
        />
      </aside>
    </div>
  );
}
