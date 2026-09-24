import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /* Build a self-contained server (.next/standalone) that carries only the
     dependencies it actually uses. The dashboard is deployed by copying it to a
     machine that has Node and nothing else: no npm install, no lockfile, no
     network. See docs/DEPLOY.md. */
  output: "standalone",
};

export default nextConfig;
