import type { Metadata } from "next";
import { Newsreader, Schibsted_Grotesk, JetBrains_Mono } from "next/font/google";
import "./globals.css";
import { Providers } from "./providers";

// Notebook type system: serif for headings and AI notes, grotesk for UI, mono for every number.
const newsreader = Newsreader({
  variable: "--font-newsreader",
  subsets: ["latin"],
  style: ["normal", "italic"],
});

const schibsted = Schibsted_Grotesk({
  variable: "--font-schibsted",
  subsets: ["latin"],
});

const jetbrains = JetBrains_Mono({
  variable: "--font-jetbrains",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "SensSera — Greenhouse Monitoring",
  description:
    "Multi-tenant smart greenhouse monitoring: real-time sensors, threshold alerts, and analytics.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      lang="en"
      suppressHydrationWarning
      className={`${newsreader.variable} ${schibsted.variable} ${jetbrains.variable} h-full antialiased`}
    >
      <body className="min-h-full flex flex-col">
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
