import { Stack } from "expo-router";
import { useFonts } from "expo-font";
import { tokens } from "@vestra/tokens";

// Fonts are loaded here; swap for @expo-google-fonts/* once verified available in step 1.
export default function RootLayout() {
  const [loaded] = useFonts({});

  if (!loaded) return null;

  return (
    <Stack
      screenOptions={{
        headerShown: false,
        contentStyle: { backgroundColor: tokens.color.bg },
      }}
    />
  );
}
