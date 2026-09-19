import { useEffect, useRef, useState } from "react";
import { NEWS, NEWS_CATEGORIES, type NewsCategory, type NewsPost } from "../data/demo";
import { Icon } from "../components/Icon";

const LABEL = Object.fromEntries(NEWS_CATEGORIES) as Record<NewsCategory, string>;

function Meta({ post }: { post: NewsPost }) {
  return (
    <p className={`news-meta is-${post.category}`}>
      <i />{LABEL[post.category]} · {post.date}
    </p>
  );
}

function Featured({ post, onOpen }: { post: NewsPost; onOpen: () => void }) {
  return (
    <button className="news-feature" onClick={onOpen}>
      {post.badge && <span className="news-badge" aria-hidden="true">{post.badge}</span>}
      <span className="eyebrow">Latest</span>
      <b className="display news-feature-title">{post.title}</b>
      <span className="news-feature-text">{post.excerpt}</span>
      <Meta post={post} />
      <span className="news-more">Read more <Icon name="arrow" size={15} /></span>
    </button>
  );
}

function Card({ post, onOpen }: { post: NewsPost; onOpen: () => void }) {
  return (
    <li>
      <button className="news-card" onClick={onOpen}>
        <Meta post={post} />
        <b className="news-card-title">{post.title}</b>
        <span className="news-excerpt">{post.excerpt}</span>
        <span className="news-more">Read more <Icon name="arrow" size={14} /></span>
      </button>
    </li>
  );
}

function Article({ post, onBack, onOpen }: {
  post: NewsPost; onBack: () => void; onOpen: (p: NewsPost) => void;
}) {
  const more = NEWS.filter(p => p.id !== post.id).slice(0, 4);
  return (
    <div className="news-read">
      <article className="news-article">
        <button className="text-btn news-back" onClick={onBack}>
          <Icon name="back" size={14} />All news
        </button>
        <Meta post={post} />
        <h1 className="display news-article-title">{post.title}</h1>
        <p className="news-lead">{post.excerpt}</p>
        {post.body.map((para, i) => <p key={i}>{para}</p>)}
      </article>

      <aside className="news-more-list">
        <p className="stat-k">More news</p>
        <ul>
          {more.map(p => (
            <li key={p.id}>
              <button onClick={() => onOpen(p)}>
                <Meta post={p} />
                <b>{p.title}</b>
              </button>
            </li>
          ))}
        </ul>
      </aside>
    </div>
  );
}

export default function News() {
  const [filter, setFilter] = useState<NewsCategory | "all">("all");
  const [open, setOpen] = useState<NewsPost | null>(null);
  const top = useRef<HTMLDivElement>(null);

  // opening or closing an article starts at the top, not where the list was scrolled to
  useEffect(() => { top.current?.scrollIntoView({ block: "start" }); }, [open]);

  if (open) {
    return (
      <div className="view" ref={top}>
        <Article post={open} onBack={() => setOpen(null)} onOpen={setOpen} />
      </div>
    );
  }

  const shown = filter === "all" ? NEWS : NEWS.filter(p => p.category === filter);
  const [first, ...others] = shown;

  return (
    <div className="view" ref={top}>
      <header className="page-head">
        <div className="view-head">
          <p className="eyebrow">News</p>
          <h1 className="display h1">Latest <span className="accent">news</span></h1>
        </div>

        <div className="seg" role="radiogroup" aria-label="Filter news">
          {([["all", "All"], ...NEWS_CATEGORIES] as const).map(([id, label]) => (
            <button
              key={id}
              role="radio"
              aria-checked={filter === id}
              className={`seg-btn${filter === id ? " is-on" : ""}`}
              onClick={() => setFilter(id)}
            >
              {label}
            </button>
          ))}
        </div>
      </header>

      {first ? (
        <>
          <Featured post={first} onOpen={() => setOpen(first)} />
          {others.length > 0 && (
            <ul className="news-grid">
              {others.map(p => <Card key={p.id} post={p} onOpen={() => setOpen(p)} />)}
            </ul>
          )}
        </>
      ) : (
        <p className="news-empty">Nothing here yet.</p>
      )}
    </div>
  );
}
