import { API } from "./api";

/* The server pool, from the dashboard's side.

   Called from client components, so every request needs credentials: the session
   is a cookie and browsers only attach one cross-origin when asked. */

export type ServerStatus = "Offline" | "Idle" | "Reserved" | "Hosting";
export type ServerType = "Competitive" | "Wingman";

export type Server = {
  id: number;
  name: string;
  region: string;
  type: ServerType;
  host: string;
  port: number;
  status: ServerStatus;
  currentMatchId: number | null;
  lastSeenAt: string | null;
  isEnabled: boolean;
  hasRcon: boolean;          // the password itself is never sent back
  hasGamePassword: boolean;
  createdAt: string;
};

export type ServerForm = {
  name?: string;
  region?: string;
  type?: ServerType;
  host?: string;
  port?: number;
  gamePassword?: string;
  rconPassword?: string;
  isEnabled?: boolean;
};

const base = `${API}/api/admin/servers`;

/* The API answers a refused action with plain text ("That server is in a match.
   Wait for it to finish."), so that text becomes the error. Writing our own
   messages here would mean two sets of wording drifting apart. */
async function send<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, { credentials: "include", cache: "no-store", ...init });

  if (!res.ok) {
    const body = await res.text();
    throw new Error(body.trim() || `Request failed (${res.status}).`);
  }

  return res.status === 204 ? (undefined as T) : (await res.json() as T);
}

const json = (body: unknown): RequestInit => ({
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify(body),
});

export const listServers = () => send<Server[]>(base);

// The token is shown once and only stored hashed, so it cannot be fetched later.
export const addServer = (form: ServerForm) =>
  send<{ server: Server; token: string }>(base, { method: "POST", ...json(form) });

export const editServer = (id: number, form: ServerForm) =>
  send<Server>(`${base}/${id}`, { method: "PATCH", ...json(form) });

export const deleteServer = (id: number) =>
  send<void>(`${base}/${id}`, { method: "DELETE" });

export const rotateToken = (id: number) =>
  send<{ token: string }>(`${base}/${id}/token`, { method: "POST" });
