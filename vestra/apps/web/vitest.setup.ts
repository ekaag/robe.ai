import "@testing-library/jest-dom";

// jsdom has no ResizeObserver; components like ImageWithOverlay need one to
// measure their container. A no-op stub is enough since tests that assert on
// its output (ImageWithOverlay.test.tsx) install their own mock that captures
// the callback and fires it manually.
if (typeof globalThis.ResizeObserver === "undefined") {
  globalThis.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as unknown as typeof ResizeObserver;
}
