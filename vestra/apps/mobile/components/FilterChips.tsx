import { ScrollView, Pressable, Text, StyleSheet } from "react-native";
import { tokens } from "@vestra/tokens";
import type { GarmentCategory } from "@vestra/types";

export type FilterOption = GarmentCategory | "all";

interface FilterChipsProps {
  options: FilterOption[];
  selected: FilterOption;
  onChange: (value: FilterOption) => void;
}

export function FilterChips({ options, selected, onChange }: FilterChipsProps) {
  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.container}>
      {options.map((opt) => {
        const active = selected === opt;
        return (
          <Pressable
            key={opt}
            onPress={() => onChange(opt)}
            style={[styles.chip, active && styles.chipActive]}
          >
            <Text style={[styles.label, active && styles.labelActive]}>
              {opt.charAt(0).toUpperCase() + opt.slice(1)}
            </Text>
          </Pressable>
        );
      })}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flexDirection: "row",
    gap: tokens.space.xs,
    paddingHorizontal: tokens.space.lg,
  },
  chip: {
    paddingVertical: 6,
    paddingHorizontal: 14,
    borderRadius: tokens.radius.pill,
    borderWidth: 1,
    borderColor: tokens.color.line,
    backgroundColor: "transparent",
  },
  chipActive: {
    borderColor: tokens.color.ink,
    backgroundColor: tokens.color.ink,
  },
  label: {
    fontSize: 13,
    color: tokens.color.ink2,
  },
  labelActive: {
    color: tokens.color.bg,
  },
});
