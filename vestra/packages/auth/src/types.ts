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
