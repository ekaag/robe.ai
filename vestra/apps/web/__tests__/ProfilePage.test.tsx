import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ApiClientProvider, FakeApiClient } from "@vestra/api";
import ProfilePage from "../app/profile/page";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), back: vi.fn() }),
  usePathname: () => "/profile",
  useParams: () => ({}),
}));

function wrap(client: FakeApiClient) {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={qc}>
      <ApiClientProvider client={client}>
        <ProfilePage />
      </ApiClientProvider>
    </QueryClientProvider>
  );
}

describe("ProfilePage", () => {
  let client: FakeApiClient;

  beforeEach(() => {
    client = new FakeApiClient();
  });

  it("renders dominant style chips when profile is loaded", async () => {
    wrap(client);

    await waitFor(() => expect(screen.getByText("minimalist")).toBeInTheDocument());
    expect(screen.getByText("classic")).toBeInTheDocument();
  });

  it("renders PaletteStrip with color swatches from the profile", async () => {
    wrap(client);

    await waitFor(() => expect(screen.getByLabelText("Color palette")).toBeInTheDocument());
    expect(screen.getByLabelText("navy")).toBeInTheDocument();
    expect(screen.getByLabelText("white")).toBeInTheDocument();
  });

  it("renders FormalityDots with the typical formality level", async () => {
    wrap(client);

    // fakeProfile has formalityRange.typical = 2 → 2 filled dots out of 5
    await waitFor(() => expect(screen.getByText(/range/i)).toBeInTheDocument());
    const dots = document.querySelectorAll('[aria-hidden="true"]');
    expect(dots).toHaveLength(5);
    const filled = Array.from(dots).filter(
      (d) => (d as HTMLElement).style.backgroundColor !== "var(--color-line)"
    );
    expect(filled).toHaveLength(2);
  });

  it("renders the editorial summary quote", async () => {
    wrap(client);

    await waitFor(() =>
      expect(
        screen.getByText(/leans minimalist and neutral/i)
      ).toBeInTheDocument()
    );
  });

  it("shows Regenerate button on loaded profile", async () => {
    wrap(client);

    await waitFor(() =>
      expect(screen.getByRole("button", { name: /regenerate profile/i })).toBeInTheDocument()
    );
  });

  it("shows empty state and Generate button when profile is null", async () => {
    vi.spyOn(client, "getProfile").mockResolvedValue(null);
    wrap(client);

    await waitFor(() =>
      expect(screen.getByRole("button", { name: /generate profile/i })).toBeInTheDocument()
    );
    expect(
      screen.getByText(/generate your profile to see a personalized style analysis/i)
    ).toBeInTheDocument();
  });

  it("clicking Generate on null state fires the mutation and renders profile", async () => {
    vi.spyOn(client, "getProfile").mockResolvedValue(null);
    const generateSpy = vi.spyOn(client, "generateProfile");

    wrap(client);

    await waitFor(() => screen.getByRole("button", { name: /generate profile/i }));
    await userEvent.click(screen.getByRole("button", { name: /generate profile/i }));

    expect(generateSpy).toHaveBeenCalledOnce();
    // After mutation, onSuccess sets query data → profile chips appear
    await waitFor(() => expect(screen.getByText("minimalist")).toBeInTheDocument());
  });

  it("Regenerate button on loaded profile fires the mutation", async () => {
    const generateSpy = vi.spyOn(client, "generateProfile");

    wrap(client);

    await waitFor(() => screen.getByRole("button", { name: /regenerate profile/i }));
    await userEvent.click(screen.getByRole("button", { name: /regenerate profile/i }));

    expect(generateSpy).toHaveBeenCalledOnce();
  });

  it("shows thin-wardrobe message when garmentCount is 0", async () => {
    const fakeProfileBase = await client.getProfile();
    vi.spyOn(client, "getProfile").mockResolvedValue({
      ...fakeProfileBase!,
      garmentCount: 0,
    });

    wrap(client);

    await waitFor(() =>
      expect(
        screen.getByText(/add garments to your wardrobe/i)
      ).toBeInTheDocument()
    );
    // Dominant style chips should NOT appear in this state
    expect(screen.queryByText("minimalist")).not.toBeInTheDocument();
  });

  it("shows garment count and generated date in metadata", async () => {
    wrap(client);

    // fakeProfile has garmentCount: 5
    await waitFor(() => expect(screen.getByText(/5 pieces/i)).toBeInTheDocument());
  });
});
