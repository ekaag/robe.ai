import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { AuthContextProvider, FakeAuthProvider } from "@vestra/auth";
import LoginPage from "../app/login/page";

const mockPush = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: mockPush, replace: mockPush }),
  usePathname: () => "/login",
}));

function renderLoginPage(auth: FakeAuthProvider) {
  return render(
    <AuthContextProvider auth={auth}>
      <LoginPage />
    </AuthContextProvider>
  );
}

describe("LoginPage", () => {
  let auth: FakeAuthProvider;

  beforeEach(() => {
    auth = new FakeAuthProvider();
    auth.currentUser = null;
    mockPush.mockClear();
  });

  it("renders all four provider buttons", () => {
    renderLoginPage(auth);
    expect(screen.getByText(/continue with apple/i)).toBeInTheDocument();
    expect(screen.getByText(/continue with google/i)).toBeInTheDocument();
    expect(screen.getByText(/continue with microsoft/i)).toBeInTheDocument();
    expect(screen.getByText(/continue with facebook/i)).toBeInTheDocument();
  });

  it("clicking Google calls signIn('google') then navigates home", async () => {
    const signInSpy = vi.spyOn(auth, "signIn");
    renderLoginPage(auth);
    fireEvent.click(screen.getByText(/continue with google/i));
    await waitFor(() => expect(signInSpy).toHaveBeenCalledWith("google"));
    expect(mockPush).toHaveBeenCalledWith("/");
  });

  it("clicking Apple calls signIn('apple') then navigates home", async () => {
    const signInSpy = vi.spyOn(auth, "signIn");
    renderLoginPage(auth);
    fireEvent.click(screen.getByText(/continue with apple/i));
    await waitFor(() => expect(signInSpy).toHaveBeenCalledWith("apple"));
    expect(mockPush).toHaveBeenCalledWith("/");
  });
});
