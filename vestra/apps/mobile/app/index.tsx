import { View, Text, StyleSheet } from "react-native";
import { useQuery } from "@tanstack/react-query";
import { tokens } from "@vestra/tokens";
import { useApiClient } from "@vestra/api";

export default function HomeScreen() {
  const api = useApiClient();
  const { data: me } = useQuery({
    queryKey: ["me"],
    queryFn: () => api.getMe(),
  });

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Vestra</Text>
      <Text style={styles.subtitle}>
        {me ? `Signed in as ${me.userId} via ${me.provider}.` : "Verifying session…"}
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
