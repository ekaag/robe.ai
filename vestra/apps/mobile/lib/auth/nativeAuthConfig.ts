export interface NativeEntraConfig {
  authority: string;
  clientId: string;
  apiScope: string;
}

export function readNativeEntraConfig(): NativeEntraConfig | null {
  const clientId = process.env.EXPO_PUBLIC_ENTRA_CLIENT_ID;
  if (!clientId) return null;
  return {
    authority: process.env.EXPO_PUBLIC_ENTRA_AUTHORITY ?? "",
    clientId,
    apiScope: process.env.EXPO_PUBLIC_ENTRA_API_SCOPE ?? "",
  };
}
