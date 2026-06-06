import { describe, it, expect, beforeEach } from "vitest";
import { FakeAuthProvider } from "../FakeAuthProvider";

describe("FakeAuthProvider", () => {
  let auth: FakeAuthProvider;

  beforeEach(() => {
    auth = new FakeAuthProvider();
  });

  it("starts signed out", () => {
    expect(auth.currentUser).toBeNull();
  });

  it("signIn sets currentUser with the given provider", async () => {
    await auth.signIn("apple");
    expect(auth.currentUser?.provider).toBe("apple");
    expect(auth.currentUser?.id).toBe("usr_fake");
  });

  it("signIn replaces currentUser when called again", async () => {
    await auth.signIn("google");
    await auth.signIn("microsoft");
    expect(auth.currentUser?.provider).toBe("microsoft");
  });

  it("signOut clears currentUser", async () => {
    await auth.signIn("google");
    await auth.signOut();
    expect(auth.currentUser).toBeNull();
  });

  it("getAccessToken returns token when signed in", async () => {
    await auth.signIn("google");
    const token = await auth.getAccessToken();
    expect(token).toBe("fake-access-token");
  });

  it("getAccessToken returns null when signed out", async () => {
    const token = await auth.getAccessToken();
    expect(token).toBeNull();
  });
});
