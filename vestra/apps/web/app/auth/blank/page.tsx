"use client";

// This page exists solely as the popup redirectUri for MSAL popup flows.
// MSAL v3 detects the popup context, broadcasts the auth result to the opener
// via BroadcastChannel, then closes this window. Nothing is rendered.
export default function AuthBlankPage() {
  return null;
}
