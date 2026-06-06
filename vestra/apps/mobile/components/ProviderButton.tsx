import { Pressable, Text, StyleSheet } from "react-native";
import { tokens } from "@vestra/tokens";
import type { AuthProvider } from "@vestra/auth";

interface Props {
  provider: AuthProvider;
  label: string;
  onPress: () => void;
}

export function ProviderButton({ label, onPress }: Props) {
  return (
    <Pressable
      style={({ pressed }) => [styles.button, pressed && styles.pressed]}
      onPress={onPress}
    >
      <Text style={styles.label}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  button: {
    backgroundColor: tokens.color.surface,
    borderWidth: 1,
    borderColor: tokens.color.line,
    borderRadius: tokens.radius.md,
    paddingVertical: tokens.space.md,
    paddingHorizontal: tokens.space.lg,
    alignItems: "center",
  },
  pressed: {
    backgroundColor: tokens.color.surface2,
  },
  label: {
    color: tokens.color.ink,
    fontSize: 16,
    fontWeight: "500",
  },
});
