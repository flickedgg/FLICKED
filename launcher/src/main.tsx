import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App";

// fonts ship inside the app (no network at startup); latin subsets only
import "@fontsource/barlow-condensed/latin-700.css";
import "@fontsource/barlow-condensed/latin-700-italic.css";
import "@fontsource/jetbrains-mono/latin-400.css";
import "@fontsource/jetbrains-mono/latin-500.css";
import "./styles.css";
import { applyMotion, storedMotion } from "./lib/motion";

// apply the saved animation preference before first paint (Settings owns the rest of prefs)
applyMotion(storedMotion());

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
