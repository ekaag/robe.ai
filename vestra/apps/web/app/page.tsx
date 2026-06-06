"use client";

import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@vestra/auth";
import { useApiClient } from "@vestra/api";

export default function HomePage() {
  const auth = useAuth();
  const router = useRouter();
  const api = useApiClient();
  const { data: me, isLoading } = useQuery({
    queryKey: ["me"],
    queryFn: () => api.getMe(),
  });

  const handleSignOut = async () => {
    await auth.signOut();
    router.push("/login");
  };

  return (
    <main style={{ padding: "2rem" }}>
      <h1 style={{ fontFamily: "var(--font-display)", color: "var(--color-ink)" }}>
        Vestra
      </h1>
      <p style={{ color: "var(--color-ink2)" }}>
        {isLoading
          ? "Verifying session…"
          : me
          ? `Signed in as ${me.userId} via ${me.provider}.`
          : "You're signed in. Wardrobe coming in step 2."}
      </p>
      <button
        onClick={handleSignOut}
        style={{
          marginTop: "1rem",
          padding: "0.5rem 1rem",
          background: "none",
          border: "1px solid var(--color-ink2)",
          borderRadius: "0.375rem",
          color: "var(--color-ink2)",
          cursor: "pointer",
          fontSize: "0.875rem",
        }}
      >
        Sign out
      </button>
    </main>
  );
}
