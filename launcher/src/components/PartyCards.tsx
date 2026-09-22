import { useState, type ReactNode } from "react";
import { type Stats } from "../data/demo";
import type { Account } from "../lib/session";
import type { Party } from "../hooks/useParty";
import { Icon } from "./Icon";

const PARTY_CODE = "FLK-7Q2X"; // demo; issued by the backend later

function Seat({ name, rating, division, stats, avatarUrl, you, lead, badge, action }: {
  name: string; rating: number; division: string; stats: Stats;
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
      <dl className="card-stats">
        <div><dt>K/D</dt><dd>{stats.kd.toFixed(2)}</dd></div>
        <div><dt>Win</dt><dd>{stats.winRate}%</dd></div>
        <div><dt>ADR</dt><dd>{Math.round(stats.adr)}</dd></div>
      </dl>
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
  const [copied, setCopied] = useState(false);
  const empty = Math.max(0, size - party.taken);
  const ratings = [account?.rating ?? 0, ...party.members.map(m => m.rating)];
  const avg = Math.round(ratings.reduce((a, b) => a + b, 0) / ratings.length);

  const copyCode = () => {
    navigator.clipboard?.writeText(PARTY_CODE).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    }, () => {});
  };

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
        lead
      />
    </li>
  );

  // everyone else, in fill order: members, then pending invites, then open seats
  const others = [
    ...party.members.map(f => (
      <li key={f.id} className="pcard">
        <Seat {...f} action={!locked && <CornerX label={`Remove ${f.name} from party`} onClick={() => party.kick(f.id)} />} />
      </li>
    )),
    ...party.pending.map(f => (
      <li key={f.id} className="pcard is-pending">
        <Seat
          {...f}
          badge={<span className="card-badge"><i className="live-dot" />Invite sent</span>}
          action={<CornerX label={`Cancel invite to ${f.name}`} onClick={() => party.cancelInvite(f.id)} />}
        />
      </li>
    )),
    ...Array.from({ length: empty }, (_, i) => (
      <li key={`empty-${i}`} className="pcard-cell">
        <button className="pcard is-empty" onClick={onInvite} disabled={locked}>
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
          <button className="code" onClick={copyCode} title="Friends can join with this code">
            <Icon name={copied ? "check" : "copy"} size={13} />
            {copied ? "Copied" : PARTY_CODE}
          </button>
          {party.taken > 1 && !locked && (
            <button className="text-btn" onClick={party.disband}>
              <Icon name="leave" size={14} />Disband
            </button>
          )}
        </span>
      </div>

      <ul className={`cards cards-${size}`}>{lineup(leader, others)}</ul>
    </section>
  );
}
