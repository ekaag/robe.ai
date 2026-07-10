/** @type {import('next').NextConfig} */
const config = {
  // standalone is only needed for Azure SWA deployment (Linux/CI).
  // Set NEXT_OUTPUT=standalone in deploy scripts. Windows dev builds
  // skip it — next build without standalone avoids the EPERM symlink
  // error that occurs on Windows without Developer Mode enabled.
  ...(process.env.NEXT_OUTPUT === "standalone" ? { output: "standalone" } : {}),
  transpilePackages: [
    "@vestra/tokens",
    "@vestra/types",
    "@vestra/api",
    "@vestra/auth",
  ],
};

export default config;
