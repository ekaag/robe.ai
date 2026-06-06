"use client";

import { useState, useEffect } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AuthContextProvider, FakeAuthProvider, type IAuthProvider } from "@vestra/auth";
import { ApiClientProvider, FakeApiClient, HttpApiClient, type IApiClient } from "@vestra/api";
import { WebMsalAuthProvider } from "../lib/auth/WebMsalAuthProvider";
import { readEntraConfig } from "../lib/auth/msalConfig";

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, staleTime: 30_000 } },
});

type ReadyState = { auth: IAuthProvider; api: IApiClient };

async function buildProviders(): Promise<ReadyState> {
  const config = readEntraConfig();
  let auth: IAuthProvider;
  if (config) {
    const msalAuth = new WebMsalAuthProvider(config);
    await msalAuth.initialize();
    auth = msalAuth;
  } else {
    auth = new FakeAuthProvider();
  }

  const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL;
  const api: IApiClient = apiBase
    ? new HttpApiClient(apiBase, () => auth.getAccessToken())
    : new FakeApiClient();

  return { auth, api };
}

export function Providers({ children }: { children: React.ReactNode }) {
  const [ready, setReady] = useState<ReadyState | null>(null);

  useEffect(() => {
    buildProviders().then(setReady);
  }, []);

  if (!ready) return null;

  return (
    <QueryClientProvider client={queryClient}>
      <ApiClientProvider client={ready.api}>
        <AuthContextProvider auth={ready.auth}>{children}</AuthContextProvider>
      </ApiClientProvider>
    </QueryClientProvider>
  );
}
