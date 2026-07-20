import { defineConfig } from "orval";

// Run via: pnpm gen:api (from vestra/ root)
// Reads ../../../contracts/openapi.json once the backend commits it.
// Output overwrites src/generated-client.ts and src/generated-hooks.ts.
export default defineConfig({
  vestra: {
    input: "../../../contracts/openapi.json",
    output: {
      mode: "tags-split",
      target: "./src/generated-client.ts",
      client: "react-query",
    },
  },
});
