import type { GarmentCategory } from "@vestra/types";

export type FilterOption = GarmentCategory | "all";

interface FilterChipsProps {
  options: FilterOption[];
  selected: FilterOption;
  onChange: (value: FilterOption) => void;
}

export function FilterChips({ options, selected, onChange }: FilterChipsProps) {
  return (
    <div
      style={{
        display: "flex",
        gap: "0.5rem",
        flexWrap: "wrap",
      }}
    >
      {options.map((opt) => {
        const active = selected === opt;
        return (
          <button
            key={opt}
            onClick={() => onChange(opt)}
            style={{
              padding: "0.375rem 0.875rem",
              borderRadius: "100px",
              border: `1px solid ${active ? "var(--color-ink)" : "var(--color-line)"}`,
              backgroundColor: active ? "var(--color-ink)" : "transparent",
              color: active ? "var(--color-bg)" : "var(--color-ink2)",
              fontSize: "0.8125rem",
              fontFamily: "var(--font-body)",
              cursor: "pointer",
              textTransform: "capitalize",
            }}
          >
            {opt}
          </button>
        );
      })}
    </div>
  );
}
