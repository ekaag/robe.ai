import { ScrollView, View, Text, Pressable, StyleSheet } from "react-native";
import { useStyleProfile, useGenerateProfile } from "@vestra/api";
import { tokens } from "@vestra/tokens";
import { FormalityDots } from "../../../components/FormalityDots";
import { PaletteStrip } from "../../../components/PaletteStrip";

export default function StyleScreen() {
  const { data: profile, isLoading } = useStyleProfile();
  const generate = useGenerateProfile();

  if (isLoading) {
    return (
      <View style={styles.centered}>
        <Text style={styles.statusText}>Loading…</Text>
      </View>
    );
  }

  if (profile === null || profile === undefined) {
    return (
      <View style={styles.centered}>
        <Text style={styles.title}>Your Style Profile</Text>
        <Text style={styles.emptyMsg}>
          Generate your profile to see a personalized style analysis based on your wardrobe.
        </Text>
        <Pressable
          onPress={() => generate.mutate()}
          disabled={generate.isPending}
          accessibilityLabel="Generate profile"
          style={[styles.ctaBtn, generate.isPending && styles.btnDisabled]}
        >
          <Text style={styles.ctaBtnText}>
            {generate.isPending ? "Generating…" : "Generate Profile"}
          </Text>
        </Pressable>
      </View>
    );
  }

  if (profile.garmentCount === 0) {
    return (
      <View style={styles.centered}>
        <Text style={styles.title}>Your Style Profile</Text>
        <Text style={styles.emptyMsg}>
          Add garments to your wardrobe to generate a meaningful profile.
        </Text>
      </View>
    );
  }

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      <View style={styles.header}>
        <Text style={styles.title}>Your Style Profile</Text>
        <Pressable
          onPress={() => generate.mutate()}
          disabled={generate.isPending}
          accessibilityLabel="Regenerate profile"
        >
          <Text style={[styles.regenText, generate.isPending && styles.btnDisabled]}>
            {generate.isPending ? "Regenerating…" : "Regenerate"}
          </Text>
        </Pressable>
      </View>

      {/* Dominant styles */}
      <View style={styles.section}>
        <Text style={styles.sectionLabel}>Dominant Styles</Text>
        <View style={styles.chips}>
          {profile.dominantStyles.map((style) => (
            <View key={style} style={styles.chip}>
              <Text style={styles.chipText}>{style}</Text>
            </View>
          ))}
        </View>
      </View>

      {/* Color palette */}
      <View style={styles.section}>
        <Text style={styles.sectionLabel}>Color Palette</Text>
        <PaletteStrip palette={profile.colorPalette} />
      </View>

      {/* Formality */}
      <View style={styles.section}>
        <Text style={styles.sectionLabel}>Formality</Text>
        <View style={styles.formalityRow}>
          <FormalityDots value={profile.formalityRange.typical} />
          <Text style={styles.formalityRange}>
            {profile.formalityRange.min}–{profile.formalityRange.max} range
          </Text>
        </View>
      </View>

      {/* Editorial summary quote */}
      <View style={styles.quote}>
        <View style={styles.quoteBorder} />
        <Text style={styles.quoteText}>
          &ldquo;{profile.summary}&rdquo;
        </Text>
      </View>

      {/* Metadata */}
      <Text style={styles.meta}>
        {profile.garmentCount} {profile.garmentCount === 1 ? "piece" : "pieces"} · Generated{" "}
        {new Date(profile.modifiedAt).toLocaleDateString()}
      </Text>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: tokens.color.bg,
  },
  content: {
    padding: tokens.space.lg,
    paddingTop: tokens.space.xl,
  },
  centered: {
    flex: 1,
    backgroundColor: tokens.color.bg,
    alignItems: "center",
    justifyContent: "center",
    padding: tokens.space.xl,
  },
  header: {
    flexDirection: "row",
    alignItems: "baseline",
    justifyContent: "space-between",
    marginBottom: tokens.space.xl,
  },
  title: {
    fontSize: 24,
    fontWeight: "600",
    color: tokens.color.ink,
  },
  emptyMsg: {
    fontSize: 15,
    color: tokens.color.muted,
    textAlign: "center",
    marginTop: tokens.space.sm,
    marginBottom: tokens.space.lg,
  },
  statusText: {
    fontSize: 15,
    color: tokens.color.muted,
  },
  ctaBtn: {
    backgroundColor: tokens.color.accent,
    borderRadius: tokens.radius.pill,
    paddingHorizontal: tokens.space.lg,
    paddingVertical: tokens.space.sm,
  },
  btnDisabled: {
    opacity: 0.6,
  },
  ctaBtnText: {
    color: "#fff",
    fontSize: 15,
    fontWeight: "600",
  },
  regenText: {
    fontSize: 13,
    color: tokens.color.ink2,
  },
  section: {
    marginBottom: tokens.space.lg,
  },
  sectionLabel: {
    fontSize: 11,
    color: tokens.color.muted,
    textTransform: "uppercase",
    letterSpacing: 1,
    marginBottom: tokens.space.xs,
  },
  chips: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: tokens.space.xs,
  },
  chip: {
    borderWidth: 1.5,
    borderColor: tokens.color.accent,
    borderRadius: tokens.radius.pill,
    paddingHorizontal: tokens.space.sm,
    paddingVertical: tokens.space.xs,
  },
  chipText: {
    color: tokens.color.accent,
    fontSize: 14,
    fontWeight: "500",
    textTransform: "capitalize",
  },
  formalityRow: {
    flexDirection: "row",
    alignItems: "center",
    gap: tokens.space.sm,
  },
  formalityRange: {
    fontSize: 13,
    color: tokens.color.muted,
  },
  quote: {
    flexDirection: "row",
    marginBottom: tokens.space.lg,
  },
  quoteBorder: {
    width: 3,
    backgroundColor: tokens.color.accent,
    borderRadius: 2,
    marginRight: tokens.space.sm,
  },
  quoteText: {
    flex: 1,
    fontSize: 17,
    fontStyle: "italic",
    color: tokens.color.ink,
    lineHeight: 26,
  },
  meta: {
    fontSize: 13,
    color: tokens.color.muted,
  },
});
