import { getCurrentWindow } from "@tauri-apps/api/window";
import { isTauri } from "@tauri-apps/api/core";
import logo from "../assets/logo.png";
import type { Queue } from "../hooks/useQueue";
import { Elapsed } from "./Elapsed";

// null when the UI is opened in a plain browser (vite dev without tauri)
const win = isTauri() ? getCurrentWindow() : null;

export function TitleBar({ queue, showQueue }: { queue: Queue; showQueue: boolean }) {
  return (
    <header className="titlebar" data-tauri-drag-region>
      <div className="brand" data-tauri-drag-region>
        <img src={logo} alt="FLICKED" width={20} height={18} draggable={false} />
        <span className="tag">alpha</span>
      </div>

      {showQueue && queue.phase === "searching" && (
        <button className="queue-pill" onClick={queue.cancel} title="Cancel search">
          <i className="live-dot" />
          Searching · <Elapsed since={queue.startedAt} />
          <span className="queue-pill-x">Cancel</span>
        </button>
      )}

      <div className="win-controls">
        <button aria-label="Minimize" onClick={() => win?.minimize()}>
          <svg viewBox="0 0 10 10"><path d="M1 5h8" /></svg>
        </button>
        <button aria-label="Maximize" onClick={() => win?.toggleMaximize()}>
          <svg viewBox="0 0 10 10"><rect x="1.5" y="1.5" width="7" height="7" /></svg>
        </button>
        <button aria-label="Close" className="is-close" onClick={() => win?.close()}>
          <svg viewBox="0 0 10 10"><path d="M1.5 1.5l7 7M8.5 1.5l-7 7" /></svg>
        </button>
      </div>
    </header>
  );
}
