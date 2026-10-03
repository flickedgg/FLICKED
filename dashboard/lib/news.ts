import { API } from "./api";

/* News, from the dashboard's side.

   Reading is the public endpoint on purpose: a post is live the moment it is
   saved, so the list an admin edits is exactly the list players see, and there is
   no second shape of it to drift. Writing needs the session, which is a cookie,
   and browsers only attach one cross-origin when asked. */

export type NewsPost = {
  id: string;            // a slug made from the title, fixed once published
  category: string;      // one of the slugs from categories()
  date: string;          // yyyy-mm-dd
  badge: string | null;
  title: string;
  excerpt: string;
  body: string[];        // one entry per paragraph
  createdAt: string;     // only ever used to order two posts from the same day
};

export type NewsCategory = { slug: string; label: string };

/* What a post is edited with. Undefined leaves a field alone, which is how the
   same shape serves both adding and editing; body always travels whole. */
export type NewsForm = {
  category?: string;
  title?: string;
  excerpt?: string;
  body?: string[];
  date?: string;
  badge?: string;
};

const admin = `${API}/api/admin/news`;

/* The API answers a refused write with plain text ("A summary is required."), so
   that text becomes the error. Writing our own messages here would mean two sets
   of wording drifting apart. */
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

export const listNews = () => send<NewsPost[]>(`${API}/api/news`);

/* The categories come from the backend rather than a copy here, because this is
   the side that writes one: a dropdown offering a slug the API refuses would be
   an error the admin cannot act on. */
export const listCategories = () => send<NewsCategory[]>(`${API}/api/news/categories`);

export const publishNews = (form: NewsForm) =>
  send<NewsPost>(admin, { method: "POST", ...json(form) });

export const editNews = (id: string, form: NewsForm) =>
  send<NewsPost>(`${admin}/${id}`, { method: "PATCH", ...json(form) });

export const deleteNews = (id: string) =>
  send<void>(`${admin}/${id}`, { method: "DELETE" });

/* One textarea, one paragraph per blank-line-separated block.

   Writing prose in a browser means pressing Enter, and a single newline inside a
   sentence that wrapped is not a new paragraph. Splitting on blank lines is the
   rule people already know from Markdown. The backend drops whatever blanks
   survive this, so a trailing newline is harmless. */
export const toParagraphs = (text: string): string[] =>
  text.split(/\n\s*\n/).map(p => p.trim().replace(/\s*\n\s*/g, " ")).filter(Boolean);

/// The other way round, to put a stored post back in the textarea.
export const toText = (body: string[]): string => body.join("\n\n");
