export type AuthProvider = "apple" | "google" | "microsoft" | "facebook";

export interface CurrentUser {
  id: string;
  name?: string;
  provider: AuthProvider;
}

export interface IAuthProvider {
  signIn(provider: AuthProvider): Promise<void>;
  signOut(): Promise<void>;
  getAccessToken(): Promise<string | null>;
  currentUser: CurrentUser | null;
}

// Concrete impls added in step 1:
// - WebMsalAuthProvider   (@azure/msal-browser + @azure/msal-react)
// - NativeMsalAuthProvider (expo-auth-session against Entra External ID)

export class FakeAuthProvider implements IAuthProvider {
  currentUser: CurrentUser | null = {
    id: "usr_fake",
    name: "Dev User",
    provider: "google",
  };

  async signIn(_provider: AuthProvider): Promise<void> {
    this.currentUser = { id: "usr_fake", name: "Dev User", provider: _provider };
  }

  async signOut(): Promise<void> {
    this.currentUser = null;
  }

  async getAccessToken(): Promise<string | null> {
    return this.currentUser ? "fake-access-token" : null;
  }
}
