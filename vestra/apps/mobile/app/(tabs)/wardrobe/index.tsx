import { useState } from "react";
import { View, Text, FlatList, StyleSheet } from "react-native";
import { useRouter } from "expo-router";
import { useGarments } from "@vestra/api";
import { tokens } from "@vestra/tokens";
import { FilterChips, type FilterOption } from "../../../components/FilterChips";
import { GarmentCard } from "../../../components/GarmentCard";
import { AddTile } from "../../../components/AddTile";
import { UploadFlow } from "../../../components/UploadFlow";

const FILTER_OPTIONS: FilterOption[] = [
  "all",
  "top",
  "bottom",
  "dress",
  "outerwear",
  "footwear",
  "accessory",
];

export default function WardrobeScreen() {
  const router = useRouter();
  const [filter, setFilter] = useState<FilterOption>("all");
  const [uploadOpen, setUploadOpen] = useState(false);

  const { data: garments = [], isLoading } = useGarments(
    filter !== "all" ? { category: filter } : undefined
  );

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.title}>Wardrobe</Text>
        {!isLoading && (
          <Text style={styles.count}>
            {garments.length} {garments.length === 1 ? "piece" : "pieces"}
          </Text>
        )}
      </View>

      <FilterChips options={FILTER_OPTIONS} selected={filter} onChange={setFilter} />

      {isLoading ? (
        <View style={styles.loading}>
          <Text style={styles.loadingText}>Loading…</Text>
        </View>
      ) : (
        <FlatList
          data={garments}
          keyExtractor={(g) => g.id}
          numColumns={2}
          contentContainerStyle={styles.grid}
          columnWrapperStyle={styles.row}
          renderItem={({ item }) => (
            <View style={styles.cell}>
              <GarmentCard
                garment={item}
                onPress={() => router.push(`/wardrobe/${item.id}`)}
              />
            </View>
          )}
          ListFooterComponent={
            <View style={styles.cell}>
              <AddTile onPress={() => setUploadOpen(true)} />
            </View>
          }
        />
      )}

      <UploadFlow open={uploadOpen} onClose={() => setUploadOpen(false)} />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: tokens.color.bg,
    paddingTop: tokens.space.xl,
  },
  header: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "baseline",
    paddingHorizontal: tokens.space.lg,
    marginBottom: tokens.space.md,
  },
  title: {
    fontSize: 28,
    fontWeight: "600",
    color: tokens.color.ink,
  },
  count: {
    fontSize: 14,
    color: tokens.color.muted,
  },
  loading: {
    flex: 1,
    alignItems: "center",
    justifyContent: "center",
  },
  loadingText: {
    color: tokens.color.muted,
    fontSize: 15,
  },
  grid: {
    padding: tokens.space.md,
    paddingTop: tokens.space.lg,
  },
  row: {
    gap: tokens.space.md,
  },
  cell: {
    flex: 1,
    marginBottom: tokens.space.md,
  },
});
