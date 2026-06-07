"use client";

import { useRouter } from "next/navigation";
import { useAuth } from "@vestra/auth";
import type { AuthProvider } from "@vestra/auth";
import { ProviderButton } from "../../components/ProviderButton";

const PROVIDERS: { provider: AuthProvider; label: string }[] = [
  { provider: "apple", label: "Continue with Apple" },
  { provider: "google", label: "Continue with Google" },
  { provider: "microsoft", label: "Continue with Microsoft" },
  { provider: "facebook", label: "Continue with Facebook" },
];

export default function LoginPage() {
  const auth = useAuth();
  const router = useRouter();

  const handleSignIn = async (provider: AuthProvider) => {
    await auth.signIn(provider);
    router.push("/wardrobe");
  };

  return (
    <main
      style={{
        minHeight: "100vh",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        backgroundColor: "var(--color-bg)",
        padding: "2rem",
      }}
    >
      <h1
        style={{
          fontFamily: "var(--font-display)",
          fontSize: "3rem",
          color: "var(--color-ink)",
          marginBottom: "0.5rem",
          fontWeight: 600,
        }}
      >
        Vestra
      </h1>
      <p
        style={{
          color: "var(--color-ink2)",
          marginBottom: "2.5rem",
          fontSize: "1rem",
        }}
      >
        Sign in to your wardrobe
      </p>
      <div
        style={{
          display: "flex",
          flexDirection: "column",
          gap: "0.75rem",
          width: "100%",
          maxWidth: "360px",
        }}
      >
        {PROVIDERS.map(({ provider, label }) => (
          <ProviderButton
            key={provider}
            provider={provider}
            label={label}
            onClick={() => handleSignIn(provider)}
          />
        ))}
      </div>
    </main>
  );
}
