import { defineConfig } from "vitest/config";
import { resolve } from "path";

export default defineConfig({
  test: {
    environment: "node",
    globals: true,
  },
  resolve: {
    alias: {
      "@vestra/types": resolve(__dirname, "../../packages/types/src/index.ts"),
    },
  },
});
