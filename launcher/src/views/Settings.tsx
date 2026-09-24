import { useEffect, useState } from "react";
import { applyMotion, storedMotion, type Motion } from "../lib/motion";
import { PREFS_EVENT, PREFS_KEY } from "../lib/prefs";
import { AccountCard } from "../components/AccountCard";
import { openExternal } from "../lib/openExternal";

const REPO = "https://github.com/viix0dev/FLICKED";

type Prefs = {
  startWithWindows: boolean;
  minimizeToTray: boolean;
  closeOnLaunch: boolean;
  discordActivity: boolean;
  motion: Motion;
};

const DEFAULTS: Prefs = {
  startWithWindows: false,
  minimizeToTray: true,
  closeOnLaunch: false,
  discordActivity: true,
  motion: "system",
};


// kept in localStorage for now; moves to a Rust-side store once settings drive real behaviour
function load(): Prefs {
  try {
    /* cs2Path and launchOptions were the Game section, removed because Steam
       already owns both: CS2 is launched through steam://, which finds the
       install itself and applies the launch options set in Steam. Discarded on
       read so a value nobody can change no longer rides along in storage. */
    const { reduceMotion: _old, cs2Path: _path, launchOptions: _opts, ...saved } =
      JSON.parse(localStorage.getItem(PREFS_KEY) ?? "{}");
    return { ...DEFAULTS, ...saved, motion: storedMotion() };
  } catch { return DEFAULTS; }
}

// live: follows Windows' Animation effects while the page is open
function useWindowsReducesMotion() {
  const query = "(prefers-reduced-motion: reduce)";
  const [reduced, setReduced] = useState(() => matchMedia(query).matches);
  useEffect(() => {
    const mq = matchMedia(query);
    const on = () => setReduced(mq.matches);
    mq.addEventListener("change", on);
    return () => mq.removeEventListener("change", on);
  }, []);
  return reduced;
}

function Choice<T extends string>({ label, hint, options, value, onChange }: {
  label: string; hint: string; options: [T, string][]; value: T; onChange: (v: T) => void;
}) {
  return (
    <div className="setting">
      <span>
        <b>{label}</b>
        <small>{hint}</small>
      </span>
      <div className="seg seg-sm" role="radiogroup" aria-label={label}>
        {options.map(([id, text]) => (
          <button key={id} role="radio" aria-checked={value === id}
            className={`seg-btn${value === id ? " is-on" : ""}`} onClick={() => onChange(id)}>
            {text}
          </button>
        ))}
      </div>
    </div>
  );
}

function Toggle({ label, hint, checked, onChange }: {
  label: string; hint: string; checked: boolean; onChange: (v: boolean) => void;
}) {
  return (
    <label className="setting">
      <span>
        <b>{label}</b>
        <small>{hint}</small>
      </span>
      <input type="checkbox" role="switch" className="switch" checked={checked} onChange={e => onChange(e.target.checked)} />
    </label>
  );
}

export default function Settings() {
  const [prefs, setPrefs] = useState(load);
  const windowsReduces = useWindowsReducesMotion();
  const set = <K extends keyof Prefs>(k: K, v: Prefs[K]) => setPrefs(p => ({ ...p, [k]: v }));

  useEffect(() => {
    try { localStorage.setItem(PREFS_KEY, JSON.stringify(prefs)); } catch { /* storage unavailable */ }
    window.dispatchEvent(new Event(PREFS_EVENT)); // e.g. Discord activity reacts to the toggle
    applyMotion(prefs.motion);
  }, [prefs]);

  return (
    <div className="view">
      <header className="page-head">
        <div className="view-head">
          <p className="eyebrow">Preferences</p>
          <h1 className="display h1">Settings</h1>
        </div>
        <div className="head-links">
          <button className="text-btn" onClick={() => openExternal(REPO)}>GitHub</button>
          <button className="text-btn" onClick={() => openExternal(`${REPO}/issues/new`)}>Report a bug</button>
          <button className="text-btn" onClick={() => setPrefs(DEFAULTS)}>Reset to defaults</button>
        </div>
      </header>

      <div className="settings-stack">
        <AccountCard />

        <section className="card settings">
          <p className="stat-k">Launcher</p>
          <Toggle label="Start with Windows" hint="Open FLICKED in the background when you sign in."
            checked={prefs.startWithWindows} onChange={v => set("startWithWindows", v)} />
          <Toggle label="Minimize to tray" hint="Closing the window keeps FLICKED running."
            checked={prefs.minimizeToTray} onChange={v => set("minimizeToTray", v)} />
          <Toggle label="Hide while in a match" hint="Frees memory for CS2 while you play."
            checked={prefs.closeOnLaunch} onChange={v => set("closeOnLaunch", v)} />
          <Toggle label="Show activity on Discord" hint="Friends see your queue, party and match on your Discord profile."
            checked={prefs.discordActivity} onChange={v => set("discordActivity", v)} />
          <Choice
            label="Animations"
            hint={`Follow Windows uses your Windows animation effects setting (currently ${windowsReduces ? "off" : "on"}).`}
            options={[["system", "Follow Windows"], ["on", "On"], ["off", "Off"]]}
            value={prefs.motion}
            onChange={v => set("motion", v)}
          />
        </section>
      </div>
    </div>
  );
}
