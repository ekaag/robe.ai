interface TraitRowProps {
  label: string;
  value: string;
}

export function TraitRow({ label, value }: TraitRowProps) {
  return (
    <div
      style={{
        display: "flex",
        justifyContent: "space-between",
        alignItems: "center",
        padding: "0.625rem 0",
        borderBottom: "1px solid var(--color-line)",
      }}
    >
      <span style={{ color: "var(--color-ink2)", fontSize: "0.875rem" }}>{label}</span>
      <span
        style={{
          color: "var(--color-ink)",
          fontSize: "0.875rem",
          fontWeight: 500,
          textTransform: "capitalize",
        }}
      >
        {value}
      </span>
    </div>
  );
}
