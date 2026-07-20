import { describe, it, expect, beforeEach } from "vitest";
import { act, fireEvent, render } from "@testing-library/react";
import { ImageWithOverlay } from "../components/ImageWithOverlay";

class MockResizeObserver {
  static instances: MockResizeObserver[] = [];
  callback: ResizeObserverCallback;
  constructor(callback: ResizeObserverCallback) {
    this.callback = callback;
    MockResizeObserver.instances.push(this);
  }
  observe() {}
  unobserve() {}
  disconnect() {}
}

function fireContainerResize(width: number, height: number) {
  const ro = MockResizeObserver.instances[MockResizeObserver.instances.length - 1];
  act(() => {
    ro.callback(
      [{ contentRect: { width, height } } as unknown as ResizeObserverEntry],
      ro as unknown as ResizeObserver
    );
  });
}

function fireImageLoad(img: HTMLImageElement, naturalWidth: number, naturalHeight: number) {
  Object.defineProperty(img, "naturalWidth", { value: naturalWidth, configurable: true });
  Object.defineProperty(img, "naturalHeight", { value: naturalHeight, configurable: true });
  fireEvent.load(img);
}

describe("ImageWithOverlay", () => {
  beforeEach(() => {
    MockResizeObserver.instances = [];
    (globalThis as any).ResizeObserver = MockResizeObserver;
  });

  it("positions face and garment boxes using letterboxed object-fit: contain math", () => {
    const { container, getByAltText } = render(
      <ImageWithOverlay
        src="data:image/jpeg;base64,abc"
        alt="outfit"
        faces={[{ box: { x: 0.1, y: 0.05, width: 0.15, height: 0.2 } }]}
        garments={[{ box: { x: 0.5, y: 0.5, width: 0.2, height: 0.2 }, label: "t-shirt" }]}
      />
    );

    // natural image is 200x100 (2:1); container is 400x300 (4:3) — contain scale is
    // min(400/200, 300/100) = 2, so the image renders at 400x200, letterboxed 50px top/bottom.
    fireContainerResize(400, 300);
    fireImageLoad(getByAltText("outfit") as HTMLImageElement, 200, 100);

    const rects = container.querySelectorAll("svg rect");
    expect(rects).toHaveLength(2);

    const faceRect = rects[0];
    expect(faceRect.getAttribute("x")).toBe(String(0 + 0.1 * 400));
    expect(faceRect.getAttribute("y")).toBe(String(50 + 0.05 * 200));
    expect(faceRect.getAttribute("width")).toBe(String(0.15 * 400));
    expect(faceRect.getAttribute("height")).toBe(String(0.2 * 200));

    const garmentRect = rects[1];
    expect(garmentRect.getAttribute("x")).toBe(String(0 + 0.5 * 400));
    expect(garmentRect.getAttribute("y")).toBe(String(50 + 0.5 * 200));
    expect(garmentRect.getAttribute("width")).toBe(String(0.2 * 400));
    expect(garmentRect.getAttribute("height")).toBe(String(0.2 * 200));

    const label = container.querySelector("svg text");
    expect(label?.textContent).toBe("t-shirt");
  });

  it("renders no overlay rects until both container size and image natural size are known", () => {
    const { container } = render(
      <ImageWithOverlay
        src="data:image/jpeg;base64,abc"
        alt="outfit"
        faces={[{ box: { x: 0.1, y: 0.05, width: 0.15, height: 0.2 } }]}
      />
    );

    expect(container.querySelectorAll("svg")).toHaveLength(0);
  });

  it("highlights a selected garment box distinctly from unselected ones", () => {
    const { container } = render(
      <ImageWithOverlay
        src="data:image/jpeg;base64,abc"
        alt="outfit"
        garments={[
          { box: { x: 0.1, y: 0.1, width: 0.2, height: 0.2 }, label: "jeans", highlighted: true },
          { box: { x: 0.5, y: 0.5, width: 0.2, height: 0.2 }, label: "jacket", highlighted: false },
        ]}
      />
    );

    fireContainerResize(400, 300);
    fireImageLoad(container.querySelector("img") as HTMLImageElement, 200, 100);

    const rects = container.querySelectorAll("svg rect");
    expect(rects[0].getAttribute("stroke-width")).toBe("3");
    expect(rects[1].getAttribute("stroke-width")).toBe("2");
  });
});
