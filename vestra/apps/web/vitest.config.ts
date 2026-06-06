import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import { resolve } from "path";

export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./vitest.setup.ts"],
  },
  resolve: {
    alias: {
      "@vestra/tokens": resolve(__dirname, "../../packages/tokens/src/index.ts"),
      "@vestra/types": resolve(__dirname, "../../packages/types/src/index.ts"),
      "@vestra/api": resolve(__dirname, "../../packages/api/src/index.ts"),
      "@vestra/auth": resolve(__dirname, "../../packages/auth/src/index.ts"),
    },
  },
});
