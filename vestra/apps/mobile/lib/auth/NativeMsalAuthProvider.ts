import * as AuthSession from "expo-auth-session";
import * as WebBrowser from "expo-web-browser";
import * as SecureStore from "expo-secure-store";
import type { IAuthProvider, AuthProvider, CurrentUser } from "@vestra/auth";
import type { NativeEntraConfig } from "./nativeAuthConfig";

const KEYS = {
  accessToken: "vestra_access_token",
  refreshToken: "vestra_refresh_token",
  tokenExpiry: "vestra_token_expiry",
  userInfo: "vestra_user_info",
} as const;

// Maps our provider names to the domain_hint Entra accepts.
// Apple: verify whether your user flow needs domain_hint or extraParams: { idp: "Apple" }.
const DOMAIN_HINTS: Partial<Record<AuthProvider, string>> = {
  google: "google.com",
  facebook: "facebook.com",
  apple: "apple.com",
};

function parseJwtPayload(token: string): Record<string, unknown> {
  try {
    const part = token.split(".")[1];
    const decoded =
      typeof Buffer !== "undefined"
        ? Buffer.from(part, "base64").toString()
        : atob(part.replace(/-/g, "+").replace(/_/g, "/"));
    return JSON.parse(decoded);
  } catch {
    return {};
  }
}

export class NativeMsalAuthProvider implements IAuthProvider {
  currentUser: CurrentUser | null = null;
  private discovery: AuthSession.DiscoveryDocument | null = null;

  constructor(private readonly config: NativeEntraConfig) {
    // Warm up the browser so sign-in opens faster on the first tap.
    WebBrowser.warmUpAsync().catch(() => {});
  }

  async initialize(): Promise<void> {
    const [accessToken, expiry, userInfoJson] = await Promise.all([
      SecureStore.getItemAsync(KEYS.accessToken),
      SecureStore.getItemAsync(KEYS.tokenExpiry),
      SecureStore.getItemAsync(KEYS.userInfo),
    ]);

    if (accessToken && expiry && userInfoJson) {
      if (Date.now() < parseInt(expiry, 10)) {
        this.currentUser = JSON.parse(userInfoJson) as CurrentUser;
        return;
      }
      if (await this.tryRefresh()) return;
    }
    await this.clearStorage();
  }

  async signIn(provider: AuthProvider): Promise<void> {
    const discovery = await this.ensureDiscovery();
    const redirectUri = AuthSession.makeRedirectUri({ scheme: "vestra" });

    const request = new AuthSession.AuthRequest({
      clientId: this.config.clientId,
      scopes: ["openid", "profile", "offline_access", this.config.apiScope],
      redirectUri,
      usePKCE: true,
      extraParams: DOMAIN_HINTS[provider]
        ? { domain_hint: DOMAIN_HINTS[provider]! }
        : {},
    });

    const result = await request.promptAsync(discovery);
    if (result.type !== "success") return;

    const tokenResult = await AuthSession.exchangeCodeAsync(
      {
        clientId: this.config.clientId,
        code: result.params.code,
        redirectUri,
        extraParams: { code_verifier: request.codeVerifier ?? "" },
      },
      discovery
    );

    await this.storeTokens(tokenResult, provider);
  }

  async signOut(): Promise<void> {
    await this.clearStorage();
    this.currentUser = null;
  }

  async getAccessToken(): Promise<string | null> {
    const [accessToken, expiry] = await Promise.all([
      SecureStore.getItemAsync(KEYS.accessToken),
      SecureStore.getItemAsync(KEYS.tokenExpiry),
    ]);
    if (!accessToken || !expiry) return null;
    if (Date.now() < parseInt(expiry, 10)) return accessToken;
    if (await this.tryRefresh()) {
      return SecureStore.getItemAsync(KEYS.accessToken);
    }
    return null;
  }

  private async ensureDiscovery(): Promise<AuthSession.DiscoveryDocument> {
    if (!this.discovery) {
      this.discovery = await AuthSession.fetchDiscoveryAsync(this.config.authority);
    }
    return this.discovery;
  }

  private async tryRefresh(): Promise<boolean> {
    const [refreshToken, userInfoJson] = await Promise.all([
      SecureStore.getItemAsync(KEYS.refreshToken),
      SecureStore.getItemAsync(KEYS.userInfo),
    ]);
    if (!refreshToken || !userInfoJson) return false;
    try {
      const discovery = await this.ensureDiscovery();
      const tokenResult = await AuthSession.refreshAsync(
        { clientId: this.config.clientId, refreshToken },
        discovery
      );
      const user = JSON.parse(userInfoJson) as CurrentUser;
      await this.storeTokens(tokenResult as AuthSession.TokenResponse, user.provider);
      return true;
    } catch {
      await this.clearStorage();
      this.currentUser = null;
      return false;
    }
  }

  private async storeTokens(
    tokenResult: AuthSession.TokenResponse,
    provider: AuthProvider
  ): Promise<void> {
    const expiryMs = Date.now() + (tokenResult.expiresIn ?? 3600) * 1000;

    let userId = "unknown";
    let name: string | undefined;
    if (tokenResult.idToken) {
      const payload = parseJwtPayload(tokenResult.idToken);
      userId =
        (payload["sub"] as string | undefined) ??
        (payload["oid"] as string | undefined) ??
        "unknown";
      name =
        (payload["name"] as string | undefined) ??
        (payload["given_name"] as string | undefined);
    }

    const user: CurrentUser = { id: userId, name, provider };
    this.currentUser = user;

    await Promise.all([
      SecureStore.setItemAsync(KEYS.accessToken, tokenResult.accessToken),
      SecureStore.setItemAsync(KEYS.tokenExpiry, String(expiryMs)),
      SecureStore.setItemAsync(KEYS.userInfo, JSON.stringify(user)),
      tokenResult.refreshToken
        ? SecureStore.setItemAsync(KEYS.refreshToken, tokenResult.refreshToken)
        : Promise.resolve(),
    ]);
  }

  private async clearStorage(): Promise<void> {
    await Promise.all(Object.values(KEYS).map((k) => SecureStore.deleteItemAsync(k)));
  }
}
