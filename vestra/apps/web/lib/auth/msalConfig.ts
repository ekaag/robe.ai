import type { Configuration } from "@azure/msal-browser";

export interface EntraConfig {
  authority: string;
  clientId: string;
  redirectUri: string;
  apiScope: string;
}

export function readEntraConfig(): EntraConfig | null {
  const clientId = process.env.NEXT_PUBLIC_ENTRA_CLIENT_ID;
  if (!clientId) return null;
  return {
    authority: process.env.NEXT_PUBLIC_ENTRA_AUTHORITY ?? "",
    clientId,
    redirectUri:
      process.env.NEXT_PUBLIC_ENTRA_REDIRECT_URI ??
      (typeof window !== "undefined" ? window.location.origin : ""),
    apiScope: process.env.NEXT_PUBLIC_ENTRA_API_SCOPE ?? "",
  };
}

export function buildMsalConfig(config: EntraConfig): Configuration {
  return {
    auth: {
      clientId: config.clientId,
      authority: config.authority,
      redirectUri: config.redirectUri,
      knownAuthorities: [new URL(config.authority).hostname],
    },
    cache: {
      cacheLocation: "sessionStorage",
      storeAuthStateInCookie: false,
    },
  };
}
