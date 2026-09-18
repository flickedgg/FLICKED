"use client"; // react-icons uses React context

import { SiDotnet, SiNextdotjs, SiPostgresql, SiRedis, SiRust, SiTauri } from "react-icons/si";

// The real stack, from the README.
const STACK = [
  [SiNextdotjs, "Next.js"],
  [SiDotnet, ".NET"],
  [SiPostgresql, "PostgreSQL"],
  [SiRedis, "Redis"],
  [SiRust, "Rust"],
  [SiTauri, "Tauri"],
] as const;

export function StackRow() {
  return (
    <div className="flex flex-col gap-6 md:flex-row md:items-center md:justify-between">
      <p className="eyebrow">Built with</p>
      <ul className="flex flex-wrap items-center gap-x-10 gap-y-4">
        {STACK.map(([Icon, name]) => (
          <li key={name} className="stack-item" title={name}>
            <Icon aria-hidden="true" />
            <span className={name === ".NET" ? "sr-only" : undefined}>{name}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}
