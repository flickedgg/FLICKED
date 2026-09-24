import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
// @ts-expect-error type error without @types/node package
import process from "node:process";
const host = process.env.TAURI_DEV_HOST;

// https://vite.dev/config/
export default defineConfig(() => ({
  plugins: [react()],

  define: {
    __APP_VERSION__: JSON.stringify(process.env.npm_package_version),

    /* The API address, baked in at build time from the same FLICKED_API that
       src-tauri/src/auth.rs reads. One variable on purpose: when the webview and
       the Rust side took their address from different places, a launcher could
       be built where signing in worked and the news screen quietly called the
       machine it was built on. */
    __FLICKED_API__: JSON.stringify(process.env.FLICKED_API ?? "http://localhost:5165"),
  },

  // the webview is always a modern engine (WebView2 on Windows), so skip legacy transforms
  build: {
    target: "es2022",
    cssTarget: "chrome110",
    reportCompressedSize: false,
  },

  // Vite options tailored for Tauri development and only applied in `tauri dev` or `tauri build`
  //
  // 1. prevent Vite from obscuring rust errors
  clearScreen: false,
  // 2. tauri expects a fixed port, fail if that port is not available
  server: {
    port: 1420,
    strictPort: true,
    host: host || false,
    hmr: host
      ? {
          protocol: "ws",
          host,
          port: 1421,
        }
      : undefined,
    watch: {
      // 3. tell Vite to ignore watching `src-tauri`
      ignored: ["**/src-tauri/**"],
    },
  },
}));
