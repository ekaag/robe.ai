/** @type {import('next').NextConfig} */
const config = {
  // Static export — produces out/ with pre-rendered HTML/CSS/JS.
  // No Next.js API routes are used (backend is robe.api/.NET); no SSR needed.
  // Avoids SWA's Node runtime entirely (no Node 18 vs 20 mismatch, no warmup timeout).
  output: "export",
  images: { unoptimized: true }, // required with output: "export" — no Image Optimization API
  transpilePackages: [
    "@vestra/tokens",
    "@vestra/types",
    "@vestra/api",
    "@vestra/auth",
  ],
};
export default config;
