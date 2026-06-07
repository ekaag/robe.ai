import { View, Text, StyleSheet } from "react-native";
import { tokens } from "@vestra/tokens";

interface ConfidenceBarProps {
  value: number; // 0–1
}

export function ConfidenceBar({ value }: ConfidenceBarProps) {
  const pct = Math.round(Math.min(Math.max(value, 0), 1) * 100);
  return (
    <View>
      <View style={styles.header}>
        <Text style={styles.label}>Confidence</Text>
        <Text style={styles.label}>{pct}%</Text>
      </View>
      <View style={styles.track}>
        <View style={[styles.fill, { width: `${pct}%` as `${number}%` }]} />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  header: {
    flexDirection: "row",
    justifyContent: "space-between",
    marginBottom: 4,
  },
  label: {
    fontSize: 12,
    color: tokens.color.ink2,
  },
  track: {
    height: 6,
    borderRadius: 100,
    backgroundColor: tokens.color.line,
    overflow: "hidden",
  },
  fill: {
    height: "100%",
    borderRadius: 100,
    backgroundColor: tokens.color.accent,
  },
});
