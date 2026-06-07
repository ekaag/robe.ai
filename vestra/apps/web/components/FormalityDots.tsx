interface FormalityDotsProps {
  value: number; // 1–5
  max?: number;
}

export function FormalityDots({ value, max = 5 }: FormalityDotsProps) {
  return (
    <div style={{ display: "flex", gap: "0.3rem", alignItems: "center" }}>
      {Array.from({ length: max }, (_, i) => (
        <div
          key={i}
          aria-hidden="true"
          style={{
            width: "0.625rem",
            height: "0.625rem",
            borderRadius: "50%",
            backgroundColor: i < value ? "var(--color-accent)" : "var(--color-line)",
            flexShrink: 0,
          }}
        />
      ))}
    </div>
  );
}
