import { useEffect, useRef, useState } from "react";
import { ACCOUNT, PLAYER } from "../data/demo";
import { openExternal } from "../lib/openExternal";
import { Icon } from "./Icon";

type Link = "linked" | "linking" | "unlinked";

/* One row: who you are on FLICKED, and the Steam account you play as.
   Linking is simulated: the real flow opens Steam sign-in in the browser
   and the backend confirms the account. */
export function AccountCard() {
  const [link, setLink] = useState<Link>("linked");
  const timer = useRef<number>(undefined);
  useEffect(() => () => clearTimeout(timer.current), []);

  const startLink = () => {
    setLink("linking");
    timer.current = window.setTimeout(() => setLink("linked"), 2200);
  };

  const { steam } = ACCOUNT;

  return (
    <section className="card account">
      <span className="account-av" aria-hidden="true">{PLAYER.name[0].toUpperCase()}</span>
      <span className="account-who">
        <b>{PLAYER.name}</b>
        <small>{PLAYER.division} · {PLAYER.rating.toLocaleString()}</small>
      </span>

      {link === "linked" ? (
        <span className="account-steam">
          {/* the chip itself opens the Steam profile */}
          <button
            className="steam-chip"
            onClick={() => openExternal(`https://steamcommunity.com/profiles/${steam.id64}`)}
            title="Open Steam profile"
          >
            <span className="steam-chip-icon"><Icon name="steam" size={22} /><i /></span>
            <span className="steam-chip-text">
              <b>{steam.persona}</b>
              <small>Steam · linked</small>
            </span>
            <span className="steam-chip-go"><Icon name="external" size={14} /></span>
          </button>
          <button className="icon-btn is-danger" onClick={() => setLink("unlinked")} title="Unlink Steam" aria-label="Unlink Steam">
            <Icon name="unlink" size={17} />
          </button>
        </span>
      ) : (
        <button className="btn btn-primary steam-link" onClick={startLink} disabled={link === "linking"}>
          {link === "linking"
            ? <><i className="spinner" />Opening Steam…</>
            : <><Icon name="steam" size={16} />Link Steam</>}
        </button>
      )}
    </section>
  );
}
