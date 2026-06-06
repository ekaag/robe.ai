"use client";

import { useEffect } from "react";
import { useRouter, usePathname } from "next/navigation";
import { useAuth } from "@vestra/auth";

export function AuthGuard({ children }: { children: React.ReactNode }) {
  const auth = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (!auth.currentUser && pathname !== "/login") {
      router.replace("/login");
    }
  }, [auth.currentUser, pathname, router]);

  // Suppress protected content on the client until auth is confirmed.
  if (!auth.currentUser && pathname !== "/login") return null;

  return <>{children}</>;
}
