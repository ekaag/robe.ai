"use client";

import { useState, useRef } from "react";
import { useAnalyzeGarment, useAddGarment } from "@vestra/api";
import type { GarmentTraits } from "@vestra/types";
import { TraitRow } from "./TraitRow";
import { FormalityDots } from "./FormalityDots";
import { ConfidenceBar } from "./ConfidenceBar";

type Step = "pick" | "analyzing" | "review" | "saving" | "error";

interface UploadFlowProps {
  open: boolean;
  onClose: () => void;
}

export function UploadFlow({ open, onClose }: UploadFlowProps) {
  const analyze = useAnalyzeGarment();
  const add = useAddGarment();

  const [step, setStep] = useState<Step>("pick");
  const [imageBase64, setImageBase64] = useState("");
  const [mimeType, setMimeType] = useState("");
  const [previewSrc, setPreviewSrc] = useState("");
  const [traits, setTraits] = useState<GarmentTraits | null>(null);
  const [errorMsg, setErrorMsg] = useState("");
  const fileInputRef = useRef<HTMLInputElement>(null);

  const reset = () => {
    setStep("pick");
    setTraits(null);
    setErrorMsg("");
    setImageBase64("");
    setMimeType("");
    setPreviewSrc("");
  };

  const handleClose = () => {
    reset();
    onClose();
  };

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    e.target.value = "";
    const reader = new FileReader();
    reader.onload = async (evt) => {
      const result = evt.target?.result as string;
      const b64 = result.split(",")[1];
      setImageBase64(b64);
      setMimeType(file.type);
      setPreviewSrc(result);
      setStep("analyzing");
      try {
        const extracted = await analyze.mutateAsync({ imageBase64: b64, mimeType: file.type });
        setTraits(extracted);
        setStep("review");
      } catch {
        setErrorMsg("Could not analyze garment. Try a clearer photo.");
        setStep("error");
      }
    };
    reader.readAsDataURL(file);
  };

  const handleSave = async () => {
    if (!traits) return;
    setStep("saving");
    try {
      await add.mutateAsync({ traits, imageBase64, mimeType });
      reset();
      onClose();
    } catch {
      setErrorMsg("Failed to save garment. Please try again.");
      setStep("error");
    }
  };

  if (!open) return null;

  return (
    <div
      style={{
        position: "fixed",
        inset: 0,
        backgroundColor: "rgba(34, 28, 21, 0.5)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        zIndex: 50,
        padding: "1rem",
      }}
    >
      <div
        style={{
          background: "var(--color-surface)",
          borderRadius: "1.125rem",
          padding: "1.5rem",
          width: "100%",
          maxWidth: "480px",
          maxHeight: "90vh",
          overflowY: "auto",
        }}
      >
        {/* header */}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: "1.25rem",
          }}
        >
          <h2 style={{ margin: 0, fontSize: "1.125rem", color: "var(--color-ink)" }}>
            Add garment
          </h2>
          <button
            onClick={handleClose}
            aria-label="Close"
            style={{
              background: "none",
              border: "none",
              cursor: "pointer",
              fontSize: "1.25rem",
              color: "var(--color-ink2)",
              padding: "0.25rem",
              lineHeight: 1,
            }}
          >
            ✕
          </button>
        </div>

        {step === "pick" && (
          <div style={{ textAlign: "center" }}>
            <input
              ref={fileInputRef}
              type="file"
              accept="image/*"
              onChange={handleFileChange}
              style={{ display: "none" }}
              aria-label="Upload garment photo"
            />
            <button
              onClick={() => fileInputRef.current?.click()}
              aria-label="Choose photo"
              style={{
                display: "flex",
                flexDirection: "column",
                alignItems: "center",
                justifyContent: "center",
                gap: "0.75rem",
                width: "100%",
                aspectRatio: "4 / 3",
                background: "var(--color-bg2)",
                border: "2px dashed var(--color-line)",
                borderRadius: "0.875rem",
                cursor: "pointer",
                color: "var(--color-muted)",
              }}
            >
              <span style={{ fontSize: "2.5rem", lineHeight: 1 }}>+</span>
              <span style={{ fontSize: "0.875rem", fontFamily: "var(--font-body)" }}>
                Choose a photo
              </span>
            </button>
          </div>
        )}

        {(step === "analyzing" || step === "saving") && (
          <div
            style={{
              textAlign: "center",
              padding: "3rem 0",
              color: "var(--color-ink2)",
              fontSize: "0.9375rem",
            }}
          >
            {step === "analyzing" ? "Analyzing garment…" : "Saving…"}
          </div>
        )}

        {step === "review" && traits && (
          <div>
            {previewSrc && (
              <img
                src={previewSrc}
                alt="Garment preview"
                style={{
                  width: "100%",
                  maxHeight: "240px",
                  objectFit: "contain",
                  borderRadius: "0.625rem",
                  marginBottom: "1rem",
                  background: "var(--color-bg2)",
                }}
              />
            )}
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
                alignItems: "center",
                justifyContent: "space-between",
                padding: "0.625rem 0",
                borderBottom: "1px solid var(--color-line)",
              }}
            >
              <span style={{ color: "var(--color-ink2)", fontSize: "0.875rem" }}>Formality</span>
              <FormalityDots value={traits.formality} />
            </div>
            <div style={{ padding: "0.75rem 0 1.25rem" }}>
              <ConfidenceBar value={traits.confidence} />
            </div>
            <div style={{ display: "flex", gap: "0.75rem" }}>
              <button
                onClick={handleClose}
                style={{
                  flex: 1,
                  padding: "0.625rem",
                  borderRadius: "0.5rem",
                  border: "1px solid var(--color-line)",
                  background: "none",
                  color: "var(--color-ink2)",
                  cursor: "pointer",
                  fontFamily: "var(--font-body)",
                  fontSize: "0.9375rem",
                }}
              >
                Cancel
              </button>
              <button
                onClick={handleSave}
                aria-label="Confirm and save"
                style={{
                  flex: 2,
                  padding: "0.625rem",
                  borderRadius: "0.5rem",
                  border: "none",
                  background: "var(--color-ink)",
                  color: "var(--color-bg)",
                  cursor: "pointer",
                  fontFamily: "var(--font-body)",
                  fontSize: "0.9375rem",
                  fontWeight: 500,
                }}
              >
                Confirm &amp; save
              </button>
            </div>
          </div>
        )}

        {step === "error" && (
          <div style={{ textAlign: "center", padding: "1rem 0" }}>
            <p style={{ color: "var(--color-ink2)", marginBottom: "1.25rem" }}>{errorMsg}</p>
            <div style={{ display: "flex", gap: "0.75rem", justifyContent: "center" }}>
              <button
                onClick={reset}
                style={{
                  padding: "0.625rem 1.25rem",
                  borderRadius: "0.5rem",
                  border: "1px solid var(--color-line)",
                  background: "none",
                  color: "var(--color-ink2)",
                  cursor: "pointer",
                }}
              >
                Try again
              </button>
              <button
                onClick={handleClose}
                style={{
                  padding: "0.625rem 1.25rem",
                  borderRadius: "0.5rem",
                  border: "none",
                  background: "var(--color-ink)",
                  color: "var(--color-bg)",
                  cursor: "pointer",
                }}
              >
                Cancel
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
