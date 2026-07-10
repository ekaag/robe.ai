/** @type {import('next').NextConfig} */
const config = {
  // No output: "standalone" — Azure SWA Standard tier has native Next.js hosting
  // and serves SSR pages itself. Standalone mode is for self-hosted Node servers
  // (App Service, Docker). On Windows, avoid `next build` directly; use pnpm dev
  // for local dev and let GitHub Actions (Linux) handle CI builds.
  transpilePackages: [
    "@vestra/tokens",
    "@vestra/types",
    "@vestra/api",
    "@vestra/auth",
  ],
};
export default config;
