import {
  PublicClientApplication,
  InteractionRequiredAuthError,
  type AccountInfo,
} from "@azure/msal-browser";
import type { IAuthProvider, AuthProvider, CurrentUser } from "@vestra/auth";
import { buildMsalConfig, type EntraConfig } from "./msalConfig";

// Maps our provider names to the domain_hint Entra accepts to pre-select an IdP.
// Apple: Entra CIAM may also accept extraQueryParameters: { idp: "Apple" } —
// verify which works for your user flow during the live test.
const DOMAIN_HINTS: Partial<Record<AuthProvider, string>> = {
  google: "google.com",
  facebook: "facebook.com",
  apple: "apple.com",
};

function accountToUser(account: AccountInfo, provider: AuthProvider): CurrentUser {
  return {
    id: account.homeAccountId,
    name: account.name ?? account.username,
    provider,
  };
}

function inferProvider(account: AccountInfo): AuthProvider {
  const idp = account.idTokenClaims?.["idp"] as string | undefined;
  if (idp?.includes("google")) return "google";
  if (idp?.includes("facebook")) return "facebook";
  if (idp?.includes("apple")) return "apple";
  return "microsoft";
}

export class WebMsalAuthProvider implements IAuthProvider {
  currentUser: CurrentUser | null = null;
  private readonly pca: PublicClientApplication;
  private readonly apiScope: string;

  constructor(config: EntraConfig) {
    this.pca = new PublicClientApplication(buildMsalConfig(config));
    this.apiScope = config.apiScope;
  }

  async initialize(): Promise<void> {
    await this.pca.initialize();
    const accounts = this.pca.getAllAccounts();
    if (accounts.length > 0) {
      const account = accounts[0];
      this.pca.setActiveAccount(account);
      this.currentUser = accountToUser(account, inferProvider(account));
    }
  }

  private get blankRedirectUri(): string {
    return window.location.origin + "/auth/blank";
  }

  async signIn(provider: AuthProvider): Promise<void> {
    const domainHint = DOMAIN_HINTS[provider];
    // Entra External ID (CIAM) requires OIDC scopes and API scopes to be
    // requested separately — mixing them in loginPopup causes AADSTS500011.
    // The API access token is acquired on demand in getAccessToken().
    const result = await this.pca.loginPopup({
      scopes: ["openid", "profile", "offline_access"],
      redirectUri: this.blankRedirectUri,
      ...(domainHint ? { domainHint } : {}),
    });
    this.pca.setActiveAccount(result.account);
    this.currentUser = accountToUser(result.account, provider);
  }

  async signOut(): Promise<void> {
    const account = this.pca.getActiveAccount();
    await this.pca.logoutPopup({
      ...(account ? { account } : {}),
      postLogoutRedirectUri: this.blankRedirectUri,
    });
    this.currentUser = null;
  }

  async getAccessToken(): Promise<string | null> {
    const account = this.pca.getActiveAccount();
    if (!account) return null;
    try {
      const result = await this.pca.acquireTokenSilent({
        account,
        scopes: [this.apiScope],
      });
      return result.accessToken;
    } catch (e) {
      if (e instanceof InteractionRequiredAuthError) {
        const result = await this.pca.acquireTokenPopup({
          account,
          scopes: [this.apiScope],
          redirectUri: this.blankRedirectUri,
        });
        return result.accessToken;
      }
      return null;
    }
  }
}
