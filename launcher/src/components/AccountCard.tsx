import { useSession } from "../hooks/useSession";
import { openExternal } from "../lib/openExternal";
import { Icon } from "./Icon";

/* One row: who you are on FLICKED, and the Steam account you play as.
   Signing in opens Steam in the browser; Rust holds the session afterwards. */
export function AccountCard() {
  const { account, busy, error, begin, end } = useSession();

  if (!account) {
    return (
      <section className="card account">
        <span className="account-av" aria-hidden="true">?</span>
        <span className="account-who">
          <b>Not signed in</b>
          <small>{error ?? "Sign in with Steam to queue and keep your stats."}</small>
        </span>

        <button className="btn btn-primary steam-link" onClick={begin} disabled={busy}>
          {busy
            ? <><i className="spinner" />Opening Steam…</>
            : <><Icon name="steam" size={16} />Sign in with Steam</>}
        </button>
      </section>
    );
  }

  return (
    <section className="card account">
      {/* the Steam picture when the host has an API key, the initial otherwise */}
      {account.avatarUrl
        ? <img className="account-av" src={account.avatarUrl} alt="" width={40} height={40} />
        : <span className="account-av" aria-hidden="true">{account.name[0].toUpperCase()}</span>}
      <span className="account-who">
        <b>{account.name}</b>
        <small>{account.rating.toLocaleString()} rating</small>
      </span>

      <span className="account-steam">
        {/* the chip itself opens the Steam profile */}
        <button
          className="steam-chip"
          onClick={() => account.steamId && openExternal(`https://steamcommunity.com/profiles/${account.steamId}`)}
          disabled={!account.steamId}
          title="Open Steam profile"
        >
          <span className="steam-chip-icon"><Icon name="steam" size={22} /><i /></span>
          <span className="steam-chip-text">
            <b>{account.steamId ?? "No Steam account"}</b>
            <small>Steam · linked</small>
          </span>
          <span className="steam-chip-go"><Icon name="external" size={14} /></span>
        </button>
        <button className="icon-btn is-danger" onClick={end} disabled={busy} title="Sign out" aria-label="Sign out">
          <Icon name="unlink" size={17} />
        </button>
      </span>
    </section>
  );
}
