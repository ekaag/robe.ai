import { View, StyleSheet } from "react-native";
import { tokens } from "@vestra/tokens";

interface FormalityDotsProps {
  value: number; // 1–5
  max?: number;
}

export function FormalityDots({ value, max = 5 }: FormalityDotsProps) {
  return (
    <View style={styles.row}>
      {Array.from({ length: max }, (_, i) => (
        <View
          key={i}
          style={[styles.dot, { backgroundColor: i < value ? tokens.color.accent : tokens.color.line }]}
        />
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  row: {
    flexDirection: "row",
    gap: 5,
    alignItems: "center",
  },
  dot: {
    width: 10,
    height: 10,
    borderRadius: 5,
  },
});
