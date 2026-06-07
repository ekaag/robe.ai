import type { Garment } from "@vestra/types";

interface GarmentCardProps {
  garment: Garment;
  onClick: () => void;
}

export function GarmentCard({ garment, onClick }: GarmentCardProps) {
  const { primaryColor, category } = garment.traits;
  return (
    <button
      onClick={onClick}
      style={{
        display: "flex",
        flexDirection: "column",
        background: "var(--color-surface)",
        borderRadius: "0.875rem",
        overflow: "hidden",
        border: "1px solid var(--color-line)",
        cursor: "pointer",
        padding: 0,
        textAlign: "left",
        width: "100%",
      }}
    >
      <div style={{ position: "relative", paddingBottom: "125%", overflow: "hidden" }}>
        <img
          src={garment.imageUrl}
          alt={`${category} garment`}
          style={{
            position: "absolute",
            inset: 0,
            width: "100%",
            height: "100%",
            objectFit: "cover",
          }}
        />
      </div>
      <div
        style={{
          padding: "0.625rem 0.75rem",
          display: "flex",
          alignItems: "center",
          gap: "0.5rem",
        }}
      >
        <div
          style={{
            width: "0.75rem",
            height: "0.75rem",
            borderRadius: "50%",
            backgroundColor: primaryColor.hex,
            border: "1px solid var(--color-line)",
            flexShrink: 0,
          }}
        />
        <span
          style={{
            fontSize: "0.8125rem",
            color: "var(--color-ink2)",
            textTransform: "capitalize",
            fontFamily: "var(--font-body)",
          }}
        >
          {category}
        </span>
      </div>
    </button>
  );
}
