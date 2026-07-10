import type { IAuthProvider, AuthProvider, CurrentUser } from "./types";

export class FakeAuthProvider implements IAuthProvider {
  currentUser: CurrentUser | null = { id: "dev-user", name: "Dev User", provider: "google" };

  async signIn(provider: AuthProvider): Promise<void> {
    this.currentUser = { id: "dev-user", name: "Dev User", provider };
  }

  async signOut(): Promise<void> {
    this.currentUser = null;
  }

  async getAccessToken(): Promise<string | null> {
    if (!this.currentUser) return null;
    // Minimal fake JWT (header.payload.sig) so LocalAuthHandler.TryBuildTicketFromJwt
    // can decode the sub claim without signature validation. A plain string like
    // "fake-access-token" has no dots and fails the 3-part check, causing a 401.
    const payload = btoa(JSON.stringify({ sub: this.currentUser.id, name: this.currentUser.name ?? "" }))
      .replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
    return `eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.${payload}.fakesig`;
  }
}
