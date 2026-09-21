"use client";

import { gsap } from "gsap";
import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { motionOK } from "@/lib/motion";

/* A product illustration, not live data: one match, start to finish,
   as a FLICKED match room would show it. */

const TABS = [
  ["scoreboard", "Scoreboard"],
  ["vote", "Map vote"],
  ["server", "Server"],
] as const;

type Tab = (typeof TABS)[number][0];

type Player = [name: string, tone: string, k: number, d: number, adr: number, rating: number];

const EMBER: Player[] = [
  ["kovac",     "#f65600", 21, 12, 97, 1.34],
  ["Nyx",       "#d9772f", 17, 13, 84, 1.16],
  ["reload",    "#b4632b", 14, 14, 76, 1.02],
  ["m0th",      "#8f5230", 12, 15, 68, 0.94],
  ["patchnote", "#6e4630", 9,  16, 55, 0.81],
];

const SLATE: Player[] = [
  ["Halden",    "#7d8aa0", 19, 14, 91, 1.22],
  ["sprayz",    "#66758c", 15, 15, 80, 1.05],
  ["quietus",   "#586478", 13, 15, 71, 0.97],
  ["Brine",     "#4b5566", 11, 16, 63, 0.88],
  ["lowground", "#3f4755", 10, 17, 58, 0.84],
];

// e = Ember won the round, s = Slate; 20 played so far of MR12
const ROUNDS = "eessesseeses" + "seeseese";

const MAPS: [map: string, note: string, state: "" | "picked"][] = [
  ["Mirage",  "4 votes · playing", "picked"],
  ["Inferno", "3 votes",  ""],
  ["Nuke",    "2 votes",  ""],
  ["Anubis",  "1 vote",   ""],
  ["Ancient", "No votes", ""],
  ["Train",   "No votes", ""],
  ["Dust II", "No votes", ""],
];

/* How a match reaches a server, narrated as a server log. */
const LINES: [tag: string, text: string, cls: string][] = [
  ["queue",  "10/10 players accepted · match 48213 created", "in"],
  ["vote",   "Mirage wins the vote · 4 of 10", "in"],
  ["pool",   "claiming registered server fra-01 · was free", "in"],
  ["server", "fra-01 reserved for match 48213 · 10 slots", "ok"],
  ["server", "map de_mirage loaded · match config applied", "in"],
  ["match",  "10/10 connected · going live", "ok"],
  ["demo",   "recording 48213_de_mirage.dem", "fl"],
  ["stats",  "round events streaming to PostgreSQL", "in"],
  ["match",  "round 20 · Ember 11 – 9 Slate", "fl"],
  ["warn",   "player Brine timed out · reconnect window 60s", "wr"],
  ["match",  "Brine reconnected", "ok"],
];

function ServerLog() {
  const logRef = useRef<HTMLDivElement>(null);

  // imperative on purpose: a typewriter re-rendering React per character is wasteful
  useEffect(() => {
    const logEl = logRef.current;
    if (!logEl) return;

    const head = (i: number) => `<span class="ts">[${LINES[i][0].padEnd(6, " ")}]</span> `;

    if (!motionOK()) {
      logEl.innerHTML = LINES.map((l, i) => head(i) + `<span class="${l[2]}">${l[1]}</span>`).join("\n");
      return;
    }

    let li = 0, ci = 0, buf = "";
    let timer: number;

    function typeLine() {
      if (li >= LINES.length) {
        timer = window.setTimeout(() => { li = 0; ci = 0; buf = ""; logEl!.innerHTML = ""; typeLine(); }, 4600);
        return;
      }
      const [, text, cls] = LINES[li];

      if (ci <= text.length) {
        logEl!.innerHTML = buf + head(li) + `<span class="${cls}">${text.slice(0, ci)}</span><span class="caret"></span>`;
        ci++;
        timer = window.setTimeout(typeLine, text[ci - 1] === " " ? 7 : 16);
      } else {
        buf += head(li) + `<span class="${cls}">${text}</span>\n`;
        li++; ci = 0;
        timer = window.setTimeout(typeLine, 380);
      }
    }

    typeLine();
    return () => clearTimeout(timer);
  }, []);

  return (
    <div className="h-[332px] overflow-hidden rounded-lg border border-white/[0.06] bg-black/30 p-4 font-mono text-[12px] leading-[1.85]">
      <div id="log" ref={logRef} className="whitespace-pre-wrap break-words" />
    </div>
  );
}

function TeamTable({ name, players, tone }: { name: string; players: Player[]; tone: string }) {
  return (
    <div>
      <p className="mb-2 flex items-center gap-2 px-1 font-display text-[17px] font-bold uppercase text-foreground">
        <i className="h-2 w-3" style={{ background: tone }} />{name}
      </p>
      <div className="sb-row sb-head">
        <span>Player</span><span>K</span><span>D</span><span>ADR</span><span>Rtg</span>
      </div>
      <div className="divide-y divide-white/[0.05] border-t border-white/[0.06]">
        {players.map(([who, av, k, d, adr, rating]) => (
          <div key={who} className="sb-row">
            <span className="who"><i className="av" style={{ background: av }} /><span>{who}</span></span>
            <span>{k}</span><span>{d}</span><span>{adr}</span>
            <span className={rating >= 1.2 ? "rating-hi" : undefined}>{rating.toFixed(2)}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

export function MatchRoom() {
  const [tab, setTab] = useState<Tab>("scoreboard");
  const [clock, setClock] = useState(84);
  const paneRefs = useRef<Partial<Record<Tab, HTMLDivElement | null>>>({});

  /* round clock */
  useEffect(() => {
    if (!motionOK()) return;
    const id = setInterval(() => setClock(c => (c <= 1 ? 115 : c - 1)), 1000);
    return () => clearInterval(id);
  }, []);

  const firstTabRun = useRef(true);
  useLayoutEffect(() => {
    if (firstTabRun.current) { firstTabRun.current = false; return; }
    if (motionOK()) {
      gsap.fromTo(paneRefs.current[tab]!, { opacity: 0, y: 6 }, { opacity: 1, y: 0, duration: 0.34, ease: "power2.out" });
    }
  }, [tab]);

  const mmss = `${Math.floor(clock / 60)}:${String(clock % 60).padStart(2, "0")}`;

  return (
    <div className="panel-2 overflow-hidden rounded-xl">

      <div className="flex items-center gap-3 border-b border-white/[0.06] px-5 py-3.5">
        <span className="font-mono text-[12px] text-subtle">match 48213</span>
        <span className="text-faint">/</span>
        <span className="font-mono text-[12px] text-foreground">de_mirage</span>
        <span className="ms-auto flex items-center gap-2 font-mono text-[11px] text-primary-light">
          <i className="live-dot" />Live · {mmss}
        </span>
      </div>

      <div className="flex overflow-x-auto border-b border-white/[0.06]" role="tablist">
        {TABS.map(([id, label]) => (
          <button
            key={id}
            className={`tab shrink-0${tab === id ? " is-active" : ""}`}
            role="tab"
            aria-selected={tab === id}
            aria-controls={`pane-${id}`}
            onClick={() => setTab(id)}
          >
            {label}
          </button>
        ))}
      </div>

      <div className="p-5 sm:p-6">
        {/* scoreboard */}
        <div id="pane-scoreboard" ref={el => { paneRefs.current.scoreboard = el; }} hidden={tab !== "scoreboard"}>
          <div className="grid grid-cols-[1fr_auto_1fr] items-center gap-4">
            <div>
              <p className="stat-k">Team Ember</p>
              <p className="team-score mt-1 text-primary">11</p>
            </div>
            <div className="text-center">
              <p className="font-display text-[15px] font-bold uppercase text-muted">Mirage</p>
              <p className="mt-0.5 font-mono text-[10.5px] tracking-[0.12em] text-faint">MR12 · RD 21</p>
            </div>
            <div className="text-end">
              <p className="stat-k">Team Slate</p>
              <p className="team-score mt-1 text-foreground">9</p>
            </div>
          </div>

          <div className="rounds mt-5" aria-label="Round history: Ember 11, Slate 9">
            {Array.from({ length: 24 }, (_, i) => (
              <i key={i} className={`${ROUNDS[i] ?? ""}${i === 12 ? " half" : ""}`} />
            ))}
          </div>

          <div className="mt-6 grid gap-6 border-t border-white/[0.06] pt-5 lg:grid-cols-2 lg:gap-8">
            <TeamTable name="Ember" players={EMBER} tone="var(--primary)" />
            <TeamTable name="Slate" players={SLATE} tone="var(--slate)" />
          </div>
        </div>

        {/* vote */}
        <div id="pane-vote" ref={el => { paneRefs.current.vote = el; }} hidden={tab !== "vote"}>
          <div className="mb-4 flex items-center justify-between">
            <p className="stat-k">Everyone votes · 15 seconds</p>
            <p className="font-mono text-[11px] text-faint">7 maps · BO1</p>
          </div>
          <div className="veto">
            {MAPS.map(([map, note, state]) => (
              <div key={map} className={state ? `map is-${state}` : "map"}>
                <div>
                  <b>{map}</b>
                  <small className="mt-1.5 block">{note}</small>
                </div>
              </div>
            ))}
          </div>
          <p className="mt-5 border-t border-white/[0.06] pt-5 text-[13.5px] leading-relaxed text-subtle">
            Once everyone accepts, all ten players vote. The map with the most votes is
            played, and a tie is settled at random.
          </p>
        </div>

        {/* server */}
        <div id="pane-server" ref={el => { paneRefs.current.server = el; }} hidden={tab !== "server"}>
          <ServerLog />
        </div>
      </div>
    </div>
  );
}
