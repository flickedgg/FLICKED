import { lazy, Suspense, useState } from "react";
import { TitleBar } from "./components/TitleBar";
import { Sidebar, type View } from "./components/Sidebar";
import { StatusBar } from "./components/StatusBar";
import { MatchFlow } from "./components/MatchFlow";
import { useQueue } from "./hooks/useQueue";
import { useParty } from "./hooks/useParty";
import { useSocial } from "./hooks/useSocial";
import { usePresence } from "./hooks/usePresence";
import { MODES } from "./data/demo";
import { Play } from "./views/Play";

// Play is the first screen, so it ships in the main bundle; the rest load on first visit
const Matches = lazy(() => import("./views/Matches"));
const Leaderboard = lazy(() => import("./views/Leaderboard"));
const Settings = lazy(() => import("./views/Settings"));
const News = lazy(() => import("./views/News"));

export default function App() {
  const [view, setView] = useState<View>("play");
  const queue = useQueue();
  /* One poll for friends, party and invites, owned here rather than in the
     friends panel: the party is drawn on the Play screen and in Discord's
     presence, and two hooks polling the same endpoint would be two answers. */
  const social = useSocial();
  const party = useParty(social);
  // lives here (not in Play) so it survives page switches and Discord can show it
  const [modeId, setModeId] = useState(MODES[0].id);
  const mode = MODES.find(m => m.id === modeId)!;
  usePresence(queue, party, mode);

  return (
    <div className="app">
      <TitleBar queue={queue} showQueue={view !== "play"} />
      <Sidebar current={view} onSelect={setView} />

      <main className="main">
        <Suspense fallback={null}>
          {view === "play" && <Play queue={queue} party={party} social={social} mode={modeId} setMode={setModeId} />}
          {view === "matches" && <Matches />}
          {view === "leaderboard" && <Leaderboard />}
          {view === "settings" && <Settings />}
          {view === "news" && <News />}
        </Suspense>
      </main>

      <StatusBar />

      {queue.phase !== "idle" && queue.phase !== "searching" && <MatchFlow queue={queue} />}
    </div>
  );
}
