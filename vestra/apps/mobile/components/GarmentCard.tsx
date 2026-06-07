import { Pressable, View, Text, Image, StyleSheet } from "react-native";
import { tokens } from "@vestra/tokens";
import type { Garment } from "@vestra/types";

interface GarmentCardProps {
  garment: Garment;
  onPress: () => void;
}

export function GarmentCard({ garment, onPress }: GarmentCardProps) {
  const { primaryColor, category } = garment.traits;
  return (
    <Pressable onPress={onPress} style={({ pressed }) => [styles.card, pressed && styles.pressed]}>
      <Image
        source={{ uri: garment.imageUrl }}
        style={styles.image}
        resizeMode="cover"
      />
      <View style={styles.footer}>
        <View style={[styles.colorDot, { backgroundColor: primaryColor.hex }]} />
        <Text style={styles.category}>{category}</Text>
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: tokens.color.surface,
    borderRadius: tokens.radius.md,
    overflow: "hidden",
    borderWidth: 1,
    borderColor: tokens.color.line,
  },
  pressed: {
    opacity: 0.8,
  },
  image: {
    width: "100%",
    aspectRatio: 3 / 4,
  },
  footer: {
    flexDirection: "row",
    alignItems: "center",
    gap: tokens.space.xs,
    padding: tokens.space.sm,
  },
  colorDot: {
    width: 10,
    height: 10,
    borderRadius: 5,
    borderWidth: 1,
    borderColor: tokens.color.line,
  },
  category: {
    fontSize: 13,
    color: tokens.color.ink2,
    textTransform: "capitalize",
  },
});
