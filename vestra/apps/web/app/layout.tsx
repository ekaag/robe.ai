import type { Metadata } from "next";
import "@fontsource/fraunces";
import "@fontsource/hanken-grotesk";
import { tokens } from "@vestra/tokens";
import "./globals.css";

export const metadata: Metadata = {
  title: "Vestra",
  description: "Your personal wardrobe stylist",
};

// Generate :root CSS variables from the tokens package (single source of truth).
const colorVars = (Object.entries(tokens.color) as [string, string][])
  .map(([k, v]) => `--color-${k}:${v}`)
  .join(";");

const rootCss =
  `:root{${colorVars};` +
  `--font-display:"${tokens.font.display}",Georgia,serif;` +
  `--font-body:"${tokens.font.body}",system-ui,sans-serif}`;

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <head>
        {/* eslint-disable-next-line react/no-danger */}
        <style dangerouslySetInnerHTML={{ __html: rootCss }} />
      </head>
      <body>{children}</body>
    </html>
  );
}
