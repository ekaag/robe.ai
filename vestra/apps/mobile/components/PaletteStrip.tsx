import { View, Text, StyleSheet } from "react-native";
import { tokens } from "@vestra/tokens";
import type { ColorWeight } from "@vestra/types";

interface PaletteStripProps {
  palette: ColorWeight[];
}

export function PaletteStrip({ palette }: PaletteStripProps) {
  return (
    <View style={styles.row} accessibilityLabel="Color palette">
      {palette.map((color) => (
        <View key={color.hex} style={styles.swatch}>
          <View
            accessibilityLabel={color.name}
            style={[styles.circle, { backgroundColor: color.hex }]}
          />
          <Text style={styles.label}>{color.name}</Text>
        </View>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  row: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: tokens.space.sm,
  },
  swatch: {
    alignItems: "center",
    gap: tokens.space.xs,
  },
  circle: {
    width: 40,
    height: 40,
    borderRadius: tokens.radius.sm,
    borderWidth: 1,
    borderColor: tokens.color.line,
  },
  label: {
    fontSize: 11,
    color: tokens.color.ink2,
    textTransform: "capitalize",
  },
});
