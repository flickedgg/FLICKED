import { NETWORK } from "../data/demo";

export function StatusBar() {
  return (
    <footer className="statusbar">
      <span className="status is-demo"><i />Demo data · no backend connected</span>
      <span className="statusbar-mid">
        <span>{NETWORK.online.toLocaleString()} online</span>
        <span>{NETWORK.servers} servers</span>
      </span>
      <span>v{__APP_VERSION__}</span>
    </footer>
  );
}
