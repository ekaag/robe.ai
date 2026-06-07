"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@vestra/auth";

const NAV_ITEMS = [
  { href: "/wardrobe", label: "Wardrobe" },
  { href: "/profile", label: "Style" },
  { href: "/shop", label: "Shop" },
];

export function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const auth = useAuth();
  const router = useRouter();

  const handleSignOut = async () => {
    await auth.signOut();
    router.push("/login");
  };

  return (
    <div
      style={{
        display: "flex",
        minHeight: "100vh",
        backgroundColor: "var(--color-bg)",
      }}
    >
      {/* Sidebar */}
      <nav
        style={{
          width: "220px",
          flexShrink: 0,
          borderRight: "1px solid var(--color-line)",
          display: "flex",
          flexDirection: "column",
          padding: "1.5rem 0",
          position: "sticky",
          top: 0,
          height: "100vh",
        }}
      >
        <div style={{ padding: "0 1.5rem", marginBottom: "2rem" }}>
          <span
            style={{
              fontFamily: "var(--font-display)",
              fontSize: "1.375rem",
              color: "var(--color-ink)",
              fontWeight: 600,
              letterSpacing: "-0.01em",
            }}
          >
            Vestra
          </span>
        </div>

        <div style={{ flex: 1, display: "flex", flexDirection: "column", gap: "0.125rem" }}>
          {NAV_ITEMS.map(({ href, label }) => {
            const active = pathname.startsWith(href);
            return (
              <Link
                key={href}
                href={href}
                style={{
                  display: "block",
                  padding: "0.625rem 1.5rem",
                  color: active ? "var(--color-ink)" : "var(--color-ink2)",
                  fontWeight: active ? 600 : 400,
                  fontSize: "0.9375rem",
                  fontFamily: "var(--font-body)",
                  textDecoration: "none",
                  borderRadius: "0 0.5rem 0.5rem 0",
                  backgroundColor: active ? "var(--color-surface)" : "transparent",
                  borderLeft: active ? "2px solid var(--color-accent)" : "2px solid transparent",
                }}
              >
                {label}
              </Link>
            );
          })}
        </div>

        {/* Account footer */}
        <div
          style={{
            padding: "1rem 1.5rem",
            borderTop: "1px solid var(--color-line)",
          }}
        >
          {auth.currentUser && (
            <div style={{ marginBottom: "0.625rem" }}>
              <div
                style={{
                  fontSize: "0.875rem",
                  color: "var(--color-ink)",
                  fontWeight: 500,
                  overflow: "hidden",
                  textOverflow: "ellipsis",
                  whiteSpace: "nowrap",
                }}
              >
                {auth.currentUser.name ?? auth.currentUser.id}
              </div>
              <div style={{ fontSize: "0.75rem", color: "var(--color-muted)", textTransform: "capitalize" }}>
                {auth.currentUser.provider}
              </div>
            </div>
          )}
          <button
            onClick={handleSignOut}
            style={{
              background: "none",
              border: "none",
              padding: 0,
              color: "var(--color-ink2)",
              fontSize: "0.8125rem",
              cursor: "pointer",
              fontFamily: "var(--font-body)",
            }}
          >
            Sign out
          </button>
        </div>
      </nav>

      {/* Main content */}
      <main style={{ flex: 1, minWidth: 0 }}>{children}</main>
    </div>
  );
}
