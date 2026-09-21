/* Relative times for timestamps coming from the backend. Intl does the
   pluralising and wording, so this stays right in other languages too. */

const rtf = new Intl.RelativeTimeFormat(undefined, { numeric: "auto" });

const UNITS: [limit: number, seconds: number, unit: Intl.RelativeTimeFormatUnit][] = [
  [60, 1, "second"],
  [3600, 60, "minute"],
  [86400, 3600, "hour"],
  [604800, 86400, "day"],
  [2629800, 604800, "week"],
  [31557600, 2629800, "month"],
  [Infinity, 31557600, "year"],
];

// "2 hours ago", "yesterday", "3 days ago"
export function timeAgo(iso: string): string {
  const seconds = (Date.parse(iso) - Date.now()) / 1000;
  const abs = Math.abs(seconds);
  const [, size, unit] = UNITS.find(([limit]) => abs < limit)!;
  return rtf.format(Math.round(seconds / size), unit);
}
