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

export const metadata: Metadata = {
  title: "FLICKED | Self-hosted competitive CS2",
  description:
    "FLICKED is a free, open-source, self-hostable competitive platform for CS2: matchmaking, dedicated-server orchestration, rankings, demos and statistics.",
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
