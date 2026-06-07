import { Tabs } from "expo-router";
import { tokens } from "@vestra/tokens";

export default function TabsLayout() {
  return (
    <Tabs
      screenOptions={{
        headerShown: false,
        tabBarStyle: {
          backgroundColor: tokens.color.surface,
          borderTopColor: tokens.color.line,
          borderTopWidth: 1,
        },
        tabBarActiveTintColor: tokens.color.accent,
        tabBarInactiveTintColor: tokens.color.muted,
        tabBarLabelStyle: { fontSize: 11 },
      }}
    >
      <Tabs.Screen
        name="wardrobe"
        options={{ title: "Closet" }}
      />
      <Tabs.Screen
        name="style"
        options={{ title: "Style" }}
      />
      <Tabs.Screen
        name="shop"
        options={{ title: "Shop" }}
      />
    </Tabs>
  );
}
