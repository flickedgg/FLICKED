"use client";

import { gsap } from "gsap";
import { ScrollTrigger } from "gsap/ScrollTrigger";
import { useEffect } from "react";
import { FINE_POINTER, MOTION_OK } from "@/lib/motion";

/* A quiet fade-up as sections enter. Gated on prefers-reduced-motion, and
   nothing is load-bearing: the page is complete without it. */
export function MotionLayer() {
  useEffect(() => {
    gsap.registerPlugin(ScrollTrigger);
    const mm = gsap.matchMedia();

    mm.add(MOTION_OK, () => {
      // only now is it safe to start elements hidden
      document.documentElement.classList.add("rv-ready");

      gsap.utils.toArray<HTMLElement>("[data-rv]").forEach(el => {
        gsap.fromTo(el,
          { opacity: 0, y: 18 },
          { opacity: 1, y: 0, duration: 0.85, ease: "power2.out",
            scrollTrigger: { trigger: el, start: "top 88%", once: true } });
      });

      gsap.utils.toArray<HTMLElement>("[data-rv-panel]").forEach(el => {
        gsap.fromTo(el,
          { opacity: 0, y: 26 },
          { opacity: 1, y: 0, duration: 1, ease: "power2.out",
            scrollTrigger: { trigger: el, start: "top 90%", once: true } });
      });

      // ambient colour drifts on long, deliberately unsynchronised cycles
      ([
        [".amb-1",   90,  60, 1.08, 26],
        [".amb-2", -110,  70, 1.12, 31],
        [".amb-3",   70, -90, 1.06, 37],
      ] as const).forEach(([sel, x, y, scale, duration]) => {
        gsap.to(sel, { x, y, scale, duration, repeat: -1, yoyo: true, ease: "sine.inOut" });
      });

      // …and leans, barely, toward the pointer
      let onPointer: ((e: PointerEvent) => void) | undefined;
      if (matchMedia(FINE_POINTER).matches) {
        const toX = gsap.quickTo(".amb-field", "x", { duration: 2.4, ease: "power3.out" });
        const toY = gsap.quickTo(".amb-field", "y", { duration: 2.4, ease: "power3.out" });
        onPointer = e => {
          toX((e.clientX / innerWidth - 0.5) * 50);
          toY((e.clientY / innerHeight - 0.5) * 36);
        };
        addEventListener("pointermove", onPointer, { passive: true });
      }

      return () => {
        document.documentElement.classList.remove("rv-ready");
        if (onPointer) removeEventListener("pointermove", onPointer);
      };
    });

    return () => mm.revert();
  }, []);

  return null;
}
