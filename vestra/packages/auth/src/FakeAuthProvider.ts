import type { IAuthProvider, AuthProvider, CurrentUser } from "./types";

export class FakeAuthProvider implements IAuthProvider {
  currentUser: CurrentUser | null = {
    id: "usr_fake",
    name: "Dev User",
    provider: "google",
  };

  async signIn(provider: AuthProvider): Promise<void> {
    this.currentUser = { id: "usr_fake", name: "Dev User", provider };
  }

  async signOut(): Promise<void> {
    this.currentUser = null;
  }

  async getAccessToken(): Promise<string | null> {
    return this.currentUser ? "fake-access-token" : null;
  }
}
