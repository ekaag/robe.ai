import { defineConfig } from "vitest/config";
import { resolve } from "path";

export default defineConfig({
  test: {
    environment: "node",
    globals: true,
  },
  resolve: {
    alias: {
      "@vestra/tokens": resolve(__dirname, "../../packages/tokens/src/index.ts"),
      "@vestra/types": resolve(__dirname, "../../packages/types/src/index.ts"),
      // Point directly to the client module (no hooks / React Query dependency)
      // so node-environment tests can import FakeApiClient without loading React.
      "@vestra/api": resolve(__dirname, "../../packages/api/src/client.ts"),
      "@vestra/auth": resolve(__dirname, "../../packages/auth/src/index.ts"),
    },
  },
});
