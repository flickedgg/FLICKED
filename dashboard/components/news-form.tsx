"use client";

import { useState } from "react";
import {
  toParagraphs, toText,
  type NewsCategory, type NewsForm as Form, type NewsPost,
} from "@/lib/news";

/* Writing a post, and editing one: the same fields, so the same form.

   The only asymmetry is what the id does. Adding makes one from the title;
   editing leaves the existing one alone however much the title changes, because
   it is what anyone who linked the post is holding. The form says so rather than
   letting an admin wonder. */
export function NewsForm({ existing, categories, busy, error, onSubmit, onCancel }: {
  existing?: NewsPost;
  categories: NewsCategory[];
  busy: boolean;
  error: string | null;
  onSubmit: (form: Form) => void;
  onCancel: () => void;
}) {
  const [category, setCategory] = useState(existing?.category ?? categories[0]?.slug ?? "");
  const [title, setTitle] = useState(existing?.title ?? "");
  const [excerpt, setExcerpt] = useState(existing?.excerpt ?? "");
  const [badge, setBadge] = useState(existing?.badge ?? "");
  const [date, setDate] = useState(existing?.date ?? today());
  const [body, setBody] = useState(existing ? toText(existing.body) : "");

  const paragraphs = toParagraphs(body);

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit({ category, title, excerpt, date, badge, body: paragraphs });
  };

  return (
    <form onSubmit={submit} className="panel-1 mt-6 rounded-xl p-6">
      <p className="stat-k">{existing ? `Edit ${existing.id}` : "Write a post"}</p>

      <div className="mt-5 grid gap-4 sm:grid-cols-2">
        <Field label="Category" hint="Decides the dot colour and the launcher's filter">
          <select value={category} onChange={e => setCategory(e.target.value)} className={select}>
            {categories.map(c => <option key={c.slug} value={c.slug}>{c.label}</option>)}
          </select>
        </Field>

        <Field label="Date" hint="The date on the card. Back-date it freely.">
          <input value={date} onChange={e => setDate(e.target.value)} type="date"
                 required className={input} />
        </Field>

        <Field
          label="Title"
          hint={existing
            ? `The id stays ${existing.id}, so any link to this post keeps working.`
            : "Becomes the post's id, as a slug"}
        >
          <input value={title} onChange={e => setTitle(e.target.value)} required
                 placeholder="Alpha 0.3: bans" className={input} />
        </Field>

        <Field label="Badge" hint="Watermark on the featured card. Usually a version; optional.">
          <input value={badge} onChange={e => setBadge(e.target.value)}
                 placeholder="0.3" className={input} />
        </Field>
      </div>

      <div className="mt-4 grid gap-4">
        <Field label="Summary" hint="One line, on the card and above the article">
          <input value={excerpt} onChange={e => setExcerpt(e.target.value)} required
                 placeholder="Admins can now remove somebody from the servers and the launcher."
                 className={input} />
        </Field>

        <Field
          label="Body"
          hint={`Blank line between paragraphs. ${paragraphs.length} paragraph${
            paragraphs.length === 1 ? "" : "s"
          } so far.`}
        >
          <textarea value={body} onChange={e => setBody(e.target.value)} required rows={10}
                    placeholder={"The first paragraph.\n\nThe second one."}
                    className={`${input} resize-y leading-relaxed`} />
        </Field>
      </div>

      {error && <p className="mt-5 text-[13.5px] text-primary-light">{error}</p>}

      <div className="mt-6 flex flex-wrap gap-3">
        <button type="submit" disabled={busy} className="btn btn-primary">
          <span>{busy ? "Saving…" : existing ? "Save changes" : "Publish"}</span>
        </button>
        <button type="button" onClick={onCancel} className="btn btn-outline">
          <span>Cancel</span>
        </button>
      </div>

      {!existing && (
        <p className="mt-4 text-[12.5px] text-subtle">
          Publishing puts this on every launcher&apos;s News screen straight away.
        </p>
      )}
    </form>
  );
}

/* The <input type="date"> value, which wants yyyy-mm-dd in the admin's own day
   rather than UTC: at 01:00 in Berlin, toISOString() still says yesterday. */
function today(): string {
  const now = new Date();
  return [
    now.getFullYear(),
    String(now.getMonth() + 1).padStart(2, "0"),
    String(now.getDate()).padStart(2, "0"),
  ].join("-");
}

const field =
  "w-full rounded-md border border-white/10 px-3 py-2 text-[14px] " +
  "text-foreground outline-none focus:border-primary/60";

const input = `${field} bg-black/20`;

/* A dropdown needs an opaque colour of its own, and so do its options.

   The translucent bg-black/20 that suits a text box is wrong here: the list is
   drawn by the browser over the page rather than inside it, so there is nothing
   dark behind the 20% black and the white text lands on the system's white.
   globals.css asks for a dark colour-scheme, which handles this in Chrome and
   Edge; naming the colour as well covers the browsers that still paint the list
   from the option's own background. */
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
