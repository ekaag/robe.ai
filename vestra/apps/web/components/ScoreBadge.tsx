interface ScoreBadgeProps {
  score: number;
}

export function ScoreBadge({ score }: ScoreBadgeProps) {
  const pct = Math.round(score * 100);
  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        justifyContent: "center",
        minWidth: "2.75rem",
        padding: "0.25rem 0.625rem",
        borderRadius: "100px",
        backgroundColor: "var(--color-accent)",
        color: "#fff",
        fontSize: "0.8125rem",
        fontWeight: 600,
        fontFamily: "var(--font-body)",
        letterSpacing: "0.01em",
      }}
    >
      {pct}%
    </span>
  );
}
