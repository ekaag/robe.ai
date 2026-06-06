import { View, Text, StyleSheet, TouchableOpacity } from "react-native";
import { useRouter } from "expo-router";
import { useQuery } from "@tanstack/react-query";
import { tokens } from "@vestra/tokens";
import { useAuth } from "@vestra/auth";
import { useApiClient } from "@vestra/api";

export default function HomeScreen() {
  const auth = useAuth();
  const router = useRouter();
  const api = useApiClient();
  const { data: me } = useQuery({
    queryKey: ["me"],
    queryFn: () => api.getMe(),
  });

  const handleSignOut = async () => {
    await auth.signOut();
    router.replace("/login");
  };

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Vestra</Text>
      <Text style={styles.subtitle}>
        {me ? `Signed in as ${me.userId} via ${me.provider}.` : "Verifying session…"}
      </Text>
      <TouchableOpacity onPress={handleSignOut} style={styles.signOutButton}>
        <Text style={styles.signOutText}>Sign out</Text>
      </TouchableOpacity>
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
  signOutButton: {
    marginTop: tokens.space.md,
    paddingVertical: tokens.space.sm,
    paddingHorizontal: tokens.space.md,
    borderWidth: 1,
    borderColor: tokens.color.ink2,
    borderRadius: tokens.radius.sm,
  },
  signOutText: {
    color: tokens.color.ink2,
    fontSize: 14,
  },
});
