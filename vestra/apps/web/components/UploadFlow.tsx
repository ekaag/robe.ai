"use client";

import { useState, useRef } from "react";
import { useAnalyzeBatch, useAddGarment } from "@vestra/api";
import type {
  GarmentTraits,
  BatchImageInput,
  BatchAnalyzeResult,
  ClothingItemTraitsResult,
  GarmentCategory,
  GarmentPattern,
  GarmentFit,
  StyleTag,
  Season,
  Occasion,
} from "@vestra/types";
import { ImageWithOverlay, type FaceMarker, type GarmentMarker } from "./ImageWithOverlay";

// All uploads — single image or several — go through /analyze-batch so there's
// one extraction code path; a single photo just yields a one-image batch result.
type Step = "pick" | "analyzing" | "batch-review" | "saving" | "error";

interface BatchImageData {
  imageId: string;
  imageBase64: string;
  mimeType: string;
  previewSrc: string;
}

interface UploadFlowProps {
  open: boolean;
  onClose: () => void;
}

const VALID_CATEGORIES: GarmentCategory[] = [
  "top", "bottom", "dress", "outerwear", "footwear", "accessory", "other",
];
const VALID_PATTERNS: GarmentPattern[] = [
  "solid", "striped", "plaid", "checked", "floral", "graphic", "other",
];

function mapToGarmentTraits(item: ClothingItemTraitsResult): GarmentTraits {
  const cat = item.category?.toLowerCase() as GarmentCategory;
  const pat = (item.pattern?.toLowerCase() ?? "solid") as GarmentPattern;
  return {
    category: VALID_CATEGORIES.includes(cat) ? cat : "other",
    subcategory: item.subtype ?? item.type,
    primaryColor: { name: item.primaryColor?.normalized ?? "unknown", hex: "#888888" },
    secondaryColors: [],
    pattern: VALID_PATTERNS.includes(pat) ? pat : "other",
    material: item.material ?? null,
    fit: (item.fit as GarmentFit) ?? null,
    formality: 2,
    seasonality: ["spring", "summer", "fall", "winter"] as Season[],
    styleTags: (item.styleTags ?? []) as StyleTag[],
    occasions: ["everyday"] as Occasion[],
    confidence: item.confidence,
  };
}

function readFileAsDataURL(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = (e) => resolve(e.target?.result as string);
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
}

export function UploadFlow({ open, onClose }: UploadFlowProps) {
  const analyzeBatch = useAnalyzeBatch();
  const add = useAddGarment();

  const [step, setStep] = useState<Step>("pick");
  const [selectedFileCount, setSelectedFileCount] = useState(0);

  const [batchImages, setBatchImages] = useState<BatchImageData[]>([]);
  const [batchResult, setBatchResult] = useState<BatchAnalyzeResult | null>(null);
  const [selectedKeys, setSelectedKeys] = useState<Set<string>>(new Set());
  const [saveProgress, setSaveProgress] = useState({ done: 0, total: 0 });

  const [errorMsg, setErrorMsg] = useState("");
  const fileInputRef = useRef<HTMLInputElement>(null);

  const reset = () => {
    setStep("pick");
    setSelectedFileCount(0);
    setErrorMsg("");
    setBatchImages([]);
    setBatchResult(null);
    setSelectedKeys(new Set());
    setSaveProgress({ done: 0, total: 0 });
  };

  const handleClose = () => {
    reset();
    onClose();
  };

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files ?? []);
    e.target.value = "";
    if (files.length === 0) return;

    setSelectedFileCount(files.length);
    setStep("analyzing");
    try {
      const readFiles = await Promise.all(
        files.map(async (file, i) => {
          const dataUrl = await readFileAsDataURL(file);
          return {
            imageId: `img-${i + 1}`,
            imageBase64: dataUrl.split(",")[1],
            mimeType: file.type,
            previewSrc: dataUrl,
          } satisfies BatchImageData;
        })
      );
      setBatchImages(readFiles);

      const batchInputs: BatchImageInput[] = readFiles.map((img) => ({
        imageId: img.imageId,
        imageBase64: img.imageBase64,
        mimeType: img.mimeType,
      }));

      const result = await analyzeBatch.mutateAsync(batchInputs);
      setBatchResult(result);

      const allKeys = new Set<string>();
      for (const img of result.images) {
        for (const person of img.people) {
          for (let i = 0; i < person.clothingItems.length; i++) {
            allKeys.add(`${img.imageId}:${person.personId}:${i}`);
          }
        }
      }
      setSelectedKeys(allKeys);
      setStep("batch-review");
    } catch {
      setErrorMsg(
        files.length === 1
          ? "Could not analyze garment. Try a clearer photo."
          : "Could not analyze images. Make sure each file is a clear photo of clothing."
      );
      setStep("error");
    }
  };

  const handleBatchSave = async () => {
    if (!batchResult || selectedKeys.size === 0) return;
    setStep("saving");

    const items = Array.from(selectedKeys).map((key) => {
      const [imageId, personId, itemIdxStr] = key.split(":");
      const imageData = batchImages.find((img) => img.imageId === imageId)!;
      const imageResult = batchResult.images.find((img) => img.imageId === imageId)!;
      const person = imageResult.people.find((p) => p.personId === personId)!;
      const item = person.clothingItems[parseInt(itemIdxStr)];
      return { item, imageData };
    });

    setSaveProgress({ done: 0, total: items.length });
    try {
      for (let i = 0; i < items.length; i++) {
        const { item, imageData } = items[i];
        await add.mutateAsync({
          traits: mapToGarmentTraits(item),
          imageBase64: imageData.imageBase64,
          mimeType: imageData.mimeType,
        });
        setSaveProgress({ done: i + 1, total: items.length });
      }
      reset();
      onClose();
    } catch {
      setErrorMsg("Failed to save one or more garments. Please try again.");
      setStep("error");
    }
  };

  const toggleKey = (key: string) => {
    setSelectedKeys((prev) => {
      const next = new Set(prev);
      next.has(key) ? next.delete(key) : next.add(key);
      return next;
    });
  };

  if (!open) return null;

  const isBatch = selectedFileCount > 1;

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
          maxWidth: step === "batch-review" ? "560px" : "480px",
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
            {isBatch ? "Add garments" : "Add garment"}
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

        {/* pick */}
        {step === "pick" && (
          <div style={{ textAlign: "center" }}>
            <input
              ref={fileInputRef}
              type="file"
              accept="image/jpeg,image/png,image/webp"
              multiple
              onChange={handleFileChange}
              style={{ display: "none" }}
              aria-label="Upload garment photos"
            />
            <button
              onClick={() => fileInputRef.current?.click()}
              aria-label="Choose photos"
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
              <span style={{ fontSize: "0.75rem", fontFamily: "var(--font-body)" }}>
                or select multiple to analyze outfits
              </span>
            </button>
          </div>
        )}

        {/* analyzing / saving */}
        {(step === "analyzing" || step === "saving") && (
          <div
            style={{
              textAlign: "center",
              padding: "3rem 0",
              color: "var(--color-ink2)",
              fontSize: "0.9375rem",
            }}
          >
            {step === "analyzing"
              ? isBatch
                ? `Analyzing ${selectedFileCount} images…`
                : "Analyzing garment…"
              : saveProgress.total > 1
              ? `Saving ${saveProgress.done + 1} of ${saveProgress.total} items…`
              : "Saving…"}
          </div>
        )}

        {/* batch review */}
        {step === "batch-review" && batchResult && (
          <BatchReview
            images={batchImages}
            result={batchResult}
            selectedKeys={selectedKeys}
            onToggle={toggleKey}
            onCancel={handleClose}
            onSave={handleBatchSave}
          />
        )}

        {/* error */}
        {step === "error" && (
          <div style={{ textAlign: "center", padding: "1rem 0" }}>
            <p style={{ color: "var(--color-ink2)", marginBottom: "1.25rem" }}>{errorMsg}</p>
            <div style={{ display: "flex", gap: "0.75rem", justifyContent: "center" }}>
              <button onClick={reset} style={ghostBtn}>
                Try again
              </button>
              <button onClick={handleClose} style={primaryBtn}>
                Cancel
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

// ── Batch review ──────────────────────────────────────────────────────────────

interface BatchReviewProps {
  images: BatchImageData[];
  result: BatchAnalyzeResult;
  selectedKeys: Set<string>;
  onToggle: (key: string) => void;
  onCancel: () => void;
  onSave: () => void;
}

function BatchReview({ images, result, selectedKeys, onToggle, onCancel, onSave }: BatchReviewProps) {
  const previewMap = Object.fromEntries(images.map((img) => [img.imageId, img.previewSrc]));
  const totalItems = result.images.reduce(
    (sum, img) => sum + img.people.reduce((s, p) => s + p.clothingItems.length, 0),
    0
  );

  return (
    <div>
      <p style={{ fontSize: "0.8125rem", color: "var(--color-muted)", marginBottom: "1rem", margin: "0 0 1rem" }}>
        Select items to add to your wardrobe.
      </p>

      {result.images.map((imgResult) => {
        const preview = previewMap[imgResult.imageId];
        const personCount = imgResult.people.length;
        const itemCount = imgResult.people.reduce((s, p) => s + p.clothingItems.length, 0);

        const faces: FaceMarker[] = imgResult.people
          .filter((p) => p.faceBoundingBox)
          .map((p) => ({ box: p.faceBoundingBox! }));

        const garments: GarmentMarker[] = imgResult.people.flatMap((person) =>
          person.clothingItems
            .map((item, idx) => ({ item, idx }))
            .filter(({ item }) => item.boundingBox)
            .map(({ item, idx }) => ({
              box: item.boundingBox!,
              label: item.type,
              highlighted: selectedKeys.has(`${imgResult.imageId}:${person.personId}:${idx}`),
            }))
        );

        return (
          <div
            key={imgResult.imageId}
            style={{
              marginBottom: "1.25rem",
              borderBottom: "1px solid var(--color-line)",
              paddingBottom: "1.25rem",
            }}
          >
            {/* image header */}
            <div style={{ marginBottom: "0.75rem" }}>
              {preview && (
                <ImageWithOverlay
                  src={preview}
                  alt={imgResult.imageId}
                  faces={faces}
                  garments={garments}
                  style={{ height: 220, marginBottom: "0.5rem" }}
                />
              )}
              <div style={{ fontSize: "0.875rem", fontWeight: 500, color: "var(--color-ink)" }}>
                {imgResult.imageId}
              </div>
              <div style={{ fontSize: "0.75rem", color: "var(--color-muted)" }}>
                {personCount} {personCount === 1 ? "person" : "people"} · {itemCount} items
              </div>
              {imgResult.warnings.map((w, i) => (
                <div key={i} style={{ fontSize: "0.75rem", color: "var(--color-accent)", marginTop: "0.25rem" }}>
                  ⚠ {w}
                </div>
              ))}
            </div>

            {/* clothing items per person */}
            {imgResult.people.map((person) => (
              <div key={person.personId}>
                {personCount > 1 && (
                  <div style={{ fontSize: "0.75rem", color: "var(--color-muted)", margin: "0.25rem 0 0.375rem 0.25rem" }}>
                    {person.personId}{person.position ? ` · ${person.position}` : ""}
                  </div>
                )}
                {person.clothingItems.map((item, idx) => {
                  const key = `${imgResult.imageId}:${person.personId}:${idx}`;
                  const checked = selectedKeys.has(key);
                  return (
                    <label
                      key={key}
                      style={{
                        display: "flex",
                        alignItems: "center",
                        gap: "0.625rem",
                        padding: "0.5rem 0.625rem",
                        borderRadius: "0.5rem",
                        cursor: "pointer",
                        background: checked ? "var(--color-bg2)" : "transparent",
                        marginBottom: "0.25rem",
                      }}
                    >
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={() => onToggle(key)}
                        style={{ accentColor: "var(--color-accent)", width: 15, height: 15, flexShrink: 0 }}
                      />
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <span style={{ fontSize: "0.875rem", color: "var(--color-ink)", fontWeight: 500 }}>
                          {item.type}
                          {item.subtype ? ` · ${item.subtype}` : ""}
                        </span>
                        {item.primaryColor && (
                          <span style={{ fontSize: "0.8125rem", color: "var(--color-ink2)" }}>
                            {" "}· {item.primaryColor.normalized}
                          </span>
                        )}
                        {item.pattern && item.pattern !== "solid" && (
                          <span style={{ fontSize: "0.8125rem", color: "var(--color-ink2)" }}>
                            {" "}· {item.pattern}
                          </span>
                        )}
                        {item.brand && (
                          <span style={{ fontSize: "0.75rem", color: "var(--color-muted)", marginLeft: "0.375rem" }}>
                            {item.brand}
                          </span>
                        )}
                      </div>
                      <span style={{ fontSize: "0.75rem", color: "var(--color-muted)", flexShrink: 0 }}>
                        {Math.round(item.confidence * 100)}%
                      </span>
                    </label>
                  );
                })}
                {person.clothingItems.length === 0 && (
                  <p style={{ fontSize: "0.875rem", color: "var(--color-muted)", paddingLeft: "0.25rem", margin: 0 }}>
                    No clothing detected.
                  </p>
                )}
              </div>
            ))}

            {imgResult.people.length === 0 && (
              <p style={{ fontSize: "0.875rem", color: "var(--color-muted)", paddingLeft: "0.25rem", margin: 0 }}>
                No people detected in this image.
              </p>
            )}
          </div>
        );
      })}

      {/* footer */}
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: "0.75rem", paddingTop: "0.25rem" }}>
        <span style={{ fontSize: "0.8125rem", color: "var(--color-muted)" }}>
          {selectedKeys.size} of {totalItems} selected
        </span>
        <div style={{ display: "flex", gap: "0.75rem" }}>
          <button onClick={onCancel} style={ghostBtn}>
            Cancel
          </button>
          <button
            onClick={onSave}
            disabled={selectedKeys.size === 0}
            style={{
              ...primaryBtn,
              opacity: selectedKeys.size === 0 ? 0.45 : 1,
              cursor: selectedKeys.size === 0 ? "default" : "pointer",
            }}
          >
            Save {selectedKeys.size} {selectedKeys.size === 1 ? "item" : "items"}
          </button>
        </div>
      </div>
    </div>
  );
}

// ── Shared button styles ──────────────────────────────────────────────────────

const ghostBtn: React.CSSProperties = {
  padding: "0.625rem 1rem",
  borderRadius: "0.5rem",
  border: "1px solid var(--color-line)",
  background: "none",
  color: "var(--color-ink2)",
  cursor: "pointer",
  fontFamily: "var(--font-body)",
  fontSize: "0.9375rem",
};

const primaryBtn: React.CSSProperties = {
  padding: "0.625rem 1rem",
  borderRadius: "0.5rem",
  border: "none",
  background: "var(--color-ink)",
  color: "var(--color-bg)",
  cursor: "pointer",
  fontFamily: "var(--font-body)",
  fontSize: "0.9375rem",
  fontWeight: 500,
};
