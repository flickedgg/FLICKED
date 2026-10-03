"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

/* Moving between the dashboard's pages.

   A client component only because it marks the current one, which means reading
   the path in the browser. Built from utilities rather than the site header's
   .nav-link, which hides itself below 1024px: an admin fixing something from a
   phone still needs to be able to get to the other page. */
const PAGES = [
  { href: "/servers", label: "Servers" },
  { href: "/news", label: "News" },
] as const;

export function DashNav() {
  const here = usePathname();

  return (
    <nav className="mt-10 flex gap-1 border-b border-white/[0.06]" aria-label="Dashboard">
      {PAGES.map(({ href, label }) => {
        const current = here === href;
        return (
          <Link
            key={href}
            href={href}
            aria-current={current ? "page" : undefined}
            className={`-mb-px border-b px-4 py-3 text-[14px] transition-colors ${
              current
                ? "border-primary text-foreground"
                : "border-transparent text-subtle hover:text-foreground"
            }`}
          >
            {label}
          </Link>
        );
      })}
    </nav>
  );
}
