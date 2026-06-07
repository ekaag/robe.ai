"use client";

import { useParams, useRouter } from "next/navigation";
import { useGarment } from "@vestra/api";
import { TraitRow } from "../../../components/TraitRow";
import { FormalityDots } from "../../../components/FormalityDots";
import { ConfidenceBar } from "../../../components/ConfidenceBar";

export default function GarmentDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const { data: garment, isLoading, isError } = useGarment(id);

  if (isLoading) {
    return (
      <div style={{ padding: "2rem", color: "var(--color-muted)", fontSize: "0.9375rem" }}>
        Loading…
      </div>
    );
  }

  if (isError || !garment) {
    return (
      <div style={{ padding: "2rem" }}>
        <p style={{ color: "var(--color-ink2)" }}>Garment not found.</p>
        <button
          onClick={() => router.back()}
          style={{
            marginTop: "1rem",
            background: "none",
            border: "none",
            color: "var(--color-accent)",
            cursor: "pointer",
            padding: 0,
            fontFamily: "var(--font-body)",
          }}
        >
          ← Back
        </button>
      </div>
    );
  }

  const { traits } = garment;

  return (
    <div style={{ padding: "2rem", maxWidth: "600px" }}>
      <button
        onClick={() => router.back()}
        style={{
          background: "none",
          border: "none",
          color: "var(--color-ink2)",
          cursor: "pointer",
          padding: 0,
          fontFamily: "var(--font-body)",
          fontSize: "0.875rem",
          marginBottom: "1.5rem",
          display: "flex",
          alignItems: "center",
          gap: "0.25rem",
        }}
      >
        ← Wardrobe
      </button>

      {/* Hero image */}
      <div
        style={{
          borderRadius: "1rem",
          overflow: "hidden",
          marginBottom: "1.5rem",
          background: "var(--color-surface)",
          aspectRatio: "3 / 4",
          maxWidth: "320px",
        }}
      >
        <img
          src={garment.imageUrl}
          alt={`${traits.category} garment`}
          style={{ width: "100%", height: "100%", objectFit: "cover" }}
        />
      </div>

      <h1
        style={{
          fontFamily: "var(--font-display)",
          fontSize: "1.5rem",
          color: "var(--color-ink)",
          margin: "0 0 1.25rem",
          textTransform: "capitalize",
        }}
      >
        {traits.subcategory ?? traits.category}
      </h1>

      {/* Traits */}
      <section style={{ marginBottom: "1.5rem" }}>
        <TraitRow
          label="Category"
          value={traits.subcategory ? `${traits.category} · ${traits.subcategory}` : traits.category}
        />
        <TraitRow label="Color" value={traits.primaryColor.name} />
        <TraitRow label="Pattern" value={traits.pattern} />
        {traits.material && <TraitRow label="Material" value={traits.material} />}
        {traits.fit && <TraitRow label="Fit" value={traits.fit} />}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            padding: "0.625rem 0",
            borderBottom: "1px solid var(--color-line)",
          }}
        >
          <span style={{ color: "var(--color-ink2)", fontSize: "0.875rem" }}>Formality</span>
          <FormalityDots value={traits.formality} />
        </div>
        <TraitRow label="Season" value={traits.seasonality.join(", ")} />
        {traits.styleTags.length > 0 && (
          <TraitRow label="Style" value={traits.styleTags.join(", ")} />
        )}
      </section>

      <ConfidenceBar value={traits.confidence} />
    </div>
  );
}
