"use client";

import { useEffect, useState } from "react";

/* Soft colour fields behind the page. Each section sets a tone; the crossfade
   is CSS. The slow drift and pointer lean live in <MotionLayer>. */
export function Ambient() {
  const [tone, setTone] = useState("hero");

  useEffect(() => {
    const observer = new IntersectionObserver(entries => {
      entries.forEach(en => {
        if (en.isIntersecting) setTone((en.target as HTMLElement).dataset.tone ?? "hero");
      });
    }, { rootMargin: "-45% 0px -50% 0px" });
    document.querySelectorAll("main > section[data-tone]").forEach(s => observer.observe(s));
    return () => observer.disconnect();
  }, []);

  return (
    <div className="ambient" data-tone={tone} aria-hidden="true">
      <div className="amb-field"><i className="amb amb-1" /><i className="amb amb-2" /><i className="amb amb-3" /></div>
    </div>
  );
}
