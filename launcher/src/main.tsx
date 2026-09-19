import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App";

// fonts ship inside the app (no network at startup); latin subsets only
import "@fontsource/barlow-condensed/latin-700.css";
import "@fontsource/barlow-condensed/latin-700-italic.css";
import "@fontsource/jetbrains-mono/latin-400.css";
import "@fontsource/jetbrains-mono/latin-500.css";
import "./styles.css";

// apply the saved motion preference before first paint (Settings owns the rest of prefs)
try {
  if (JSON.parse(localStorage.getItem("flicked.prefs") ?? "{}").reduceMotion) {
    document.documentElement.setAttribute("data-reduce-motion", "");
  }
} catch { /* storage unavailable */ }

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
