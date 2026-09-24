export function StatusBar() {
  return (
    <footer className="statusbar">
      <span className="status is-demo"><i />Beta Version · could be unstable</span>
      <span className="statusbar-mid">
        <span>CS2</span>
        <span>FLICKED Servers</span>
      </span>
      <span>v{__APP_VERSION__}</span>
    </footer>
  );
}
