interface ConfidenceBarProps {
  value: number; // 0–1
}

export function ConfidenceBar({ value }: ConfidenceBarProps) {
  const pct = Math.round(Math.min(Math.max(value, 0), 1) * 100);
  return (
    <div>
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          marginBottom: "0.25rem",
        }}
      >
        <span style={{ fontSize: "0.75rem", color: "var(--color-ink2)" }}>Confidence</span>
        <span style={{ fontSize: "0.75rem", color: "var(--color-ink2)" }}>{pct}%</span>
      </div>
      <div
        style={{
          height: "0.375rem",
          borderRadius: "100px",
          backgroundColor: "var(--color-line)",
          overflow: "hidden",
        }}
      >
        <div
          style={{
            height: "100%",
            width: `${pct}%`,
            borderRadius: "100px",
            backgroundColor: "var(--color-accent)",
          }}
        />
      </div>
    </div>
  );
}
