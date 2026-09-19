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

/* Collapsed friends: a column of online friends. Click one to invite them
   straight from here; the ring shows who is already in the party. */
function FriendsRail({ party, canInvite, stateOf, onExpand, inactive }: {
  party: Party; canInvite: boolean; stateOf: (id: string) => "member" | "pending" | null; onExpand: () => void;
  inactive: boolean;
}) {
  const online = FRIENDS.filter(f => f.presence !== "offline");
  return (
    <section className="card card-flush friends-rail" inert={inactive}>
      <button className="rail-toggle" onClick={onExpand} title="Show friends" aria-label="Show friends">
        <Icon name="chevron-left" size={16} />
      </button>
      <span className="rail-count" title={`${online.length} friends online`}>
        <Icon name="users" size={17} />
        <b>{online.length}</b>
      </span>
      <ul className="rail-list">
        {online.map(f => {
          const state = stateOf(f.id);
          const invitable = f.presence === "online" && !state && canInvite;
          const tip = state === "member" ? `${f.name} · in party`
            : state === "pending" ? `${f.name} · invited`
            : f.presence === "ingame" ? `${f.name} · ${f.activity}`
            : invitable ? `Invite ${f.name}` : f.name;
          return (
            <li key={f.id}>
              <button
                className={`rail-av is-${f.presence}${state ? ` is-${state}` : ""}`}
                onClick={() => party.invite(f.id)}
                disabled={!invitable}
                title={tip}
                aria-label={tip}
              >
                {f.name[0].toUpperCase()}<i />
                {invitable && <span className="rail-plus"><Icon name="plus" size={12} /></span>}
              </button>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

export function FriendsPanel({ party, canInvite, searchRef, collapsed, onToggle }: {
  party: Party; canInvite: boolean; searchRef: Ref<HTMLInputElement>;
  collapsed: boolean; onToggle: () => void;
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

  // both stay mounted: the column eases between widths while they crossfade,
  // and the hidden one is inert (no focus, no clicks, not read out)
  return (
    <div className="friends-dock" data-open={!collapsed}>
      <FriendsRail party={party} canInvite={canInvite} stateOf={stateOf} onExpand={onToggle} inactive={!collapsed} />
      <section className="card card-flush friends" inert={collapsed}>
        <div className="friends-head">
          <span className="stat-k">Friends</span>
          <span className="friends-tools">
            <span className="friends-count">{FRIENDS.filter(f => f.presence !== "offline").length} online</span>
            <button className="icon-btn" onClick={onToggle} title="Collapse friends" aria-label="Collapse friends">
              <Icon name="chevron-right" size={16} />
            </button>
          </span>
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
    </div>
  );
}
