import { View, Text, StyleSheet } from "react-native";
import { tokens } from "@vestra/tokens";

export default function HomeScreen() {
  return (
    <View style={styles.container}>
      <Text style={styles.title}>Vestra</Text>
      <Text style={styles.subtitle}>
        Scaffold complete — sign-in and screens coming in step 1.
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: tokens.color.bg,
    alignItems: "center",
    justifyContent: "center",
    padding: tokens.space.lg,
  },
  title: {
    fontSize: 32,
    color: tokens.color.ink,
    fontWeight: "600",
    marginBottom: tokens.space.sm,
  },
  subtitle: {
    fontSize: 16,
    color: tokens.color.ink2,
    textAlign: "center",
  },
});
