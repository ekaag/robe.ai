import { Pressable, Text, StyleSheet } from "react-native";
import { tokens } from "@vestra/tokens";

interface AddTileProps {
  onPress: () => void;
  disabled?: boolean;
}

export function AddTile({ onPress, disabled }: AddTileProps) {
  return (
    <Pressable
      onPress={onPress}
      disabled={disabled}
      accessibilityLabel="Add garment"
      style={({ pressed }) => [styles.tile, pressed && !disabled && styles.pressed]}
    >
      <Text style={styles.plus}>+</Text>
      <Text style={styles.label}>Add garment</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  tile: {
    alignItems: "center",
    justifyContent: "center",
    gap: tokens.space.xs,
    borderWidth: 2,
    borderStyle: "dashed",
    borderColor: tokens.color.line,
    borderRadius: tokens.radius.md,
    aspectRatio: 3 / 4,
    backgroundColor: "transparent",
  },
  pressed: {
    opacity: 0.6,
  },
  plus: {
    fontSize: 28,
    lineHeight: 32,
    color: tokens.color.muted,
  },
  label: {
    fontSize: 12,
    color: tokens.color.muted,
  },
});
