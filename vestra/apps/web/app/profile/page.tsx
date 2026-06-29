"use client";

import { useStyleProfile, useGenerateProfile } from "@vestra/api";
import { FormalityDots } from "../../components/FormalityDots";
import { PaletteStrip } from "../../components/PaletteStrip";

const SECTION: React.CSSProperties = {
  marginBottom: "2rem",
};

const SECTION_LABEL: React.CSSProperties = {
  fontSize: "0.6875rem",
  fontFamily: "var(--font-body)",
  color: "var(--color-muted)",
  letterSpacing: "0.08em",
  textTransform: "uppercase",
  marginBottom: "0.625rem",
};

export default function ProfilePage() {
  const { data: profile, isLoading } = useStyleProfile();
  const generate = useGenerateProfile();

  if (isLoading) {
    return (
      <div style={{ padding: "2rem" }}>
        <p style={{ color: "var(--color-muted)", fontSize: "0.9375rem" }}>Loading…</p>
      </div>
    );
  }

  if (profile === null || profile === undefined) {
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
          Your Style Profile
        </h1>
        <p style={{ color: "var(--color-muted)", fontSize: "0.9375rem", marginBottom: "1.5rem" }}>
          Generate your profile to see a personalized style analysis based on your wardrobe.
        </p>
        <button
          onClick={() => generate.mutate()}
          disabled={generate.isPending}
          aria-label="Generate profile"
          style={{
            padding: "0.625rem 1.375rem",
            borderRadius: "100px",
            border: "none",
            backgroundColor: "var(--color-accent)",
            color: "#fff",
            fontFamily: "var(--font-body)",
            fontSize: "0.9375rem",
            fontWeight: 600,
            cursor: generate.isPending ? "not-allowed" : "pointer",
            opacity: generate.isPending ? 0.7 : 1,
          }}
        >
          {generate.isPending ? "Generating…" : "Generate Profile"}
        </button>
      </div>
    );
  }

  if (profile.garmentCount === 0) {
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
          Your Style Profile
        </h1>
        <p
          data-testid="empty-wardrobe-msg"
          style={{ color: "var(--color-muted)", fontSize: "0.9375rem" }}
        >
          Add garments to your wardrobe to generate a meaningful profile.
        </p>
      </div>
    );
  }

  return (
    <div style={{ padding: "2rem", maxWidth: "640px" }}>
      <div
        style={{
          display: "flex",
          alignItems: "baseline",
          justifyContent: "space-between",
          marginBottom: "2rem",
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
          Your Style Profile
        </h1>
        <button
          onClick={() => generate.mutate()}
          disabled={generate.isPending}
          aria-label="Regenerate profile"
          style={{
            background: "none",
            border: "1px solid var(--color-line)",
            borderRadius: "100px",
            padding: "0.375rem 0.875rem",
            fontFamily: "var(--font-body)",
            fontSize: "0.8125rem",
            color: "var(--color-ink2)",
            cursor: generate.isPending ? "not-allowed" : "pointer",
            opacity: generate.isPending ? 0.6 : 1,
          }}
        >
          {generate.isPending ? "Regenerating…" : "Regenerate"}
        </button>
      </div>

      {/* Dominant styles */}
      <section style={SECTION}>
        <p style={SECTION_LABEL}>Dominant Styles</p>
        <div style={{ display: "flex", flexWrap: "wrap", gap: "0.5rem" }}>
          {profile.dominantStyles.map((style) => (
            <span
              key={style}
              style={{
                padding: "0.3125rem 0.875rem",
                borderRadius: "100px",
                border: "1.5px solid var(--color-accent)",
                color: "var(--color-accent)",
                fontSize: "0.875rem",
                fontFamily: "var(--font-body)",
                fontWeight: 500,
                textTransform: "capitalize",
              }}
            >
              {style}
            </span>
          ))}
        </div>
      </section>

      {/* Color palette */}
      <section style={SECTION}>
        <p style={SECTION_LABEL}>Color Palette</p>
        <PaletteStrip palette={profile.colorPalette} />
      </section>

      {/* Formality */}
      <section style={SECTION}>
        <p style={SECTION_LABEL}>Formality</p>
        <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
          <FormalityDots value={profile.formalityRange.typical} />
          <span style={{ fontSize: "0.8125rem", color: "var(--color-muted)" }}>
            {profile.formalityRange.min}–{profile.formalityRange.max} range
          </span>
        </div>
      </section>

      {/* Editorial summary quote */}
      <blockquote
        aria-label="Style summary"
        style={{
          fontFamily: "var(--font-display)",
          fontStyle: "italic",
          fontSize: "1.125rem",
          lineHeight: 1.55,
          color: "var(--color-ink)",
          borderLeft: "3px solid var(--color-accent)",
          paddingLeft: "1.125rem",
          margin: "0 0 2rem",
        }}
      >
        &ldquo;{profile.summary}&rdquo;
      </blockquote>

      {/* Metadata */}
      <p style={{ fontSize: "0.8125rem", color: "var(--color-muted)", margin: 0 }}>
        {profile.garmentCount} {profile.garmentCount === 1 ? "piece" : "pieces"} &middot;{" "}
        Generated {new Date(profile.modifiedAt).toLocaleDateString()}
      </p>
    </div>
  );
}
