"use client";

import { useState } from "react";
import { useRecommendations, useStyleProfile } from "@vestra/api";
import type { GarmentCategory, RecommendationContext } from "@vestra/types";
import type { FilterOption } from "../../components/FilterChips";
import { FilterChips } from "../../components/FilterChips";
import { ScoreBadge } from "../../components/ScoreBadge";

const FILTER_OPTIONS: FilterOption[] = [
  "all",
  "top",
  "bottom",
  "dress",
  "outerwear",
  "footwear",
  "accessory",
];

export default function ShopPage() {
  const [filter, setFilter] = useState<FilterOption>("all");
  const [budget, setBudget] = useState<string>("");
  const { data: profile, isLoading: profileLoading } = useStyleProfile();

  const ctx: RecommendationContext = {
    ...(filter !== "all" ? { categories: [filter as GarmentCategory] } : {}),
    ...(budget && Number(budget) > 0 ? { maxBudget: Number(budget) } : {}),
  };

  const hasProfile = profile !== null && profile !== undefined;

  const {
    data: recommendations = [],
    isLoading,
    isError,
  } = useRecommendations(ctx, hasProfile);

  if (profileLoading) {
    return (
      <div style={{ padding: "2rem" }}>
        <p style={{ color: "var(--color-muted)", fontSize: "0.9375rem" }}>Loading...</p>
      </div>
    );
  }

  if (!hasProfile) {
    return (
      <div style={{ padding: "2rem", maxWidth: "480px" }}>
        <h1
          style={{
            fontFamily: "var(--font-display)",
            fontSize: "1.875rem",
            color: "var(--color-ink)",
            margin: "0 0 0.75rem",
          }}
        >
          Shop
        </h1>
        <p style={{ color: "var(--color-muted)", fontSize: "0.9375rem" }}>
          Generate a style profile first to get personalized recommendations.
        </p>
      </div>
    );
  }

  return (
    <div style={{ padding: "2rem" }}>
      <h1
        style={{
          fontFamily: "var(--font-display)",
          fontSize: "1.875rem",
          color: "var(--color-ink)",
          margin: "0 0 1.5rem",
        }}
      >
        Shop
      </h1>

      {/* Filters */}
      <div
        style={{
          display: "flex",
          flexWrap: "wrap",
          alignItems: "center",
          gap: "1rem",
          marginBottom: "1.5rem",
        }}
      >
        <FilterChips options={FILTER_OPTIONS} selected={filter} onChange={setFilter} />
        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
          <label
            htmlFor="budget"
            style={{ fontSize: "0.8125rem", color: "var(--color-muted)" }}
          >
            Budget
          </label>
          <input
            id="budget"
            type="number"
            min="0"
            placeholder="Max $"
            value={budget}
            onChange={(e) => setBudget(e.target.value)}
            style={{
              width: "5.5rem",
              padding: "0.375rem 0.625rem",
              borderRadius: "0.5rem",
              border: "1px solid var(--color-line)",
              background: "var(--color-surface)",
              color: "var(--color-ink)",
              fontFamily: "var(--font-body)",
              fontSize: "0.875rem",
              outline: "none",
            }}
          />
        </div>
      </div>

      {isLoading ? (
        <p style={{ color: "var(--color-muted)", fontSize: "0.9375rem" }}>
          Finding recommendations...
        </p>
      ) : isError ? (
        <p style={{ color: "var(--color-ink2)", fontSize: "0.9375rem" }}>
          Something went wrong loading recommendations.
        </p>
      ) : recommendations.length === 0 ? (
        <p style={{ color: "var(--color-muted)", fontSize: "0.9375rem" }}>
          No recommendations match your filters.
        </p>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          {recommendations.map((rec) => (
            <a
              key={rec.inventoryItem.id}
              href={rec.inventoryItem.url}
              target="_blank"
              rel="noopener noreferrer"
              style={{
                display: "flex",
                gap: "1rem",
                padding: "1rem",
                background: "var(--color-surface)",
                borderRadius: "0.875rem",
                border: "1px solid var(--color-line)",
                textDecoration: "none",
                color: "inherit",
                transition: "border-color 0.15s",
              }}
              onMouseEnter={(e) =>
                (e.currentTarget.style.borderColor = "var(--color-accent)")
              }
              onMouseLeave={(e) =>
                (e.currentTarget.style.borderColor = "var(--color-line)")
              }
            >
              {/* Thumbnail */}
              <div
                style={{
                  width: "80px",
                  height: "100px",
                  flexShrink: 0,
                  borderRadius: "0.5rem",
                  overflow: "hidden",
                  background: "var(--color-bg2)",
                }}
              >
                <img
                  src={rec.inventoryItem.imageUrl}
                  alt={rec.inventoryItem.name}
                  style={{ width: "100%", height: "100%", objectFit: "cover" }}
                />
              </div>

              {/* Details */}
              <div style={{ flex: 1, minWidth: 0 }}>
                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    gap: "0.5rem",
                    marginBottom: "0.375rem",
                  }}
                >
                  <span
                    style={{
                      fontFamily: "var(--font-display)",
                      fontSize: "1rem",
                      fontWeight: 600,
                      color: "var(--color-ink)",
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                      whiteSpace: "nowrap",
                    }}
                  >
                    {rec.inventoryItem.name}
                  </span>
                  <ScoreBadge score={rec.score} />
                </div>

                <p
                  style={{
                    fontSize: "0.8125rem",
                    color: "var(--color-ink2)",
                    margin: "0 0 0.5rem",
                    lineHeight: 1.45,
                  }}
                >
                  {rec.reasoning}
                </p>

                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: "0.75rem",
                    fontSize: "0.8125rem",
                  }}
                >
                  <span style={{ fontWeight: 600, color: "var(--color-ink)" }}>
                    ${rec.inventoryItem.price.toFixed(2)}{" "}
                    <span style={{ fontWeight: 400, color: "var(--color-muted)" }}>
                      {rec.inventoryItem.currency}
                    </span>
                  </span>
                  <span
                    style={{
                      color: "var(--color-muted)",
                      textTransform: "capitalize",
                    }}
                  >
                    {rec.inventoryItem.traits.category}
                    {rec.inventoryItem.traits.subcategory
                      ? ` · ${rec.inventoryItem.traits.subcategory}`
                      : ""}
                  </span>
                </div>
              </div>
            </a>
          ))}
        </div>
      )}
    </div>
  );
}
