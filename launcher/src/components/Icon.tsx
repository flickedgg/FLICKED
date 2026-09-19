/* One inline SVG set, 24px grid, 1.8 stroke. No icon library: the whole set is a few hundred bytes. */

const PATHS = {
  play:     "M7 5.5v13a1 1 0 0 0 1.5.86l10.5-6.5a1 1 0 0 0 0-1.72L8.5 4.64A1 1 0 0 0 7 5.5Z",
  matches:  "M4 5h16M4 12h16M4 19h10",
  trophy:   "M8 4h8v5a4 4 0 0 1-8 0V4ZM8 6H5a3 3 0 0 0 3 4M16 6h3a3 3 0 0 1-3 4M12 13v4M8.5 20h7",
  settings: "M4 7h9M17 7h3M4 17h3M11 17h9M15 5v4M9 15v4",
  x:        "M6 6l12 12M18 6 6 18",
  check:    "M5 12.5l4.5 4.5L19 7.5",
  plus:     "M12 5v14M5 12h14",
  crown:    "M4 8l4.5 4L12 6l3.5 6L20 8l-1.8 10H5.8L4 8Z",
  copy:     "M9 9h10v10H9zM5 15V5h10",
  leave:    "M14 5h5v14h-5M10 8l-4 4 4 4M6 12h10",
  news:     "M4 22h16a2 2 0 0 0 2-2V4a2 2 0 0 0-2-2H8a2 2 0 0 0-2 2v16a2 2 0 0 1-4 0v-9a2 2 0 0 1 2-2h2M18 14h-8M15 18h-5M11 6h6a1 1 0 0 1 1 1v2a1 1 0 0 1-1 1h-6a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1Z",
  search:   "M11 18a7 7 0 1 0 0-14 7 7 0 0 0 0 14ZM20 20l-4-4",
} as const;

export type IconName = keyof typeof PATHS;

export function Icon({ name, size = 18 }: { name: IconName; size?: number }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill={name === "play" ? "currentColor" : "none"}
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d={PATHS[name]} />
    </svg>
  );
}
