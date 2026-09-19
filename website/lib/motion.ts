// See DESIGN.md §8 (Motion): every animation is gated behind
// prefers-reduced-motion, and the page is complete without any of it.

export const REDUCED_MOTION = "(prefers-reduced-motion: reduce)";
export const MOTION_OK = "(prefers-reduced-motion: no-preference)";
export const FINE_POINTER = "(hover: hover) and (pointer: fine)";

export const motionOK = () => !matchMedia(REDUCED_MOTION).matches;

