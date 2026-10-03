"use client";

import { useCallback, useEffect, useState } from "react";
import { NewsForm } from "@/components/news-form";
import {
  deleteNews, editNews, listCategories, listNews, publishNews,
  type NewsCategory, type NewsForm as Form, type NewsPost,
} from "@/lib/news";

/* The news feed, and the three things an admin can do to it.

   The list is reloaded after every change rather than patched here, the same way
   the server pool does it: the API decides what a change actually did — a title
   is trimmed, blank paragraphs are dropped, a repeated headline is numbered — so
   asking it is more honest than guessing. */
export function NewsPanel() {
  const [posts, setPosts] = useState<NewsPost[]>([]);
  const [categories, setCategories] = useState<NewsCategory[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const [writing, setWriting] = useState(false);
  const [editing, setEditing] = useState<NewsPost | null>(null);

  const reload = useCallback(async () => {
    try {
      setPosts(await listNews());
      setError(null);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { reload(); }, [reload]);

  /* The categories come from the backend, once. A failure here is not worth an
     error of its own — the form just has nothing to offer — but it does mean the
     "Write a post" button would lead nowhere, so it is disabled below instead. */
  useEffect(() => { listCategories().then(setCategories).catch(() => setCategories([])); }, []);

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

  const publish = (form: Form) => act(async () => {
    await publishNews(form);
    setWriting(false);
  });

  const save = (form: Form) => act(async () => {
    await editNews(editing!.id, form);
    setEditing(null);
  });

  const remove = (post: NewsPost) => {
    if (!confirm(`Remove “${post.title}”? This cannot be undone.`)) return;
    act(() => deleteNews(post.id));
  };

  const label = (slug: string) =>
    categories.find(c => c.slug === slug)?.label ?? slug;

  return (
    <>
      <div className="mt-10 flex flex-wrap items-center justify-between gap-4">
        <p className="stat-k">
          {loading ? "Loading" : `${posts.length} post${posts.length === 1 ? "" : "s"}`}
        </p>
        {!writing && !editing && (
          <button
            onClick={() => { setWriting(true); setError(null); }}
            disabled={categories.length === 0}
            className="btn btn-primary h-9 px-4 text-[13px] disabled:opacity-40"
          >
            <span>Write a post</span>
          </button>
        )}
      </div>

      {writing && (
        <NewsForm categories={categories} busy={busy} error={error}
                  onSubmit={publish} onCancel={() => setWriting(false)} />
      )}
      {editing && (
        <NewsForm existing={editing} categories={categories} busy={busy} error={error}
                  onSubmit={save} onCancel={() => setEditing(null)} />
      )}

      {error && !writing && !editing && (
        <p className="mt-6 text-[13.5px] text-primary-light">{error}</p>
      )}

      {!loading && posts.length === 0 && !writing && (
        <p className="mt-8 text-[15px] text-muted">
          Nothing published yet. A post shows up on the News screen of every launcher
          pointed at this instance.
        </p>
      )}

      {posts.length > 0 && (
        <div className="panel-1 mt-6 overflow-hidden rounded-xl">
          <table className="w-full text-left text-[14px]">
            <thead>
              <tr className="border-b border-white/[0.06]">
                {["Post", "Category", "Date", ""].map((h, i) => (
                  <th key={h} className={`stat-k px-5 py-3.5 font-normal ${i === 3 ? "text-end" : ""}`}>
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-white/[0.05]">
              {posts.map((post, i) => (
                <tr key={post.id}>
                  <td className="px-5 py-4">
                    <b className="block font-medium text-foreground">
                      {post.title}
                      {/* The feed is newest first, so the first row is the one
                          the launcher shows as the big featured card. */}
                      {i === 0 && (
                        <small className="ms-2 font-mono text-[11px] font-normal text-primary-light">
                          featured
                        </small>
                      )}
                    </b>
                    <small className="mt-0.5 block text-[12px] text-subtle">
                      {post.excerpt}
                    </small>
                    <small className="mt-1 block font-mono text-[11.5px] text-faint">
                      {post.id} · {post.body.length} paragraph{post.body.length === 1 ? "" : "s"}
                      {post.badge && ` · badge ${post.badge}`}
                    </small>
                  </td>
                  <td className="px-5 py-4 text-muted">{label(post.category)}</td>
                  <td className="px-5 py-4 font-mono text-[12.5px] text-muted">{post.date}</td>
                  <td className="px-5 py-4">
                    <span className="flex flex-wrap justify-end gap-2">
                      <Action onClick={() => { setEditing(post); setError(null); }} disabled={busy}>
                        Edit
                      </Action>
                      <Action onClick={() => remove(post)} disabled={busy} danger>
                        Remove
                      </Action>
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
