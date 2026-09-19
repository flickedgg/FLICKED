import { isTauri } from "@tauri-apps/api/core";
import { openUrl } from "@tauri-apps/plugin-opener";

// external links open in the user's browser, not inside the launcher window
export function openExternal(url: string) {
  if (isTauri()) openUrl(url).catch(() => {});
  else window.open(url, "_blank", "noopener");
}
