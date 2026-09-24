import { type Ref } from "react";
import type { Social } from "../hooks/useSocial";
import type { Friend, FriendRequest, SearchResult } from "../lib/friends";
import type { PartyInvite } from "../lib/social";
import { Icon } from "./Icon";

/* Friends and party invites, from the backend, on the one poll they share.

   Presence (online / in game) is not here yet: it is runtime state rather than a
   database column, and arrives with the push channel that replaces the polling. */

function Avatar({ name, url }: { name: string; url: string | null }) {
  return url
    ? <img className="friend-av" src={url} alt="" width={30} height={30} />
    : <span className="friend-av" aria-hidden="true">{name[0]?.toUpperCase() ?? "?"}</span>;
}

/* The invite button appears when there is a seat for them and you are the one
   who may fill it. Hiding it otherwise is a courtesy; the server checks that you
   lead the party, that they are a friend, and that there is room, regardless. */
function FriendRow({ f, canInvite, onInvite, onRemove }: {
  f: Friend; canInvite: boolean; onInvite: () => void; onRemove: () => void;
}) {
  return (
    <li className="friend">
      <Avatar name={f.name} url={f.avatarUrl} />
      <span className="friend-who">
        <b>{f.name}</b>
        <small>{f.rating.toLocaleString()} rating</small>
      </span>
      {canInvite && (
        <button className="invite-btn" onClick={onInvite}
                title={`Invite ${f.name} to your party`} aria-label={`Invite ${f.name} to your party`}>
          <Icon name="plus" size={13} />Invite
        </button>
      )}
      <button className="icon-btn is-danger" onClick={onRemove}
              title={`Remove ${f.name}`} aria-label={`Remove ${f.name}`}>
        <Icon name="unlink" size={15} />
      </button>
    </li>
  );
}

function InviteRow({ i, onJoin, onDecline }: {
  i: PartyInvite; onJoin: () => void; onDecline: () => void;
}) {
  return (
    <li className="friend">
      <Avatar name={i.fromName} url={i.fromAvatarUrl} />
      <span className="friend-who">
        <b>{i.fromName}</b>
        <small>invited you to their party</small>
      </span>
      <button className="invite-btn" onClick={onJoin} aria-label={`Join ${i.fromName}'s party`}>
        <Icon name="check" size={13} />Join
      </button>
      <button className="icon-btn is-danger" onClick={onDecline}
              title="Decline" aria-label={`Decline ${i.fromName}'s party invite`}>
        <Icon name="x" size={15} />
      </button>
    </li>
  );
}

function RequestRow({ r, onAccept, onCancel }: {
  r: FriendRequest; onAccept: () => void; onCancel: () => void;
}) {
  return (
    <li className="friend">
      <Avatar name={r.name} url={r.avatarUrl} />
      <span className="friend-who">
        <b>{r.name}</b>
        <small>{r.incoming ? "wants to be friends" : "request sent"}</small>
      </span>
      {r.incoming && (
        <button className="invite-btn" onClick={onAccept} aria-label={`Accept ${r.name}`}>
          <Icon name="check" size={13} />Accept
        </button>
      )}
      <button className="icon-btn is-danger" onClick={onCancel}
              title={r.incoming ? "Decline" : "Cancel request"}
              aria-label={r.incoming ? `Decline ${r.name}` : `Cancel request to ${r.name}`}>
        <Icon name="x" size={15} />
      </button>
    </li>
  );
}

function ResultRow({ r, onAdd, onAccept }: {
  r: SearchResult; onAdd: () => void; onAccept: () => void;
}) {
  return (
    <li className="friend">
      <Avatar name={r.name} url={r.avatarUrl} />
      <span className="friend-who">
        <b>{r.name}</b>
        <small>{r.rating.toLocaleString()} rating</small>
      </span>
      {/* the button follows the relationship the server reported */}
      {r.relationship === "none" && (
        <button className="invite-btn" onClick={onAdd} aria-label={`Add ${r.name}`}>
          <Icon name="plus" size={13} />Add
        </button>
      )}
      {r.relationship === "incoming" && (
        <button className="invite-btn" onClick={onAccept} aria-label={`Accept ${r.name}`}>
          <Icon name="check" size={13} />Accept
        </button>
      )}
      {r.relationship === "requested" && <span className="friend-state is-wait">Requested</span>}
      {r.relationship === "friends" && (
        <span className="friend-state"><Icon name="check" size={13} />Friends</span>
      )}
      {r.relationship === "self" && <span className="friend-state is-wait">You</span>}
    </li>
  );
}

function Rail({ friends, onExpand, inactive }: {
  friends: Friend[]; onExpand: () => void; inactive: boolean;
}) {
  return (
    <section className="card card-flush friends-rail" inert={inactive}>
      <button className="rail-toggle" onClick={onExpand} title="Show friends" aria-label="Show friends">
        <Icon name="chevron-left" size={16} />
      </button>
      <span className="rail-count" title={`${friends.length} friends`}>
        <Icon name="users" size={17} />
        <b>{friends.length}</b>
      </span>
      <ul className="rail-list">
        {friends.slice(0, 12).map(f => (
          <li key={f.playerId}>
            <button className="rail-av" onClick={onExpand} title={f.name} aria-label={f.name}>
              {f.avatarUrl
                ? <img src={f.avatarUrl} alt="" width={34} height={34} />
                : <>{f.name[0]?.toUpperCase() ?? "?"}</>}
            </button>
          </li>
        ))}
      </ul>
    </section>
  );
}

export function FriendsPanel({ searchRef, collapsed, onToggle, social, seats }: {
  searchRef: Ref<HTMLInputElement>; collapsed: boolean; onToggle: () => void;
  social: Social;
  seats: number;   // how many a party may hold in the mode being played
}) {
  const {
    friends, requests, party, invites, leads, signedOut, error,
    query, setQuery, results, searching,
    add, accept, cancel, remove,
  } = social;

  const looking = query.trim().length >= 2;
  const incoming = requests.filter(r => r.incoming);
  const outgoing = requests.filter(r => !r.incoming);

  // you, your members and the seats your invites are holding open
  const taken = party ? party.members.length + party.invited.length : 1;
  const spoken = new Set([
    ...(party?.members ?? []).map(m => m.playerId),
    ...(party?.invited ?? []).map(m => m.playerId),
  ]);
  const canInvite = (playerId: number) => leads && taken < seats && !spoken.has(playerId);

  // both stay mounted: the column eases between widths while they crossfade,
  // and the hidden one is inert (no focus, no clicks, not read out)
  return (
    <div className="friends-dock" data-open={!collapsed}>
      <Rail friends={friends} onExpand={onToggle} inactive={!collapsed} />
      <section className="card card-flush friends" inert={collapsed}>
        <div className="friends-head">
          <span className="stat-k">Friends</span>
          <span className="friends-tools">
            {/* friend requests and party invites both count: they are what is
                waiting for an answer, and a collapsed panel should say so */}
            <span className="friends-count">
              {incoming.length + invites.length > 0
                ? `${incoming.length + invites.length} waiting`
                : friends.length}
            </span>
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
            placeholder="Find a player by name or Steam ID"
            spellCheck={false}
            disabled={signedOut}
          />
        </label>

        <div className="friends-list">
          {error && <p className="friends-empty">{error}</p>}

          {signedOut ? (
            <p className="friends-empty">Sign in with Steam to add friends.</p>
          ) : looking ? (
            <>
              {searching && results.length === 0 && <p className="friends-empty">Searching…</p>}
              {!searching && results.length === 0 && <p className="friends-empty">Nobody found.</p>}
              {results.length > 0 && (
                <ul>
                  {results.map(r => (
                    <ResultRow key={r.playerId} r={r}
                      onAdd={() => add(r.playerId)} onAccept={() => accept(r.playerId)} />
                  ))}
                </ul>
              )}
            </>
          ) : (
            <>
              {invites.length > 0 && (
                <>
                  <p className="friends-group stat-k">Party invites · {invites.length}</p>
                  <ul>
                    {invites.map(i => (
                      <InviteRow key={i.partyId} i={i}
                        onJoin={() => social.acceptInvite(i.partyId)}
                        onDecline={() => social.declineInvite(i.partyId)} />
                    ))}
                  </ul>
                </>
              )}

              {incoming.length > 0 && (
                <>
                  <p className="friends-group stat-k">Requests · {incoming.length}</p>
                  <ul>
                    {incoming.map(r => (
                      <RequestRow key={r.playerId} r={r}
                        onAccept={() => accept(r.playerId)} onCancel={() => cancel(r.playerId)} />
                    ))}
                  </ul>
                </>
              )}

              {friends.length > 0 && (
                <>
                  <p className="friends-group stat-k">Friends · {friends.length}</p>
                  <ul>
                    {friends.map(f => (
                      <FriendRow key={f.playerId} f={f}
                        canInvite={canInvite(f.playerId)}
                        onInvite={() => social.invite(f.playerId)}
                        onRemove={() => remove(f.playerId)} />
                    ))}
                  </ul>
                </>
              )}

              {outgoing.length > 0 && (
                <>
                  <p className="friends-group stat-k">Sent · {outgoing.length}</p>
                  <ul>
                    {outgoing.map(r => (
                      <RequestRow key={r.playerId} r={r}
                        onAccept={() => accept(r.playerId)} onCancel={() => cancel(r.playerId)} />
                    ))}
                  </ul>
                </>
              )}

              {friends.length === 0 && requests.length === 0 && (
                <p className="friends-empty">No friends yet. Search for someone above.</p>
              )}
            </>
          )}
        </div>
      </section>
    </div>
  );
}
