import { View, Text, StyleSheet } from "react-native";
import { tokens } from "@vestra/tokens";

interface TraitRowProps {
  label: string;
  value: string;
}

export function TraitRow({ label, value }: TraitRowProps) {
  return (
    <View style={styles.row}>
      <Text style={styles.label}>{label}</Text>
      <Text style={styles.value}>{value}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  row: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    paddingVertical: tokens.space.sm,
    borderBottomWidth: 1,
    borderBottomColor: tokens.color.line,
  },
  label: {
    fontSize: 14,
    color: tokens.color.ink2,
  },
  value: {
    fontSize: 14,
    color: tokens.color.ink,
    fontWeight: "500",
    textTransform: "capitalize",
    flexShrink: 1,
    textAlign: "right",
    marginLeft: tokens.space.sm,
  },
});
