// Per DESIGN.md: GSAP is the only motion library, every animation is gated
// behind prefers-reduced-motion, and the static composition is complete
// without any of it.

export const REDUCED_MOTION = "(prefers-reduced-motion: reduce)";
export const MOTION_OK = "(prefers-reduced-motion: no-preference)";
export const FINE_POINTER = "(hover: hover) and (pointer: fine)";

export const motionOK = () => !matchMedia(REDUCED_MOTION).matches;

