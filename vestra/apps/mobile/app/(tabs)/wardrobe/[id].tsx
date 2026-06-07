import { ScrollView, View, Text, Image, Pressable, StyleSheet } from "react-native";
import { useLocalSearchParams, useRouter } from "expo-router";
import { useGarment } from "@vestra/api";
import { tokens } from "@vestra/tokens";
import { TraitRow } from "../../../components/TraitRow";
import { FormalityDots } from "../../../components/FormalityDots";
import { ConfidenceBar } from "../../../components/ConfidenceBar";

export default function GarmentDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { data: garment, isLoading, isError } = useGarment(id ?? "");

  if (isLoading) {
    return (
      <View style={styles.centered}>
        <Text style={styles.statusText}>Loading…</Text>
      </View>
    );
  }

  if (isError || !garment) {
    return (
      <View style={styles.centered}>
        <Text style={styles.statusText}>Garment not found.</Text>
        <Pressable onPress={() => router.back()} style={{ marginTop: tokens.space.md }}>
          <Text style={{ color: tokens.color.accent }}>← Back</Text>
        </Pressable>
      </View>
    );
  }

  const { traits } = garment;

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      <Pressable onPress={() => router.back()} style={styles.backBtn}>
        <Text style={styles.backText}>← Wardrobe</Text>
      </Pressable>

      <Image
        source={{ uri: garment.imageUrl }}
        style={styles.hero}
        resizeMode="cover"
      />

      <Text style={styles.name}>
        {traits.subcategory ?? traits.category}
      </Text>

      <View style={styles.traits}>
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
        <TraitRow label="Season" value={traits.seasonality.join(", ")} />
        {traits.styleTags.length > 0 && (
          <TraitRow label="Style" value={traits.styleTags.join(", ")} />
        )}
      </View>

      <View style={{ marginTop: tokens.space.lg }}>
        <ConfidenceBar value={traits.confidence} />
      </View>
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
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: tokens.color.bg,
  },
  statusText: {
    fontSize: 15,
    color: tokens.color.muted,
  },
  backBtn: {
    marginBottom: tokens.space.md,
  },
  backText: {
    fontSize: 14,
    color: tokens.color.ink2,
  },
  hero: {
    width: "100%",
    aspectRatio: 3 / 4,
    borderRadius: tokens.radius.lg,
    backgroundColor: tokens.color.surface,
    marginBottom: tokens.space.lg,
  },
  name: {
    fontSize: 24,
    fontWeight: "600",
    color: tokens.color.ink,
    marginBottom: tokens.space.md,
    textTransform: "capitalize",
  },
  traits: {
    gap: 0,
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
});
