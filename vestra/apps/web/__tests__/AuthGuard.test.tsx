import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { AuthContextProvider, FakeAuthProvider } from "@vestra/auth";
import { AuthGuard } from "../components/AuthGuard";

const mockReplace = vi.fn();
const mockPathname = { value: "/" };

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: mockReplace }),
  usePathname: () => mockPathname.value,
}));

function wrap(auth: FakeAuthProvider, children: React.ReactNode) {
  return render(
    <AuthContextProvider auth={auth}>{children}</AuthContextProvider>
  );
}

describe("AuthGuard", () => {
  let auth: FakeAuthProvider;

  beforeEach(() => {
    auth = new FakeAuthProvider();
    mockReplace.mockClear();
    mockPathname.value = "/";
  });

  it("renders children when the user is signed in", async () => {
    await auth.signIn("google");
    wrap(auth, <AuthGuard><div>Protected</div></AuthGuard>);
    expect(screen.getByText("Protected")).toBeInTheDocument();
  });

  it("hides content and redirects to /login when not signed in", async () => {
    auth.currentUser = null;
    wrap(auth, <AuthGuard><div>Protected</div></AuthGuard>);
    expect(screen.queryByText("Protected")).not.toBeInTheDocument();
    await vi.waitFor(() => expect(mockReplace).toHaveBeenCalledWith("/login"));
  });

  it("does not redirect when already on /login even if not signed in", async () => {
    auth.currentUser = null;
    mockPathname.value = "/login";
    wrap(auth, <AuthGuard><div>Login page</div></AuthGuard>);
    await vi.waitFor(() => {}); // flush effects
    expect(mockReplace).not.toHaveBeenCalled();
    expect(screen.getByText("Login page")).toBeInTheDocument();
  });
});
