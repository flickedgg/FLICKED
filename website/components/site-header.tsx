"use client";

import { gsap } from "gsap";
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import { motionOK } from "@/lib/motion";
import { GitHubIcon, LINKS, Logo } from "./brand";

export const SECTIONS = [
  ["features", "Features"],
  ["self-host", "Self-host"],
  ["compare", "Compare"],
  ["open-source", "Open source"],
  ["faq", "FAQ"],
] as const;

type Section = (typeof SECTIONS)[number][0];

/* Rests flat inside the frame at the top of the page, then condenses into a
   floating plane-2 bar once the page scrolls. */
export function SiteHeader() {
  const [condensed, setCondensed] = useState(false);
  const [current, setCurrent] = useState<Section | null>(null);
  const [sheetOpen, setSheetOpen] = useState(false);
  const [sheetHidden, setSheetHidden] = useState(true);

  const navRef = useRef<HTMLElement>(null);
  const indicatorRef = useRef<HTMLSpanElement>(null);
  const sheetRef = useRef<HTMLDivElement>(null);
  const menuToggleRef = useRef<HTMLButtonElement>(null);
  const linkRefs = useRef<Partial<Record<Section, HTMLAnchorElement | null>>>({});

  // mirrors of state for listeners that outlive a render
  const sheetOpenRef = useRef(false);
  const currentRef = useRef<Section | null>(null);

  /* ── sliding indicator ──
     Follows the pointer / focus, then settles on the section in view. */
  const restingLink = useCallback(
    () => (currentRef.current ? (linkRefs.current[currentRef.current] ?? null) : null),
    [],
  );

  const placeIndicator = useCallback((link: HTMLElement | null) => {
    const indicator = indicatorRef.current;
    if (!indicator) return;
    if (!link || innerWidth < 1024) { indicator.style.opacity = "0"; return; }

    // when appearing, fade in place instead of sliding in from the left edge
    const appearing = indicator.style.opacity !== "1";
    if (appearing) indicator.style.transition = "opacity .25s";

    indicator.style.width = link.offsetWidth + "px";
    indicator.style.transform = `translateX(${link.offsetLeft}px)`;
    indicator.style.opacity = "1";

    if (appearing) {
      void indicator.offsetWidth;
      requestAnimationFrame(() => { indicator.style.transition = ""; });
    }
  }, []);

  /* ── mobile sheet ── */
  const openSheet = useCallback(() => {
    if (sheetOpenRef.current) return;
    sheetOpenRef.current = true;
    setSheetOpen(true);
    setSheetHidden(false);
  }, []);

  const closeSheet = useCallback((returnFocus = false) => {
    if (!sheetOpenRef.current) return;
    sheetOpenRef.current = false;
    setSheetOpen(false);
    if (returnFocus) menuToggleRef.current?.focus();
  }, []);

  const firstSheetRun = useRef(true);
  useLayoutEffect(() => {
    if (firstSheetRun.current) { firstSheetRun.current = false; return; }
    const sheet = sheetRef.current;
    if (!sheet) return;
    const animate = motionOK();

    if (sheetOpen) {
      document.body.style.overflow = "hidden";
      if (animate) {
        gsap.fromTo(sheet, { opacity: 0 }, { opacity: 1, duration: 0.24, ease: "power1.out", overwrite: true });
        gsap.fromTo(sheet.querySelectorAll("[data-m]"), { opacity: 0, x: -14 },
          { opacity: 1, x: 0, duration: 0.5, stagger: 0.04, delay: 0.04, ease: "power2.out", overwrite: true });
      }
      return;
    }

    document.body.style.overflow = "";
    const done = () => { if (!sheetOpenRef.current) setSheetHidden(true); };
    if (animate) {
      gsap.to(sheet, { opacity: 0, duration: 0.18, ease: "power1.in", overwrite: true, onComplete: done });
    } else {
      done();
    }
  }, [sheetOpen]);

  /* ── condense on scroll ── */
  useEffect(() => {
    const onScroll = () => setCondensed(scrollY > 16);
    addEventListener("scroll", onScroll, { passive: true });
    onScroll();
    return () => removeEventListener("scroll", onScroll);
  }, []);

  /* ── current section ── */
  useEffect(() => {
    const observer = new IntersectionObserver(entries => {
      entries.forEach(en => {
        const id = en.target.id as Section;
        if (en.isIntersecting) currentRef.current = id;
        else if (currentRef.current === id) currentRef.current = null;
      });
      setCurrent(currentRef.current);
      const nav = navRef.current;
      if (nav && !nav.matches(":hover") && !nav.contains(document.activeElement)) placeIndicator(restingLink());
    }, { rootMargin: "-42% 0px -52% 0px" });

    SECTIONS.forEach(([id]) => {
      const section = document.getElementById(id);
      if (section) observer.observe(section);
    });

    let alive = true;
    document.fonts?.ready.then(() => { if (alive) placeIndicator(restingLink()); });

    return () => { alive = false; observer.disconnect(); };
  }, [placeIndicator, restingLink]);

  /* ── escape, resize ── */
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") closeSheet(true); };
    const onResize = () => {
      if (innerWidth >= 1024) closeSheet();
      placeIndicator(restingLink());
    };
    addEventListener("keydown", onKey);
    addEventListener("resize", onResize);
    return () => {
      removeEventListener("keydown", onKey);
      removeEventListener("resize", onResize);
      document.body.style.overflow = "";
    };
  }, [closeSheet, placeIndicator, restingLink]);

  const pointAt = (e: React.SyntheticEvent) => {
    const link = (e.target as Element).closest<HTMLElement>(".nav-link");
    if (link) placeIndicator(link);
  };

  return (
    <>
      <header id="header" className={`site-header${condensed ? " is-condensed" : ""}`}>
        <div className="mx-auto flex h-full max-w-frame items-center px-4 lg:px-10">
          <div className="header-bar">

            <a href="#" className="brand" aria-label="FLICKED, home">
              <Logo size={30} />
              <span className="wordmark">FLICKED</span>
            </a>

            <nav
              ref={navRef}
              className="nav-group"
              aria-label="Main"
              onMouseOver={pointAt}
              onFocus={pointAt}
              onMouseLeave={() => placeIndicator(restingLink())}
              onBlur={e => { if (!navRef.current?.contains(e.relatedTarget)) placeIndicator(restingLink()); }}
            >
              <span ref={indicatorRef} className="nav-indicator" aria-hidden="true" />
              {SECTIONS.map(([id, label]) => (
                <a
                  key={id}
                  ref={el => { linkRefs.current[id] = el; }}
                  className={`nav-link${current === id ? " is-current" : ""}`}
                  href={`#${id}`}
                  aria-current={current === id ? "location" : undefined}
                >
                  {label}
                </a>
              ))}
            </nav>

            <div className="header-actions">
              <a href={LINKS.repo} className="quiet-link" target="_blank" rel="noreferrer">
                <GitHubIcon />GitHub
              </a>
              <a href="#self-host" className="btn btn-primary btn-nav"><span>Get started</span></a>
              <button
                id="menu"
                ref={menuToggleRef}
                className="menu-toggle"
                aria-label={sheetOpen ? "Close menu" : "Open menu"}
                aria-expanded={sheetOpen}
                aria-controls="mobileNav"
                onClick={() => (sheetOpenRef.current ? closeSheet() : openSheet())}
              >
                <span className="burger"><i /><i /></span>
              </button>
            </div>
          </div>
        </div>
      </header>

      {/* mobile sheet — lives outside the header so the bar's backdrop
          filter can't become its containing block */}
      <div id="mobileNav" ref={sheetRef} className="mobile-sheet" hidden={sheetHidden}>
        <div className="mx-auto flex min-h-full max-w-frame flex-col px-6 pb-10 pt-6">
          <nav className="flex flex-col" aria-label="Mobile">
            {SECTIONS.map(([id, label]) => (
              <a
                key={id}
                className={`m-link${current === id ? " is-current" : ""}`}
                href={`#${id}`}
                aria-current={current === id ? "location" : undefined}
                data-m
                onClick={() => closeSheet()}
              >
                {label}
              </a>
            ))}
          </nav>

          <div className="mt-auto grid gap-3 pt-12" data-m>
            <a href="#self-host" className="btn btn-primary btn-lg w-full" onClick={() => closeSheet()}><span>Get started</span></a>
            <a href={LINKS.repo} className="btn btn-outline btn-lg w-full" target="_blank" rel="noreferrer"><span><GitHubIcon />View on GitHub</span></a>
          </div>
          <p className="mt-6 flex items-center justify-center gap-2.5 font-mono text-[12px] text-subtle" data-m>
            <i className="tag-dot" />Alpha · free and open source
          </p>
        </div>
      </div>
    </>
  );
}
