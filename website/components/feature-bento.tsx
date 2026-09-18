/* Feature cards: an icon, a plain title, one sentence, and a small labelled
   picture of the feature. Every abbreviation is spelled out. All figures are
   illustrative. */

const ICONS = {
  queue: <><circle cx="9" cy="8" r="3" /><circle cx="17" cy="9" r="2.5" /><path d="M3 20c0-3.3 2.7-6 6-6s6 2.7 6 6M15 14.2c.6-.1 1.3-.2 2-.2 2.8 0 5 2 5 4.6" /></>,
  server: <><rect x="3" y="4" width="18" height="7" rx="2" /><rect x="3" y="13" width="18" height="7" rx="2" /><path d="M7 7.5h.01M7 16.5h.01" /></>,
  trophy: <><path d="M8 4h8v5a4 4 0 0 1-8 0V4Z" /><path d="M8 6H5a3 3 0 0 0 3 4M16 6h3a3 3 0 0 1-3 4M12 13v4M8 20h8" /></>,
  replay: <><rect x="3" y="5" width="18" height="14" rx="2" /><path d="m10 9 5 3-5 3V9Z" /></>,
  stats: <><path d="M4 20V10M10 20V4M16 20v-7M22 20H2" /></>,
};

function Icon({ name }: { name: keyof typeof ICONS }) {
  return (
    <span className="feature-icon">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
        {ICONS[name]}
      </svg>
    </span>
  );
}

function Card({ icon, title, body, className = "", children }: {
  icon: keyof typeof ICONS; title: string; body: string; className?: string; children: React.ReactNode;
}) {
  return (
    <article className={`bento-card flex flex-col ${className}`}>
      <Icon name={icon} />
      <h3 className="mt-5 text-[18px] font-semibold tracking-[-0.01em] text-foreground">{title}</h3>
      <p className="mt-2 max-w-[48ch] text-[14.5px] leading-relaxed text-muted">{body}</p>
      <div className="mt-auto pt-7">{children}</div>
    </article>
  );
}

/* ── the pictures ── */

const STEPS = [
  ["Join the queue", "10 of 10 players found"],
  ["Accept", "Everyone is ready"],
  ["Pick the map", "Mirage"],
  ["Play", "Connecting to server…"],
] as const;

function MatchFlow() {
  return (
    <ol className="flow-steps">
      {STEPS.map(([step, detail], i) => {
        const current = i === STEPS.length - 1;
        return (
          <li key={step} className={current ? "is-current" : "is-done"}>
            <span className="flow-dot" aria-hidden="true">{current ? i + 1 : "✓"}</span>
            <span className="min-w-0">
              <b>{step}</b>
              <small>{detail}</small>
            </span>
          </li>
        );
      })}
    </ol>
  );
}

function ServerList() {
  return (
    <ul className="mini-list">
      {[
        ["Match 48214", "Starting", "is-starting"],
        ["Match 48213", "Live", "is-live"],
        ["Match 48210", "Finished", "is-off"],
      ].map(([match, status, state]) => (
        <li key={match} className="flex items-center justify-between gap-4">
          <span className="text-foreground">{match}</span>
          <span className={`status ${state}`}><i aria-hidden="true" />{status}</span>
        </li>
      ))}
    </ul>
  );
}

const BOARD = "grid grid-cols-[20px_1fr_auto_52px] items-center gap-3";

function Leaderboard() {
  return (
    <div>
      <div className={`mini-head ${BOARD}`}>
        <span>#</span><span>Player</span><span>Rating</span><span className="text-end">Change</span>
      </div>
      <ol className="mini-list">
        {[
          ["kovac", "2,114", "+18"],
          ["Halden", "2,087", "+9"],
          ["Nyx", "2,031", "−12"],
        ].map(([name, rating, delta], i) => (
          <li key={name} className={BOARD}>
            <span className={i === 0 ? "text-primary-light" : "text-faint"}>{i + 1}</span>
            <span className="text-foreground">{name}</span>
            <span className="tabular-nums text-muted">{rating}</span>
            <span className={`text-end tabular-nums ${delta.startsWith("+") ? "text-primary-light" : "text-faint"}`}>{delta}</span>
          </li>
        ))}
      </ol>
    </div>
  );
}

function Replay() {
  return (
    <div className="rounded-lg border border-white/[0.07] bg-black/20 p-4">
      <div className="flex items-center gap-3">
        <span className="play" aria-hidden="true">
          <svg viewBox="0 0 24 24" fill="currentColor"><path d="M8 5v14l11-7L8 5Z" /></svg>
        </span>
        <span className="min-w-0">
          <b className="block truncate text-[13.5px] font-medium text-foreground">Ember vs Slate · Mirage</b>
          <small className="block text-[12px] text-subtle">Round 14 of 20</small>
        </span>
      </div>
      <div className="progress mt-4" role="img" aria-label="Playback at round 14 of 20">
        <i style={{ width: "70%" }} />
      </div>
    </div>
  );
}

function PlayerStats() {
  return (
    <dl className="grid grid-cols-3 gap-2.5">
      {[
        ["97", "Damage per round"],
        ["58%", "Headshots"],
        ["1.75", "Kills per death"],
      ].map(([value, label]) => (
        <div key={label} className="stat-tile flex flex-col-reverse justify-end">
          <dt className="mt-1 text-[12px] leading-snug text-subtle">{label}</dt>
          <dd className="text-[22px] font-semibold tabular-nums tracking-[-0.02em] text-foreground">{value}</dd>
        </div>
      ))}
    </dl>
  );
}

export function FeatureBento() {
  return (
    <div className="bento mt-14" data-rv-panel>
      <Card
        icon="queue"
        title="Matchmaking"
        body="Players join a queue and get matched with others at their level. Everyone accepts, picks the map, and connects automatically."
        className="bento-wide"
      >
        <MatchFlow />
      </Card>
      <Card
        icon="server"
        title="Servers start themselves"
        body="When a match is ready, FLICKED starts a CS2 server for it and shuts it down after. No admin needed."
      >
        <ServerList />
      </Card>
      <Card
        icon="trophy"
        title="Rankings"
        body="Every player has a rating. It goes up when they win and down when they lose."
      >
        <Leaderboard />
      </Card>
      <Card
        icon="replay"
        title="Match replays"
        body="Every match is recorded, so you can rewatch it, clip highlights or check a report."
      >
        <Replay />
      </Card>
      <Card
        icon="stats"
        title="Player stats"
        body="Kills, deaths, damage and more for every round, saved in your own database."
      >
        <PlayerStats />
      </Card>
    </div>
  );
}
