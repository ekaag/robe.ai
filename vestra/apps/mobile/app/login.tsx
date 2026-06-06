import { View, Text, StyleSheet } from "react-native";
import { useRouter } from "expo-router";
import { useAuth } from "@vestra/auth";
import type { AuthProvider } from "@vestra/auth";
import { tokens } from "@vestra/tokens";
import { ProviderButton } from "../components/ProviderButton";

const PROVIDERS: { provider: AuthProvider; label: string }[] = [
  { provider: "apple", label: "Continue with Apple" },
  { provider: "google", label: "Continue with Google" },
  { provider: "microsoft", label: "Continue with Microsoft" },
  { provider: "facebook", label: "Continue with Facebook" },
];

export default function LoginScreen() {
  const auth = useAuth();
  const router = useRouter();

  const handleSignIn = async (provider: AuthProvider) => {
    await auth.signIn(provider);
    router.replace("/");
  };

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Vestra</Text>
      <Text style={styles.subtitle}>Sign in to your wardrobe</Text>
      <View style={styles.buttons}>
        {PROVIDERS.map(({ provider, label }) => (
          <ProviderButton
            key={provider}
            provider={provider}
            label={label}
            onPress={() => handleSignIn(provider)}
          />
        ))}
      </View>
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
    fontSize: 40,
    color: tokens.color.ink,
    fontWeight: "600",
    marginBottom: tokens.space.sm,
  },
  subtitle: {
    fontSize: 16,
    color: tokens.color.ink2,
    marginBottom: tokens.space.xl,
  },
  buttons: {
    width: "100%",
    gap: tokens.space.sm,
  },
});
