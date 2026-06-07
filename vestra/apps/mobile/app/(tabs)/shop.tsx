import { View, Text, StyleSheet } from "react-native";
import { tokens } from "@vestra/tokens";

export default function ShopScreen() {
  return (
    <View style={styles.container}>
      <Text style={styles.title}>Shop</Text>
      <Text style={styles.subtitle}>Coming soon — recommendations arrive in step 4.</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: tokens.color.bg,
    alignItems: "center",
    justifyContent: "center",
    padding: tokens.space.xl,
  },
  title: {
    fontSize: 24,
    fontWeight: "600",
    color: tokens.color.ink,
    marginBottom: tokens.space.sm,
  },
  subtitle: {
    fontSize: 15,
    color: tokens.color.muted,
    textAlign: "center",
  },
});
