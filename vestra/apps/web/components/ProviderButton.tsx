"use client";

import type { AuthProvider } from "@vestra/auth";

interface Props {
  provider: AuthProvider;
  label: string;
  onClick: () => void;
}

export function ProviderButton({ label, onClick }: Props) {
  return (
    <button
      type="button"
      onClick={onClick}
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        width: "100%",
        padding: "14px 22px",
        backgroundColor: "var(--color-surface)",
        border: "1px solid var(--color-line)",
        borderRadius: "14px",
        color: "var(--color-ink)",
        fontSize: "1rem",
        fontFamily: "var(--font-body)",
        fontWeight: 500,
        cursor: "pointer",
      }}
    >
      {label}
    </button>
  );
}
