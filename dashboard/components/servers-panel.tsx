"use client";

import { useCallback, useEffect, useState } from "react";
import { ServerForm } from "@/components/server-form";
import {
  addServer, deleteServer, editServer, listServers, rotateToken,
  type Server, type ServerForm as Form,
} from "@/lib/servers";

/* The server pool: what is in it, and the four things an admin can do to it.

   The list is reloaded after every change rather than patched here. The server
   decides what a change actually did (an edit can be refused because a match
   started a second ago), so asking it is more honest than guessing. */
export function ServersPanel() {
  const [servers, setServers] = useState<Server[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const [adding, setAdding] = useState(false);
  const [editing, setEditing] = useState<Server | null>(null);
  const [token, setToken] = useState<{ name: string; value: string } | null>(null);

  const reload = useCallback(async () => {
    try {
      setServers(await listServers());
      setError(null);
    } catch (e) {
      setError(String((e as Error).message));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { reload(); }, [reload]);

  // one wrapper per action: run it, show what the API said, reload
  const act = async (run: () => Promise<void>) => {
    setBusy(true);
    setError(null);
    try {
      await run();
      await reload();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const create = (form: Form) => act(async () => {
    const { server, token } = await addServer(form);
    setToken({ name: server.name, value: token });   // shown once, never again
    setAdding(false);
  });

  const save = (form: Form) => act(async () => {
    await editServer(editing!.id, form);
    setEditing(null);
  });

  const remove = (server: Server) => {
    if (!confirm(`Remove ${server.name}? This cannot be undone.`)) return;
    act(() => deleteServer(server.id));
  };

  const newToken = (server: Server) => act(async () => {
    const { token } = await rotateToken(server.id);
    setToken({ name: server.name, value: token });
  });

  return (
    <>
      <div className="mt-10 flex flex-wrap items-center justify-between gap-4">
        <p className="stat-k">{loading ? "Loading" : `${servers.length} registered`}</p>
        {!adding && !editing && (
          <button onClick={() => setAdding(true)} className="btn btn-primary h-9 px-4 text-[13px]">
            <span>Add server</span>
          </button>
        )}
      </div>

      {/* shown once after registering or rotating: only the hash is kept */}
      {token && (
        <div className="panel-1 mt-6 rounded-xl border-primary/40 p-6">
          <p className="stat-k">Server token · {token.name}</p>
          <p className="mt-2 text-[13.5px] text-muted">
            Put this in the server&apos;s FLICKED plugin config. It is shown once; if it is
            lost, issue a new one.
          </p>
          <code className="mt-4 block overflow-x-auto rounded-md border border-white/10 bg-black/30 px-4 py-3 font-mono text-[13px] text-foreground">
            {token.value}
          </code>
          <button onClick={() => setToken(null)} className="btn btn-outline mt-4 h-9 px-4 text-[13px]">
            <span>I have copied it</span>
          </button>
        </div>
      )}

      {adding && (
        <ServerForm busy={busy} error={error} onSubmit={create} onCancel={() => setAdding(false)} />
      )}
      {editing && (
        <ServerForm existing={editing} busy={busy} error={error}
                    onSubmit={save} onCancel={() => setEditing(null)} />
      )}

      {error && !adding && !editing && (
        <p className="mt-6 text-[13.5px] text-primary-light">{error}</p>
      )}

      {!loading && servers.length === 0 && !adding && (
        <p className="mt-8 text-[15px] text-muted">
          No servers yet. Register the CS2 servers your community already runs, and FLICKED
          will hand one out per match.
        </p>
      )}

      {servers.length > 0 && (
        <div className="panel-1 mt-6 overflow-hidden rounded-xl">
          <table className="w-full text-left text-[14px]">
            <thead>
              <tr className="border-b border-white/[0.06]">
                {["Server", "Address", "Mode", "Status", ""].map((h, i) => (
                  <th key={h} className={`stat-k px-5 py-3.5 font-normal ${i === 4 ? "text-end" : ""}`}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-white/[0.05]">
              {servers.map(s => (
                <tr key={s.id} className={s.isEnabled ? "" : "opacity-55"}>
                  <td className="px-5 py-4">
                    <b className="block font-medium text-foreground">{s.name}</b>
                    <small className="block text-[12px] text-subtle">
                      {s.region || "no region"}{s.isEnabled ? "" : " · disabled"}
                    </small>
                  </td>
                  <td className="px-5 py-4 font-mono text-[12.5px] text-muted">{s.host}:{s.port}</td>
                  <td className="px-5 py-4 text-muted">{s.type}</td>
                  <td className="px-5 py-4"><Status server={s} /></td>
                  <td className="px-5 py-4">
                    <span className="flex flex-wrap justify-end gap-2">
                      <Action onClick={() => setEditing(s)} disabled={busy}>Edit</Action>
                      <Action onClick={() => newToken(s)} disabled={busy}>New token</Action>
                      <Action onClick={() => remove(s)} disabled={busy} danger>Remove</Action>
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}

/* Reserved and Hosting both mean "a match is depending on this", which is what
   stops it being edited or removed, so they share the live treatment. */
function Status({ server }: { server: Server }) {
  const live = server.status === "Hosting" || server.status === "Reserved";
  const label = server.status === "Hosting" && server.currentMatchId
    ? `In match #${server.currentMatchId}`
    : server.status === "Reserved" ? "Reserved"
    : server.status === "Idle" ? "Free"
    : "Offline";

  return (
    <span className={`status${live ? " is-live" : ""}`}>
      <i style={live ? { background: "var(--primary)" } : undefined} aria-hidden="true" />
      {label}
    </span>
  );
}

function Action({ onClick, disabled, danger, children }: {
  onClick: () => void; disabled: boolean; danger?: boolean; children: React.ReactNode;
}) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      className={`rounded-md border px-3 py-1.5 font-mono text-[11.5px] transition-colors disabled:opacity-40 ${
        danger
          ? "border-white/10 text-subtle hover:border-primary/50 hover:text-primary-light"
          : "border-white/10 text-muted hover:border-white/25 hover:text-foreground"
      }`}
    >
      {children}
    </button>
  );
}
