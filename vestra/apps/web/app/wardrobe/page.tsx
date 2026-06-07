"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useGarments } from "@vestra/api";
import type { FilterOption } from "../../components/FilterChips";
import { FilterChips } from "../../components/FilterChips";
import { GarmentCard } from "../../components/GarmentCard";
import { AddTile } from "../../components/AddTile";
import { UploadFlow } from "../../components/UploadFlow";

const FILTER_OPTIONS: FilterOption[] = [
  "all",
  "top",
  "bottom",
  "dress",
  "outerwear",
  "footwear",
  "accessory",
];

export default function WardrobePage() {
  const router = useRouter();
  const [filter, setFilter] = useState<FilterOption>("all");
  const [uploadOpen, setUploadOpen] = useState(false);

  const { data: garments = [], isLoading } = useGarments(
    filter !== "all" ? { category: filter } : undefined
  );

  return (
    <div style={{ padding: "2rem" }}>
      <div
        style={{
          display: "flex",
          alignItems: "baseline",
          justifyContent: "space-between",
          marginBottom: "1.5rem",
        }}
      >
        <h1
          style={{
            fontFamily: "var(--font-display)",
            fontSize: "1.875rem",
            color: "var(--color-ink)",
            margin: 0,
          }}
        >
          Wardrobe
        </h1>
        {!isLoading && (
          <span style={{ fontSize: "0.875rem", color: "var(--color-muted)" }}>
            {garments.length} {garments.length === 1 ? "piece" : "pieces"}
          </span>
        )}
      </div>

      <div style={{ marginBottom: "1.5rem" }}>
        <FilterChips options={FILTER_OPTIONS} selected={filter} onChange={setFilter} />
      </div>

      {isLoading ? (
        <p style={{ color: "var(--color-muted)", fontSize: "0.9375rem" }}>Loading…</p>
      ) : (
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fill, minmax(160px, 1fr))",
            gap: "1rem",
          }}
        >
          {garments.map((g) => (
            <GarmentCard
              key={g.id}
              garment={g}
              onClick={() => router.push(`/wardrobe/${g.id}`)}
            />
          ))}
          <AddTile onClick={() => setUploadOpen(true)} />
        </div>
      )}

      <UploadFlow open={uploadOpen} onClose={() => setUploadOpen(false)} />
    </div>
  );
}
