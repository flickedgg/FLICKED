import { Icon, type IconName } from "./Icon";

export type View = "play" | "matches" | "leaderboard" | "news" | "settings";

const NAV: [View, string, IconName][] = [
  ["play", "Play", "play"],
  ["matches", "Matches", "matches"],
  ["leaderboard", "Ranks", "trophy"],
  ["news", "News", "news"],
];

function NavButton({ id, label, icon, current, onSelect }: {
  id: View; label: string; icon: IconName; current: View; onSelect: (v: View) => void;
}) {
  return (
    <button
      className={`nav-btn${current === id ? " is-current" : ""}`}
      aria-current={current === id ? "page" : undefined}
      onClick={() => onSelect(id)}
    >
      <Icon name={icon} size={19} />
      <span>{label}</span>
    </button>
  );
}

export function Sidebar({ current, onSelect }: { current: View; onSelect: (v: View) => void }) {
  return (
    <nav className="sidebar" aria-label="Main">
      {NAV.map(([id, label, icon]) => (
        <NavButton key={id} id={id} label={label} icon={icon} current={current} onSelect={onSelect} />
      ))}
      <div className="sidebar-end">
        <NavButton id="settings" label="Settings" icon="settings" current={current} onSelect={onSelect} />
      </div>
    </nav>
  );
}
