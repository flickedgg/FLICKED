import type { Metadata } from "next";
import { DashShell } from "@/components/dash-shell";
import { ServersPanel } from "@/components/servers-panel";

export const metadata: Metadata = { title: "Servers" };

/* No caching: the admin check and the pool both have to be current. */
export const dynamic = "force-dynamic";

export default function ServersPage() {
  return (
    <DashShell page="/servers" title="Servers">
      <ServersPanel />
    </DashShell>
  );
}
