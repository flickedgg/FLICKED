import type { Metadata, Viewport } from "next";
import { Barlow_Condensed, Inter, JetBrains_Mono } from "next/font/google";
import { Ambient } from "@/components/ambient";
import { MotionLayer } from "@/components/motion-layer";
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

/* An admin panel, so no social preview and no search engines: there is nothing
   here for anyone who is not signed in, and a listed admin URL is an invitation. */
export const metadata: Metadata = {
  title: { default: "FLICKED Dashboard", template: "%s | FLICKED Dashboard" },
  description: "Administration for a FLICKED instance.",
  robots: { index: false, follow: false },
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
      <body className="bg-bg font-sans text-muted antialiased">
        {/* the same background the website has: rails, then the drifting fields */}
        <div className="frame-rails" aria-hidden="true"><i /><i /></div>
        <Ambient />
        {children}
        <MotionLayer />
      </body>
    </html>
  );
}
