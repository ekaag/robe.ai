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

// Entra CIAM's default "Display Name" application claim resolves to this literal
// placeholder when the user flow returns the claim without ever collecting a real
// value from the federated IdP (seen with Google sign-in when the claim isn't
// properly mapped in the user flow — see FRONTEND.md "CIAM + Google federation
// gives a sparse ID token"). Treat it like an empty claim rather than displaying
// it, so a misconfigured tenant degrades to the provider-name fallback instead.
const ENTRA_PLACEHOLDER_NAMES = new Set(["unknown"]);

function realClaim(value: string | undefined): string | undefined {
  const trimmed = value?.trim();
  if (!trimmed || ENTRA_PLACEHOLDER_NAMES.has(trimmed.toLowerCase())) return undefined;
  return trimmed;
}

// When no real email/preferred_username claim is present, MSAL's account.username
// for a CIAM-federated (social) account falls back to a synthetic UPN of the form
// "<oid-guid>@<tenant>.onmicrosoft.com" — it passes a naive "contains @" check but
// is just the object ID reformatted, not anything a user would recognize as theirs.
const GUID_LOCAL_PART = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}@/i;

function plausibleEmail(value: string | undefined): string | undefined {
  const trimmed = realClaim(value);
  if (!trimmed || !trimmed.includes("@") || GUID_LOCAL_PART.test(trimmed)) return undefined;
  return trimmed;
}

function accountToUser(account: AccountInfo, provider: AuthProvider): CurrentUser {
  const c = account.idTokenClaims as Record<string, unknown> | undefined;

  // oid is the stable Entra object ID — matches what the backend extracts from the JWT.
  const id = (c?.["oid"] as string | undefined) ?? account.homeAccountId;

  // CIAM user flows don't always populate account.name; try several claim sources.
  // preferred_username / email are most reliable for Google-federated CIAM accounts.
  const given  = realClaim(c?.["given_name"] as string | undefined);
  const family = realClaim(c?.["family_name"] as string | undefined);
  const full   = given && family ? `${given} ${family}` : given ?? family;
  const email  =
    plausibleEmail(c?.["email"] as string | undefined) ||
    plausibleEmail(c?.["preferred_username"] as string | undefined) ||
    plausibleEmail(account.username);
  const name   =
    full ||
    realClaim(c?.["name"] as string | undefined) ||
    realClaim(account.name) ||
    email ||
    undefined;

  return { id, name, provider };
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

  constructor(config: EntraConfig) {
    this.pca = new PublicClientApplication(buildMsalConfig(config));
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

  // Entra External ID (CIAM) won't issue access tokens for api:// scoped
  // resources to Google-federated accounts (AADSTS500207). ID tokens always
  // work for social sign-in and serve as the bearer credential for the backend.
  private _cachedIdToken: string | null = null;

  async signIn(provider: AuthProvider): Promise<void> {
    const domainHint = DOMAIN_HINTS[provider];
    const result = await this.pca.loginPopup({
      // "profile"/"email" scopes are what actually make Entra release the
      // name/given_name/family_name/email claims into the ID token — without
      // them the token stays sparse regardless of the user flow's claim config.
      scopes: ["openid", "profile", "email", "offline_access"],
      redirectUri: this.blankRedirectUri,
      ...(domainHint ? { domainHint } : {}),
    });
    this.pca.setActiveAccount(result.account);
    this.currentUser = accountToUser(result.account, provider);
    this._cachedIdToken = result.idToken;
  }

  async signOut(): Promise<void> {
    const account = this.pca.getActiveAccount();
    await this.pca.logoutPopup({
      ...(account ? { account } : {}),
      postLogoutRedirectUri: this.blankRedirectUri,
    });
    this.currentUser = null;
    this._cachedIdToken = null;
  }

  async getAccessToken(): Promise<string | null> {
    const account = this.pca.getActiveAccount();
    if (!account) return null;
    try {
      const result = await this.pca.acquireTokenSilent({
        account,
        scopes: ["openid", "profile", "email"],
      });
      this._cachedIdToken = result.idToken;
      return result.idToken;
    } catch (e) {
      if (e instanceof InteractionRequiredAuthError) {
        const result = await this.pca.acquireTokenPopup({
          account,
          scopes: ["openid", "profile", "email"],
          redirectUri: this.blankRedirectUri,
        });
        this._cachedIdToken = result.idToken;
        return result.idToken;
      }
      return this._cachedIdToken;
    }
  }
}
