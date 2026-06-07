import type { ColorWeight } from "@vestra/types";

interface PaletteStripProps {
  palette: ColorWeight[];
}

export function PaletteStrip({ palette }: PaletteStripProps) {
  return (
    <div
      role="list"
      aria-label="Color palette"
      style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap" }}
    >
      {palette.map((color) => (
        <div
          key={color.hex}
          role="listitem"
          style={{
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            gap: "0.375rem",
          }}
        >
          <div
            aria-label={color.name}
            style={{
              width: "2.5rem",
              height: "2.5rem",
              borderRadius: "9px",
              backgroundColor: color.hex,
              border: "1px solid var(--color-line)",
              flexShrink: 0,
            }}
          />
          <span
            style={{
              fontSize: "0.6875rem",
              color: "var(--color-ink2)",
              fontFamily: "var(--font-body)",
              textTransform: "capitalize",
            }}
          >
            {color.name}
          </span>
        </div>
      ))}
    </div>
  );
}
