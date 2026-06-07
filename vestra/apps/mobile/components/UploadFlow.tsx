import { useState } from "react";
import { Modal, View, Text, Image, Pressable, ActivityIndicator, StyleSheet } from "react-native";
import * as ImagePicker from "expo-image-picker";
import { useAnalyzeGarment, useAddGarment } from "@vestra/api";
import { tokens } from "@vestra/tokens";
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
  const [mimeType, setMimeType] = useState("image/jpeg");
  const [previewUri, setPreviewUri] = useState("");
  const [traits, setTraits] = useState<GarmentTraits | null>(null);
  const [errorMsg, setErrorMsg] = useState("");

  const reset = () => {
    setStep("pick");
    setTraits(null);
    setErrorMsg("");
    setImageBase64("");
    setPreviewUri("");
  };

  const handleClose = () => {
    reset();
    onClose();
  };

  const handlePickImage = async () => {
    const result = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ImagePicker.MediaTypeOptions.Images,
      base64: true,
      quality: 0.8,
    });
    if (result.canceled || !result.assets[0]) return;
    const asset = result.assets[0];
    const b64 = asset.base64 ?? "";
    const mime = asset.mimeType ?? "image/jpeg";
    setImageBase64(b64);
    setMimeType(mime);
    setPreviewUri(asset.uri);
    setStep("analyzing");
    try {
      const extracted = await analyze.mutateAsync({ imageBase64: b64, mimeType: mime });
      setTraits(extracted);
      setStep("review");
    } catch {
      setErrorMsg("Could not analyze garment. Try a clearer photo.");
      setStep("error");
    }
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

  return (
    <Modal visible={open} animationType="slide" presentationStyle="pageSheet" onRequestClose={handleClose}>
      <View style={styles.container}>
        {/* Header */}
        <View style={styles.header}>
          <Text style={styles.title}>Add garment</Text>
          <Pressable onPress={handleClose} accessibilityLabel="Close" hitSlop={12}>
            <Text style={styles.closeBtn}>✕</Text>
          </Pressable>
        </View>

        {step === "pick" && (
          <View style={styles.centered}>
            <Pressable onPress={handlePickImage} style={styles.pickButton} accessibilityLabel="Choose photo">
              <Text style={styles.pickPlus}>+</Text>
              <Text style={styles.pickLabel}>Choose from Photos</Text>
            </Pressable>
          </View>
        )}

        {(step === "analyzing" || step === "saving") && (
          <View style={styles.centered}>
            <ActivityIndicator size="large" color={tokens.color.accent} />
            <Text style={styles.statusText}>
              {step === "analyzing" ? "Analyzing garment…" : "Saving…"}
            </Text>
          </View>
        )}

        {step === "review" && traits && (
          <View style={styles.reviewContainer}>
            {previewUri ? (
              <Image source={{ uri: previewUri }} style={styles.preview} resizeMode="contain" />
            ) : null}
            <TraitRow
              label="Category"
              value={traits.subcategory ? `${traits.category} · ${traits.subcategory}` : traits.category}
            />
            <TraitRow label="Color" value={traits.primaryColor.name} />
            <TraitRow label="Pattern" value={traits.pattern} />
            {traits.material ? <TraitRow label="Material" value={traits.material} /> : null}
            {traits.fit ? <TraitRow label="Fit" value={traits.fit} /> : null}
            <View style={styles.formalityRow}>
              <Text style={styles.formalityLabel}>Formality</Text>
              <FormalityDots value={traits.formality} />
            </View>
            <View style={{ marginTop: tokens.space.sm, marginBottom: tokens.space.lg }}>
              <ConfidenceBar value={traits.confidence} />
            </View>
            <View style={styles.actions}>
              <Pressable onPress={handleClose} style={styles.cancelBtn}>
                <Text style={styles.cancelText}>Cancel</Text>
              </Pressable>
              <Pressable
                onPress={handleSave}
                style={styles.confirmBtn}
                accessibilityLabel="Confirm and save"
              >
                <Text style={styles.confirmText}>Confirm & save</Text>
              </Pressable>
            </View>
          </View>
        )}

        {step === "error" && (
          <View style={styles.centered}>
            <Text style={styles.errorText}>{errorMsg}</Text>
            <View style={styles.actions}>
              <Pressable onPress={reset} style={styles.cancelBtn}>
                <Text style={styles.cancelText}>Try again</Text>
              </Pressable>
              <Pressable onPress={handleClose} style={styles.confirmBtn}>
                <Text style={styles.confirmText}>Cancel</Text>
              </Pressable>
            </View>
          </View>
        )}
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: tokens.color.surface,
    padding: tokens.space.lg,
  },
  header: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: tokens.space.lg,
  },
  title: {
    fontSize: 18,
    fontWeight: "600",
    color: tokens.color.ink,
  },
  closeBtn: {
    fontSize: 18,
    color: tokens.color.ink2,
  },
  centered: {
    flex: 1,
    alignItems: "center",
    justifyContent: "center",
    gap: tokens.space.md,
  },
  pickButton: {
    width: "100%",
    aspectRatio: 4 / 3,
    borderWidth: 2,
    borderStyle: "dashed",
    borderColor: tokens.color.line,
    borderRadius: tokens.radius.md,
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: tokens.color.bg2,
    gap: tokens.space.sm,
  },
  pickPlus: {
    fontSize: 40,
    lineHeight: 48,
    color: tokens.color.muted,
  },
  pickLabel: {
    fontSize: 14,
    color: tokens.color.muted,
  },
  statusText: {
    fontSize: 15,
    color: tokens.color.ink2,
  },
  reviewContainer: {
    flex: 1,
  },
  preview: {
    width: "100%",
    height: 200,
    borderRadius: tokens.radius.md,
    marginBottom: tokens.space.md,
    backgroundColor: tokens.color.bg2,
  },
  formalityRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    paddingVertical: tokens.space.sm,
    borderBottomWidth: 1,
    borderBottomColor: tokens.color.line,
  },
  formalityLabel: {
    fontSize: 14,
    color: tokens.color.ink2,
  },
  actions: {
    flexDirection: "row",
    gap: tokens.space.sm,
  },
  cancelBtn: {
    flex: 1,
    paddingVertical: tokens.space.sm,
    borderRadius: tokens.radius.sm,
    borderWidth: 1,
    borderColor: tokens.color.line,
    alignItems: "center",
  },
  cancelText: {
    fontSize: 15,
    color: tokens.color.ink2,
  },
  confirmBtn: {
    flex: 2,
    paddingVertical: tokens.space.sm,
    borderRadius: tokens.radius.sm,
    backgroundColor: tokens.color.ink,
    alignItems: "center",
  },
  confirmText: {
    fontSize: 15,
    fontWeight: "500",
    color: tokens.color.bg,
  },
  errorText: {
    fontSize: 15,
    color: tokens.color.ink2,
    textAlign: "center",
    marginBottom: tokens.space.md,
  },
});
