"use client";

import { useEffect, useRef, useState } from "react";
import type { BoundingBox } from "@vestra/types";

export interface FaceMarker {
  box: BoundingBox;
}

export interface GarmentMarker {
  box: BoundingBox;
  label: string;
  highlighted?: boolean;
}

interface ImageWithOverlayProps {
  src: string;
  alt: string;
  faces?: FaceMarker[];
  garments?: GarmentMarker[];
  style?: React.CSSProperties;
}

interface Size {
  width: number;
  height: number;
}

// Maps a normalized (0-1, top-left origin) box onto the pixel rect the image is
// actually drawn at inside the container, replicating CSS `object-fit: contain`
// letterboxing math — the container's aspect ratio rarely matches the image's.
function containedRect(container: Size, natural: Size) {
  if (!container.width || !container.height || !natural.width || !natural.height) {
    return null;
  }
  const scale = Math.min(container.width / natural.width, container.height / natural.height);
  const width = natural.width * scale;
  const height = natural.height * scale;
  return {
    offsetX: (container.width - width) / 2,
    offsetY: (container.height - height) / 2,
    width,
    height,
  };
}

export function ImageWithOverlay({ src, alt, faces = [], garments = [], style }: ImageWithOverlayProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [containerSize, setContainerSize] = useState<Size>({ width: 0, height: 0 });
  const [naturalSize, setNaturalSize] = useState<Size>({ width: 0, height: 0 });

  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;
    const observer = new ResizeObserver(([entry]) => {
      const { width, height } = entry.contentRect;
      setContainerSize({ width, height });
    });
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  const handleImageLoad = (e: React.SyntheticEvent<HTMLImageElement>) => {
    setNaturalSize({
      width: e.currentTarget.naturalWidth,
      height: e.currentTarget.naturalHeight,
    });
  };

  const rect = containedRect(containerSize, naturalSize);

  const toPixelBox = (box: BoundingBox) => {
    if (!rect) return null;
    return {
      left: rect.offsetX + box.x * rect.width,
      top: rect.offsetY + box.y * rect.height,
      width: box.width * rect.width,
      height: box.height * rect.height,
    };
  };

  return (
    <div
      ref={containerRef}
      style={{
        position: "relative",
        width: "100%",
        background: "var(--color-bg2)",
        borderRadius: "0.625rem",
        overflow: "hidden",
        ...style,
      }}
    >
      <img
        src={src}
        alt={alt}
        onLoad={handleImageLoad}
        style={{ display: "block", width: "100%", height: "100%", objectFit: "contain" }}
      />
      {rect && (
        <svg
          aria-hidden="true"
          style={{ position: "absolute", inset: 0, width: "100%", height: "100%", pointerEvents: "none" }}
        >
          {faces.map((face, i) => {
            const px = toPixelBox(face.box);
            if (!px) return null;
            return (
              <rect
                key={`face-${i}`}
                x={px.left}
                y={px.top}
                width={px.width}
                height={px.height}
                fill="none"
                stroke="var(--color-ink2)"
                strokeWidth={2}
                strokeDasharray="4 3"
                rx={4}
              />
            );
          })}
          {garments.map((item, i) => {
            const px = toPixelBox(item.box);
            if (!px) return null;
            const stroke = item.highlighted ? "var(--color-accent)" : "var(--color-accent2)";
            return (
              <g key={`garment-${i}`}>
                <rect
                  x={px.left}
                  y={px.top}
                  width={px.width}
                  height={px.height}
                  fill={item.highlighted ? "rgba(156, 74, 46, 0.12)" : "none"}
                  stroke={stroke}
                  strokeWidth={item.highlighted ? 3 : 2}
                  rx={4}
                />
                <text
                  x={px.left + 4}
                  y={px.top + 14}
                  fontSize={11}
                  fontFamily="var(--font-body)"
                  fill={stroke}
                  style={{ paintOrder: "stroke", stroke: "var(--color-surface)", strokeWidth: 3 }}
                >
                  {item.label}
                </text>
              </g>
            );
          })}
        </svg>
      )}
    </div>
  );
}
