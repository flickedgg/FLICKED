"use client";

import { useState } from "react";
import type { Server, ServerForm as Form, ServerType } from "@/lib/servers";

/* Add and edit are the same fields, so they are the same form.

   The one asymmetry is the RCON password: required when adding, optional when
   editing, because the stored one cannot be read back to show. Leaving it blank
   on an edit keeps whatever is already there. */
export function ServerForm({ existing, busy, error, onSubmit, onCancel }: {
  existing?: Server;
  busy: boolean;
  error: string | null;
  onSubmit: (form: Form) => void;
  onCancel: () => void;
}) {
  const [name, setName] = useState(existing?.name ?? "");
  const [region, setRegion] = useState(existing?.region ?? "");
  const [type, setType] = useState<ServerType>(existing?.type ?? "Competitive");
  const [host, setHost] = useState(existing?.host ?? "");
  const [port, setPort] = useState(String(existing?.port ?? 27015));
  const [gamePassword, setGamePassword] = useState("");
  const [rconPassword, setRconPassword] = useState("");

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit({
      name, region, type, host,
      port: Number(port) || 27015,
      // undefined leaves a value alone; "" on an edit clears the game password
      gamePassword: existing && gamePassword === "" ? undefined : gamePassword,
      rconPassword: rconPassword === "" ? undefined : rconPassword,
    });
  };

  return (
    <form onSubmit={submit} className="panel-1 mt-6 rounded-xl p-6">
      <p className="stat-k">{existing ? `Edit ${existing.name}` : "Register a server"}</p>

      <div className="mt-5 grid gap-4 sm:grid-cols-2">
        <Field label="Name" hint="Shown in the dashboard: fra-01">
          <input value={name} onChange={e => setName(e.target.value)} required
                 placeholder="fra-01" className={input} />
        </Field>

        <Field label="Region" hint="Free text, used to prefer nearby servers">
          <input value={region} onChange={e => setRegion(e.target.value)}
                 placeholder="eu-west" className={input} />
        </Field>

        <Field label="Host" hint="Address players connect to">
          <input value={host} onChange={e => setHost(e.target.value)} required
                 placeholder="203.0.113.10" className={input} />
        </Field>

        <Field label="Port">
          <input value={port} onChange={e => setPort(e.target.value)} inputMode="numeric"
                 placeholder="27015" className={input} />
        </Field>

        <Field label="Mode" hint="What this server is configured for">
          <select value={type} onChange={e => setType(e.target.value as ServerType)} className={select}>
            <option value="Competitive">Competitive · 5v5</option>
            <option value="Wingman">Wingman · 2v2</option>
          </select>
        </Field>

        <Field label="Game password" hint="sv_password. Optional; players are given it.">
          <input value={gamePassword} onChange={e => setGamePassword(e.target.value)}
                 type="password" autoComplete="off"
                 placeholder={existing?.hasGamePassword ? "unchanged" : "none"} className={input} />
        </Field>

        <Field
          label="RCON password"
          hint={existing
            ? "Stored encrypted and never shown. Leave blank to keep it."
            : "Needed so FLICKED can tell the server to load a match."}
        >
          <input value={rconPassword} onChange={e => setRconPassword(e.target.value)}
                 type="password" autoComplete="off" required={!existing}
                 placeholder={existing ? "unchanged" : ""} className={input} />
        </Field>
      </div>

      {error && <p className="mt-5 text-[13.5px] text-primary-light">{error}</p>}

      <div className="mt-6 flex flex-wrap gap-3">
        <button type="submit" disabled={busy} className="btn btn-primary">
          <span>{busy ? "Saving…" : existing ? "Save changes" : "Register server"}</span>
        </button>
        <button type="button" onClick={onCancel} className="btn btn-outline">
          <span>Cancel</span>
        </button>
      </div>
    </form>
  );
}

const field =
  "w-full rounded-md border border-white/10 px-3 py-2 text-[14px] " +
  "text-foreground outline-none focus:border-primary/60";

const input = `${field} bg-black/20`;

/* A dropdown needs an opaque colour of its own, and so do its options: the list
   is drawn by the browser over the page rather than inside it, so there is
   nothing dark behind a translucent background and white text lands on the
   system's white. See the same pair in news-form.tsx. */
const select = `${field} bg-elevated [&>option]:bg-elevated [&>option]:text-foreground`;

function Field({ label, hint, children }: {
  label: string; hint?: string; children: React.ReactNode;
}) {
  return (
    <label className="block">
      <span className="stat-k">{label}</span>
      <span className="mt-2 block">{children}</span>
      {hint && <small className="mt-1.5 block text-[12px] text-subtle">{hint}</small>}
    </label>
  );
}
