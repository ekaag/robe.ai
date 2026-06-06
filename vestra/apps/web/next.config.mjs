/** @type {import('next').NextConfig} */
const config = {
  transpilePackages: [
    "@vestra/tokens",
    "@vestra/types",
    "@vestra/api",
    "@vestra/auth",
  ],
};

export default config;
