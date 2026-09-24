import { type ReactNode } from "react";
import { type Stats } from "../data/demo";
import type { Account } from "../lib/session";
import type { Party } from "../hooks/useParty";
import { Icon } from "./Icon";

function Seat({ name, rating, division, stats, avatarUrl, you, lead, badge, action }: {
  name: string; rating: number; division: string;
  /* Only your own seat has these. K/D and ADR are counted from match rows, which
     is not a thing to work out for four other people on every poll, so the rest
     of the party shows who they are and what they are rated. */
  stats?: Stats;
  avatarUrl?: string | null; you?: boolean;
  lead?: boolean;      // party leader: crown next to the name
  badge?: ReactNode;   // top-left label (Invite sent)
  action?: ReactNode;  // top-right button (kick, cancel)
}) {
  return (
    <>
      {badge}
      {action}
      <div className="card-id">
        {avatarUrl
          ? <img className={`card-av${you ? " is-you" : ""}`} src={avatarUrl} alt="" width={64} height={64} />
          : <span className={`card-av${you ? " is-you" : ""}`} aria-hidden="true">{name[0]?.toUpperCase() ?? "?"}</span>}
        <span className="card-name-row">
          <b className="card-name">{name}</b>
          {lead && <span className="card-crown" title="Party leader"><Icon name="crown" size={15} /><span className="sr-only">Party leader</span></span>}
        </span>
        <span className="card-rating">{rating.toLocaleString()}</span>
        <span className="stat-k">{division}</span>
      </div>
      {stats && (
        <dl className="card-stats">
          <div><dt>K/D</dt><dd>{stats.kd.toFixed(2)}</dd></div>
          <div><dt>Win</dt><dd>{stats.winRate}%</dd></div>
          <div><dt>ADR</dt><dd>{Math.round(stats.adr)}</dd></div>
        </dl>
      )}
    </>
  );
}

function CornerX({ label, onClick }: { label: string; onClick: () => void }) {
  return (
    <button className="card-x" onClick={onClick} aria-label={label} title={label}>
      <Icon name="x" size={13} />
    </button>
  );
}

/* Leader in the middle, the party fans out from them: left, right, far left, far right.
   Two seats (wingman) have no middle, so the leader simply goes first. */
function lineup(leader: ReactNode, others: ReactNode[]) {
  if (others.length !== 4) return [leader, ...others];
  const [a, b, c, d] = others;
  return [c, a, leader, b, d];
}

export function PartyCards({ party, size, locked, onInvite, account }: {
  party: Party; size: number; locked: boolean; onInvite: () => void;
  account: Account | null;   // null until sign-in finishes, or while signed out
}) {
  const empty = Math.max(0, size - party.taken);
  const ratings = [account?.rating ?? 0, ...party.members.map(m => m.rating)];
  const avg = Math.round(ratings.reduce((a, b) => a + b, 0) / ratings.length);

  /* Your own seat. Signed out it shows zeros rather than someone else's numbers:
     the lobby should never look like you are somebody you are not. */
  const leader = (
    <li key="you" className="pcard is-you">
      <Seat
        name={account?.name ?? "Not signed in"}
        rating={account?.rating ?? 0}
        division={account?.division ?? "Sign in with Steam"}
        stats={account?.stats ?? { matches: 0, winRate: 0, kd: 0, adr: 0 }}
        avatarUrl={account?.avatarUrl}
        you
        lead={party.leads}
      />
    </li>
  );

  /* Everyone else, in fill order: members, then pending invites, then open
     seats. Kicking and cancelling are offered only to the leader, who is the
     only one the server would accept them from anyway. */
  const others = [
    ...party.members.map(m => (
      <li key={m.playerId} className="pcard">
        <Seat {...m} action={!locked && party.leads &&
          <CornerX label={`Remove ${m.name} from party`} onClick={() => party.kick(m.playerId)} />} />
      </li>
    )),
    ...party.pending.map(m => (
      <li key={m.playerId} className="pcard is-pending">
        <Seat
          {...m}
          badge={<span className="card-badge"><i className="live-dot" />Invite sent</span>}
          action={party.leads &&
            <CornerX label={`Cancel invite to ${m.name}`} onClick={() => party.cancelInvite(m.playerId)} />}
        />
      </li>
    )),
    ...Array.from({ length: empty }, (_, i) => (
      <li key={`empty-${i}`} className="pcard-cell">
        <button className="pcard is-empty" onClick={onInvite} disabled={locked || !party.leads}>
          <span className="card-plus"><Icon name="plus" size={18} /></span>
          Invite
        </button>
      </li>
    )),
  ];

  return (
    <section className="party" aria-label="Party">
      <div className="party-head">
        <span className="stat-k">Party · {1 + party.members.length}/{size}</span>
        {party.members.length > 0 && <span className="party-avg">Avg {avg.toLocaleString()}</span>}
        <span className="party-tools">
          {party.taken > 1 && !locked && (
            <button className="text-btn" onClick={party.leave}>
              <Icon name="leave" size={14} />Leave party
            </button>
          )}
        </span>
      </div>

      <ul className={`cards cards-${size}`}>{lineup(leader, others)}</ul>
    </section>
  );
}
