"use client";

import { useState } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AuthContextProvider, FakeAuthProvider } from "@vestra/auth";

// Replaced with WebMsalAuthProvider in step 1b.
const fakeAuth = new FakeAuthProvider();

export function Providers({ children }: { children: React.ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: { queries: { retry: 1, staleTime: 30_000 } },
      })
  );

  return (
    <QueryClientProvider client={queryClient}>
      <AuthContextProvider auth={fakeAuth}>{children}</AuthContextProvider>
    </QueryClientProvider>
  );
}
