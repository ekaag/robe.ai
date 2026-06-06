import { describe, it, expect, beforeEach } from "vitest";
import { FakeAuthProvider } from "../FakeAuthProvider";

describe("FakeAuthProvider", () => {
  let auth: FakeAuthProvider;

  beforeEach(() => {
    auth = new FakeAuthProvider();
  });

  it("starts signed in with a default user", () => {
    expect(auth.currentUser).not.toBeNull();
    expect(auth.currentUser?.id).toBe("usr_fake");
  });

  it("signIn sets currentUser with the given provider", async () => {
    auth.currentUser = null;
    await auth.signIn("apple");
    expect(auth.currentUser?.provider).toBe("apple");
    expect(auth.currentUser?.id).toBe("usr_fake");
  });

  it("signIn updates provider when already signed in", async () => {
    await auth.signIn("microsoft");
    expect(auth.currentUser?.provider).toBe("microsoft");
  });

  it("signOut clears currentUser", async () => {
    await auth.signOut();
    expect(auth.currentUser).toBeNull();
  });

  it("getAccessToken returns token when signed in", async () => {
    const token = await auth.getAccessToken();
    expect(token).toBe("fake-access-token");
  });

  it("getAccessToken returns null when signed out", async () => {
    await auth.signOut();
    const token = await auth.getAccessToken();
    expect(token).toBeNull();
  });
});
