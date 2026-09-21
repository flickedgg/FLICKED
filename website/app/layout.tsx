import type { Metadata, Viewport } from "next";
import { Barlow_Condensed, Inter, JetBrains_Mono } from "next/font/google";
import "./globals.css";

const barlow = Barlow_Condensed({
  variable: "--font-barlow",
  weight: ["600", "700"],
  style: ["normal", "italic"], // italic only for the FLICKED wordmark
  subsets: ["latin"],
});

const inter = Inter({
  variable: "--font-inter",
  subsets: ["latin"],
});

const jetbrains = JetBrains_Mono({
  variable: "--font-jetbrains",
  weight: ["400", "500"],
  subsets: ["latin"],
});

/* Where the site is served from. Link previews need absolute URLs, so this has to be
   right in production: set NEXT_PUBLIC_SITE_URL once the domain exists. */
const SITE =
  process.env.NEXT_PUBLIC_SITE_URL ??
  (process.env.VERCEL_PROJECT_PRODUCTION_URL
    ? `https://${process.env.VERCEL_PROJECT_PRODUCTION_URL}`
    : "http://localhost:3000");

const TITLE = "FLICKED | Self-hosted competitive CS2";
const DESCRIPTION =
  "A free, open-source, self-hostable competitive platform for CS2: matchmaking, parties, map vote, ratings, match history and demos. You bring the servers.";

export const metadata: Metadata = {
  metadataBase: new URL(SITE),
  title: {
    default: TITLE,
    template: "%s | FLICKED", // future pages get this automatically
  },
  description: DESCRIPTION,
  applicationName: "FLICKED",
  keywords: [
    "CS2", "Counter-Strike 2", "matchmaking", "self-hosted", "open source",
    "pug", "competitive", "dedicated server", "ranking", "demos",
  ],
  alternates: { canonical: "/" },
  openGraph: {
    type: "website",
    siteName: "FLICKED",
    title: TITLE,
    description: DESCRIPTION,
    url: "/",
    locale: "en",
  },
  twitter: {
    card: "summary_large_image",
    title: TITLE,
    description: DESCRIPTION,
  },
  robots: { index: true, follow: true },
};

export const viewport: Viewport = {
  viewportFit: "cover",
  themeColor: "#0c0c0d",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html
      lang="en"
      data-scroll-behavior="smooth"
      className={`${barlow.variable} ${inter.variable} ${jetbrains.variable}`}
    >
      <body className="bg-bg font-sans text-muted antialiased">{children}</body>
    </html>
  );
}
