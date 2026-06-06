import { useEffect } from "react";
import { Stack, useRouter, useSegments } from "expo-router";
import { useFonts } from "expo-font";
import { tokens } from "@vestra/tokens";
import { AuthContextProvider, FakeAuthProvider, useAuth } from "@vestra/auth";

// Replaced with NativeMsalAuthProvider in step 1b.
const fakeAuth = new FakeAuthProvider();

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
  const [loaded] = useFonts({});

  if (!loaded) return null;

  return (
    <AuthContextProvider auth={fakeAuth}>
      <AuthGuard />
      <Stack
        screenOptions={{
          headerShown: false,
          contentStyle: { backgroundColor: tokens.color.bg },
        }}
      />
    </AuthContextProvider>
  );
}
