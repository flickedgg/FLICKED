import { useState, type Ref } from "react";
import { FRIENDS, type Friend } from "../data/demo";
import type { Party } from "../hooks/useParty";
import { Icon } from "./Icon";

function Row({ f, state, canInvite, onInvite }: {
  f: Friend; state: "member" | "pending" | null; canInvite: boolean; onInvite: () => void;
}) {
  return (
    <li className={`friend is-${f.presence}`}>
      <span className="friend-av" aria-hidden="true">{f.name[0].toUpperCase()}<i /></span>
      <span className="friend-who">
        <b>{f.name}</b>
        <small>{f.activity}</small>
      </span>
      {state === "member" ? (
        <span className="friend-state"><Icon name="check" size={13} />In party</span>
      ) : state === "pending" ? (
        <span className="friend-state is-wait">Invited</span>
      ) : f.presence === "online" ? (
        <button className="invite-btn" onClick={onInvite} disabled={!canInvite} aria-label={`Invite ${f.name}`}>
          <Icon name="plus" size={13} />Invite
        </button>
      ) : null}
    </li>
  );
}

export function FriendsPanel({ party, canInvite, searchRef }: {
  party: Party; canInvite: boolean; searchRef: Ref<HTMLInputElement>;
}) {
  const [query, setQuery] = useState("");
  const q = query.trim().toLowerCase();

  const stateOf = (id: string) =>
    party.members.some(m => m.id === id) ? "member" : party.pending.some(p => p.id === id) ? "pending" : null;

  const shown = q ? FRIENDS.filter(f => f.name.toLowerCase().includes(q)) : FRIENDS;
  const online = shown.filter(f => f.presence !== "offline");
  const offline = shown.filter(f => f.presence === "offline");

  const list = (items: Friend[]) => (
    <ul>
      {items.map(f => (
        <Row key={f.id} f={f} state={stateOf(f.id)} canInvite={canInvite} onInvite={() => party.invite(f.id)} />
      ))}
    </ul>
  );

  return (
    <section className="card card-flush friends">
      <div className="friends-head">
        <span className="stat-k">Friends</span>
        <span className="friends-count">{FRIENDS.filter(f => f.presence !== "offline").length} online</span>
      </div>
      <label className="friends-search">
        <Icon name="search" size={14} />
        <input
          ref={searchRef}
          value={query}
          onChange={e => setQuery(e.target.value)}
          placeholder="Search friends"
          spellCheck={false}
        />
      </label>

      <div className="friends-list">
        {online.length > 0 && <><p className="friends-group stat-k">Online · {online.length}</p>{list(online)}</>}
        {offline.length > 0 && <><p className="friends-group stat-k">Offline · {offline.length}</p>{list(offline)}</>}
        {shown.length === 0 && <p className="friends-empty">No friends match “{query}”.</p>}
      </div>
    </section>
  );
}
