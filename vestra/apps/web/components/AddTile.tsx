interface AddTileProps {
  onClick: () => void;
  disabled?: boolean;
}

export function AddTile({ onClick, disabled }: AddTileProps) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      aria-label="Add garment"
      style={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        gap: "0.5rem",
        background: "transparent",
        border: "2px dashed var(--color-line)",
        borderRadius: "0.875rem",
        cursor: disabled ? "default" : "pointer",
        color: "var(--color-muted)",
        padding: 0,
        width: "100%",
        aspectRatio: "4 / 5",
      }}
    >
      <span style={{ fontSize: "1.75rem", lineHeight: 1 }}>+</span>
      <span style={{ fontSize: "0.75rem", fontFamily: "var(--font-body)" }}>Add garment</span>
    </button>
  );
}
