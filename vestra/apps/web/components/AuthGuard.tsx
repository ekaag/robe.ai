"use client";

import { useEffect } from "react";
import { useRouter, usePathname } from "next/navigation";
import { useAuth } from "@vestra/auth";

export function AuthGuard({ children }: { children: React.ReactNode }) {
  const auth = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  const isPublic = pathname === "/login" || pathname === "/auth/blank";

  useEffect(() => {
    if (!auth.currentUser && !isPublic) {
      router.replace("/login");
    }
  }, [auth.currentUser, isPublic, router]);

  // Suppress protected content on the client until auth is confirmed.
  if (!auth.currentUser && !isPublic) return null;

  return <>{children}</>;
}
