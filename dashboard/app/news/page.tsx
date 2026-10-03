import type { Metadata } from "next";
import { DashShell } from "@/components/dash-shell";
import { NewsPanel } from "@/components/news-panel";

export const metadata: Metadata = { title: "News" };

/* No caching: the admin check has to be current, and a post published a second
   ago should be in the list. */
export const dynamic = "force-dynamic";

export default function NewsPage() {
  return (
    <DashShell page="/news" title="News">
      <NewsPanel />
    </DashShell>
  );
}
