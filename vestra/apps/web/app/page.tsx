"use client";

import { useQuery } from "@tanstack/react-query";
import { useApiClient } from "@vestra/api";

export default function HomePage() {
  const api = useApiClient();
  const { data: me, isLoading } = useQuery({
    queryKey: ["me"],
    queryFn: () => api.getMe(),
  });

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
    </main>
  );
}
