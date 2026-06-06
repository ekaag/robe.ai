import { describe, it, expect, vi, beforeEach } from "vitest";

// vi.hoisted creates variables before vi.mock factories run (which are also hoisted).
const { store, authMocks } = vi.hoisted(() => {
  const store: Record<string, string> = {};

  // base64url payload for {"sub":"usr_native","name":"Native User"}
  const MOCK_ID_TOKEN =
    "header.eyJzdWIiOiJ1c3JfbmF0aXZlIiwibmFtZSI6Ik5hdGl2ZSBVc2VyIn0.sig";

  const mockAuthRequest = {
    codeVerifier: "test-verifier",
    promptAsync: vi.fn().mockResolvedValue({
      type: "success",
      params: { code: "test-code" },
    }),
  };

  const authMocks = {
    fetchDiscoveryAsync: vi.fn().mockResolvedValue({
      authorizationEndpoint: "https://test.example.com/authorize",
      tokenEndpoint: "https://test.example.com/token",
    }),
    AuthRequest: vi.fn(() => mockAuthRequest),
    exchangeCodeAsync: vi.fn().mockResolvedValue({
      accessToken: "native-access-token",
      refreshToken: "native-refresh-token",
      expiresIn: 3600,
      idToken: MOCK_ID_TOKEN,
    }),
    refreshAsync: vi.fn().mockResolvedValue({
      accessToken: "refreshed-access-token",
      expiresIn: 3600,
      idToken: MOCK_ID_TOKEN,
    }),
    makeRedirectUri: vi.fn().mockReturnValue("vestra://"),
    mockAuthRequest,
  };

  return { store, authMocks };
});

vi.mock("expo-web-browser", () => ({
  warmUpAsync: vi.fn().mockResolvedValue(undefined),
  coolDownAsync: vi.fn().mockResolvedValue(undefined),
}));

vi.mock("expo-secure-store", () => ({
  getItemAsync: vi.fn((key: string) => Promise.resolve(store[key] ?? null)),
  setItemAsync: vi.fn((key: string, value: string) => {
    store[key] = value;
    return Promise.resolve();
  }),
  deleteItemAsync: vi.fn((key: string) => {
    delete store[key];
    return Promise.resolve();
  }),
}));

vi.mock("expo-auth-session", () => authMocks);

import * as SecureStore from "expo-secure-store";
import * as AuthSession from "expo-auth-session";
import { NativeMsalAuthProvider } from "../NativeMsalAuthProvider";
import type { NativeEntraConfig } from "../nativeAuthConfig";

const mockConfig: NativeEntraConfig = {
  authority: "https://test.ciamlogin.com/test.onmicrosoft.com",
  clientId: "test-client-id",
  apiScope: "https://test.onmicrosoft.com/api/access_as_user",
};

describe("NativeMsalAuthProvider", () => {
  let provider: NativeMsalAuthProvider;

  beforeEach(() => {
    vi.clearAllMocks();
    Object.keys(store).forEach((k) => delete store[k]);
    provider = new NativeMsalAuthProvider(mockConfig);
  });

  describe("initialize()", () => {
    it("leaves currentUser null when no stored tokens", async () => {
      await provider.initialize();
      expect(provider.currentUser).toBeNull();
    });

    it("restores currentUser from valid stored tokens", async () => {
      const user = { id: "usr_native", name: "Native User", provider: "google" as const };
      store["vestra_access_token"] = "stored-token";
      store["vestra_token_expiry"] = String(Date.now() + 3_600_000);
      store["vestra_user_info"] = JSON.stringify(user);

      await provider.initialize();

      expect(provider.currentUser).toMatchObject({ id: "usr_native", provider: "google" });
    });

    it("clears storage when token is expired and no refresh token available", async () => {
      store["vestra_access_token"] = "expired-token";
      store["vestra_token_expiry"] = "0"; // already expired
      store["vestra_user_info"] = JSON.stringify({ id: "usr_x", provider: "google" });

      await provider.initialize();

      expect(provider.currentUser).toBeNull();
      expect(SecureStore.deleteItemAsync).toHaveBeenCalled();
    });

    it("refreshes session when expired token has a stored refresh token", async () => {
      store["vestra_access_token"] = "expired-token";
      store["vestra_token_expiry"] = "0";
      store["vestra_refresh_token"] = "old-refresh-token";
      store["vestra_user_info"] = JSON.stringify({ id: "usr_x", name: "X", provider: "google" });

      await provider.initialize();

      expect(AuthSession.refreshAsync).toHaveBeenCalled();
      expect(provider.currentUser).not.toBeNull();
    });
  });

  describe("signIn()", () => {
    it("sets currentUser on a successful sign-in", async () => {
      await provider.signIn("google");
      expect(provider.currentUser).not.toBeNull();
      expect(provider.currentUser?.provider).toBe("google");
    });

    it("parses userId from the ID token", async () => {
      await provider.signIn("google");
      expect(provider.currentUser?.id).toBe("usr_native");
    });

    it("stores access and refresh tokens in SecureStore", async () => {
      await provider.signIn("google");
      expect(SecureStore.setItemAsync).toHaveBeenCalledWith(
        "vestra_access_token",
        "native-access-token"
      );
      expect(SecureStore.setItemAsync).toHaveBeenCalledWith(
        "vestra_refresh_token",
        "native-refresh-token"
      );
    });

    it("passes domain_hint for google", async () => {
      await provider.signIn("google");
      const reqConfig = vi.mocked(AuthSession.AuthRequest).mock.calls[0][0] as {
        extraParams?: Record<string, string>;
      };
      expect(reqConfig.extraParams?.domain_hint).toBe("google.com");
    });

    it("does not set currentUser when browser is dismissed", async () => {
      authMocks.mockAuthRequest.promptAsync.mockResolvedValueOnce({ type: "dismiss" });
      await provider.signIn("google");
      expect(provider.currentUser).toBeNull();
    });
  });

  describe("signOut()", () => {
    it("clears storage and nullifies currentUser", async () => {
      await provider.signIn("google");
      await provider.signOut();
      expect(provider.currentUser).toBeNull();
      expect(SecureStore.deleteItemAsync).toHaveBeenCalled();
    });
  });

  describe("getAccessToken()", () => {
    it("returns null when no token is stored", async () => {
      expect(await provider.getAccessToken()).toBeNull();
    });

    it("returns stored token when it is not expired", async () => {
      store["vestra_access_token"] = "valid-token";
      store["vestra_token_expiry"] = String(Date.now() + 3_600_000);
      expect(await provider.getAccessToken()).toBe("valid-token");
    });

    it("calls refreshAsync and returns new token when expired", async () => {
      store["vestra_access_token"] = "expired-token";
      store["vestra_token_expiry"] = "0";
      store["vestra_refresh_token"] = "refresh-token";
      store["vestra_user_info"] = JSON.stringify({ id: "u", name: "U", provider: "google" });

      await provider.getAccessToken();

      expect(AuthSession.refreshAsync).toHaveBeenCalled();
    });

    it("returns null when expired and refresh fails", async () => {
      store["vestra_access_token"] = "expired-token";
      store["vestra_token_expiry"] = "0";
      store["vestra_refresh_token"] = "bad-refresh-token";
      store["vestra_user_info"] = JSON.stringify({ id: "u", name: "U", provider: "google" });

      vi.mocked(AuthSession.refreshAsync).mockRejectedValueOnce(new Error("refresh failed"));

      expect(await provider.getAccessToken()).toBeNull();
    });
  });
});
