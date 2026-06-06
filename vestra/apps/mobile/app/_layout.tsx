import { useState, useEffect } from "react";
import { Stack, useRouter, useSegments } from "expo-router";
import { useFonts } from "expo-font";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { tokens } from "@vestra/tokens";
import { AuthContextProvider, FakeAuthProvider, useAuth, type IAuthProvider } from "@vestra/auth";
import { ApiClientProvider, FakeApiClient, HttpApiClient, type IApiClient } from "@vestra/api";
import { NativeMsalAuthProvider } from "../lib/auth/NativeMsalAuthProvider";
import { readNativeEntraConfig } from "../lib/auth/nativeAuthConfig";

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, staleTime: 30_000 } },
});

type ReadyState = { auth: IAuthProvider; api: IApiClient };

async function buildProviders(): Promise<ReadyState> {
  const config = readNativeEntraConfig();
  let auth: IAuthProvider;
  if (config) {
    const nativeAuth = new NativeMsalAuthProvider(config);
    await nativeAuth.initialize();
    auth = nativeAuth;
  } else {
    auth = new FakeAuthProvider();
  }

  const apiBase = process.env.EXPO_PUBLIC_API_BASE_URL;
  const api: IApiClient = apiBase
    ? new HttpApiClient(apiBase, () => auth.getAccessToken())
    : new FakeApiClient();

  return { auth, api };
}

function AuthGuard() {
  const auth = useAuth();
  const router = useRouter();
  const segments = useSegments();

  useEffect(() => {
    const onLoginScreen = segments[0] === "login";
    if (!auth.currentUser && !onLoginScreen) {
      router.replace("/login");
    } else if (auth.currentUser && onLoginScreen) {
      router.replace("/");
    }
  }, [auth.currentUser, segments, router]);

  return null;
}

export default function RootLayout() {
  const [fontsLoaded] = useFonts({});
  const [ready, setReady] = useState<ReadyState | null>(null);

  useEffect(() => {
    buildProviders().then(setReady);
  }, []);

  if (!fontsLoaded || !ready) return null;

  return (
    <QueryClientProvider client={queryClient}>
      <ApiClientProvider client={ready.api}>
        <AuthContextProvider auth={ready.auth}>
          <AuthGuard />
          <Stack
            screenOptions={{
              headerShown: false,
              contentStyle: { backgroundColor: tokens.color.bg },
            }}
          />
        </AuthContextProvider>
      </ApiClientProvider>
    </QueryClientProvider>
  );
}
