import { useEffect, useState } from "react";

type Prefs = {
  cs2Path: string;
  launchOptions: string;
  startWithWindows: boolean;
  minimizeToTray: boolean;
  closeOnLaunch: boolean;
  reduceMotion: boolean;
};

const DEFAULTS: Prefs = {
  cs2Path: "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Counter-Strike Global Offensive",
  launchOptions: "-novid -high",
  startWithWindows: false,
  minimizeToTray: true,
  closeOnLaunch: false,
  reduceMotion: false,
};

const KEY = "flicked.prefs";

// kept in localStorage for now; moves to a Rust-side store once settings drive real behaviour
function load(): Prefs {
  try { return { ...DEFAULTS, ...JSON.parse(localStorage.getItem(KEY) ?? "{}") }; }
  catch { return DEFAULTS; }
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
  const set = <K extends keyof Prefs>(k: K, v: Prefs[K]) => setPrefs(p => ({ ...p, [k]: v }));

  useEffect(() => {
    try { localStorage.setItem(KEY, JSON.stringify(prefs)); } catch { /* storage unavailable */ }
    document.documentElement.toggleAttribute("data-reduce-motion", prefs.reduceMotion);
  }, [prefs]);

  return (
    <div className="view view-narrow">
      <header className="view-head">
        <p className="eyebrow">Preferences</p>
        <h1 className="display h1">Settings</h1>
      </header>

      <section className="card settings">
        <p className="stat-k">Game</p>
        <label className="field">
          <span>CS2 install folder</span>
          <input className="input mono" value={prefs.cs2Path} onChange={e => set("cs2Path", e.target.value)} spellCheck={false} />
        </label>
        <label className="field">
          <span>Launch options</span>
          <input className="input mono" value={prefs.launchOptions} onChange={e => set("launchOptions", e.target.value)} spellCheck={false} />
        </label>
      </section>

      <section className="card settings">
        <p className="stat-k">Launcher</p>
        <Toggle label="Start with Windows" hint="Open FLICKED in the background when you sign in."
          checked={prefs.startWithWindows} onChange={v => set("startWithWindows", v)} />
        <Toggle label="Minimize to tray" hint="Closing the window keeps FLICKED running."
          checked={prefs.minimizeToTray} onChange={v => set("minimizeToTray", v)} />
        <Toggle label="Hide while in a match" hint="Frees memory for CS2 while you play."
          checked={prefs.closeOnLaunch} onChange={v => set("closeOnLaunch", v)} />
        <Toggle label="Reduce motion" hint="Turns off transitions and animations."
          checked={prefs.reduceMotion} onChange={v => set("reduceMotion", v)} />
      </section>
    </div>
  );
}
