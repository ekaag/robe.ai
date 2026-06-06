import { describe, it, expect, vi, beforeEach } from "vitest";

vi.mock("@azure/msal-browser", () => ({
  PublicClientApplication: vi.fn(),
  InteractionRequiredAuthError: class InteractionRequiredAuthError extends Error {
    constructor(errorCode: string) {
      super(errorCode);
      this.name = "InteractionRequiredAuthError";
    }
  },
}));

import { PublicClientApplication, InteractionRequiredAuthError } from "@azure/msal-browser";
import { WebMsalAuthProvider } from "../WebMsalAuthProvider";
import type { EntraConfig } from "../msalConfig";

const mockConfig: EntraConfig = {
  authority: "https://test.ciamlogin.com/test.onmicrosoft.com",
  clientId: "test-client-id",
  redirectUri: "http://localhost:3000",
  apiScope: "https://test.onmicrosoft.com/api/access_as_user",
};

const mockAccount = {
  homeAccountId: "usr_123",
  name: "Test User",
  username: "test@example.com",
  idTokenClaims: { idp: "google" },
  environment: "test",
  tenantId: "tenant_123",
  localAccountId: "local_123",
};

const mockPca = {
  initialize: vi.fn<[], Promise<void>>().mockResolvedValue(undefined),
  getAllAccounts: vi.fn().mockReturnValue([]),
  setActiveAccount: vi.fn(),
  getActiveAccount: vi.fn<[], typeof mockAccount | null>().mockReturnValue(null),
  loginPopup: vi.fn().mockResolvedValue({ accessToken: "token", account: mockAccount }),
  logoutPopup: vi.fn<[], Promise<void>>().mockResolvedValue(undefined),
  acquireTokenSilent: vi.fn().mockResolvedValue({ accessToken: "silent-token" }),
  acquireTokenPopup: vi.fn().mockResolvedValue({ accessToken: "popup-token" }),
};

describe("WebMsalAuthProvider", () => {
  let provider: WebMsalAuthProvider;

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(PublicClientApplication).mockImplementation(() => mockPca as any);
    mockPca.getAllAccounts.mockReturnValue([]);
    mockPca.getActiveAccount.mockReturnValue(null);
    provider = new WebMsalAuthProvider(mockConfig);
  });

  describe("initialize()", () => {
    it("calls pca.initialize()", async () => {
      await provider.initialize();
      expect(mockPca.initialize).toHaveBeenCalledOnce();
    });

    it("restores currentUser from a cached account", async () => {
      mockPca.getAllAccounts.mockReturnValue([mockAccount]);
      await provider.initialize();
      expect(provider.currentUser).not.toBeNull();
      expect(provider.currentUser?.id).toBe("usr_123");
      expect(provider.currentUser?.provider).toBe("google");
    });

    it("leaves currentUser null when no cached accounts", async () => {
      mockPca.getAllAccounts.mockReturnValue([]);
      await provider.initialize();
      expect(provider.currentUser).toBeNull();
    });
  });

  describe("signIn()", () => {
    it("calls loginPopup with domainHint for google", async () => {
      await provider.signIn("google");
      expect(mockPca.loginPopup).toHaveBeenCalledWith(
        expect.objectContaining({ domainHint: "google.com" })
      );
    });

    it("calls loginPopup with domainHint for facebook", async () => {
      await provider.signIn("facebook");
      expect(mockPca.loginPopup).toHaveBeenCalledWith(
        expect.objectContaining({ domainHint: "facebook.com" })
      );
    });

    it("calls loginPopup with domainHint for apple", async () => {
      await provider.signIn("apple");
      expect(mockPca.loginPopup).toHaveBeenCalledWith(
        expect.objectContaining({ domainHint: "apple.com" })
      );
    });

    it("calls loginPopup without domainHint for microsoft", async () => {
      await provider.signIn("microsoft");
      const callArg = mockPca.loginPopup.mock.calls[0][0] as Record<string, unknown>;
      expect(callArg.domainHint).toBeUndefined();
    });

    it("sets currentUser after successful sign-in", async () => {
      await provider.signIn("google");
      expect(provider.currentUser).not.toBeNull();
      expect(provider.currentUser?.id).toBe("usr_123");
      expect(provider.currentUser?.provider).toBe("google");
    });
  });

  describe("signOut()", () => {
    it("calls logoutPopup and clears currentUser", async () => {
      await provider.signIn("google");
      mockPca.getActiveAccount.mockReturnValue(mockAccount as any);
      await provider.signOut();
      expect(mockPca.logoutPopup).toHaveBeenCalledOnce();
      expect(provider.currentUser).toBeNull();
    });
  });

  describe("getAccessToken()", () => {
    it("returns null when no active account", async () => {
      mockPca.getActiveAccount.mockReturnValue(null);
      expect(await provider.getAccessToken()).toBeNull();
      expect(mockPca.acquireTokenSilent).not.toHaveBeenCalled();
    });

    it("returns token from acquireTokenSilent", async () => {
      mockPca.getActiveAccount.mockReturnValue(mockAccount as any);
      expect(await provider.getAccessToken()).toBe("silent-token");
      expect(mockPca.acquireTokenSilent).toHaveBeenCalledOnce();
    });

    it("falls back to acquireTokenPopup on InteractionRequiredAuthError", async () => {
      mockPca.getActiveAccount.mockReturnValue(mockAccount as any);
      mockPca.acquireTokenSilent.mockRejectedValueOnce(
        new InteractionRequiredAuthError("interaction_required")
      );
      expect(await provider.getAccessToken()).toBe("popup-token");
      expect(mockPca.acquireTokenPopup).toHaveBeenCalledOnce();
    });

    it("returns null when silent fails with a non-interaction error", async () => {
      mockPca.getActiveAccount.mockReturnValue(mockAccount as any);
      mockPca.acquireTokenSilent.mockRejectedValueOnce(new Error("network error"));
      expect(await provider.getAccessToken()).toBeNull();
      expect(mockPca.acquireTokenPopup).not.toHaveBeenCalled();
    });
  });
});
