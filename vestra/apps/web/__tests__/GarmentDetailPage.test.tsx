import { describe, it, expect, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ApiClientProvider, FakeApiClient, ApiError } from "@vestra/api";
import GarmentDetailPage from "../app/wardrobe/[id]/page";

const mockBack = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ back: mockBack, push: vi.fn() }),
  usePathname: () => "/wardrobe/grm_001",
  useParams: () => ({ id: "grm_001" }),
}));

function wrap(client: FakeApiClient) {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={qc}>
      <ApiClientProvider client={client}>
        <GarmentDetailPage />
      </ApiClientProvider>
    </QueryClientProvider>
  );
}

describe("GarmentDetailPage", () => {
  it("renders trait rows for the garment", async () => {
    const client = new FakeApiClient();
    wrap(client);

    await waitFor(() => expect(screen.getByText("Category")).toBeInTheDocument());
    expect(screen.getByText("top · t-shirt")).toBeInTheDocument();
    expect(screen.getByText("Color")).toBeInTheDocument();
    expect(screen.getByText("navy")).toBeInTheDocument();
    expect(screen.getByText("Pattern")).toBeInTheDocument();
    expect(screen.getByText("solid")).toBeInTheDocument();
    expect(screen.getByText("Material")).toBeInTheDocument();
    expect(screen.getByText("cotton")).toBeInTheDocument();
    expect(screen.getByText("Fit")).toBeInTheDocument();
    expect(screen.getByText("regular")).toBeInTheDocument();
  });

  it("renders FormalityDots with the correct filled count", async () => {
    const client = new FakeApiClient();
    wrap(client);

    await waitFor(() => expect(screen.getByText("Formality")).toBeInTheDocument());
    // grm_001 has formality = 2; FormalityDots renders 5 aria-hidden divs
    const dots = document.querySelectorAll('[aria-hidden="true"]');
    expect(dots).toHaveLength(5);
    const filled = Array.from(dots).filter(
      (d) => (d as HTMLElement).style.backgroundColor !== "var(--color-line)"
    );
    expect(filled).toHaveLength(2);
  });

  it("renders ConfidenceBar with the correct percentage", async () => {
    const client = new FakeApiClient();
    wrap(client);

    await waitFor(() => expect(screen.getByText("92%")).toBeInTheDocument());
    expect(screen.getByText("Confidence")).toBeInTheDocument();
  });

  it("shows error state when garment is not found", async () => {
    const client = new FakeApiClient();
    vi.spyOn(client, "getGarment").mockRejectedValue(new ApiError(404, "not found"));

    wrap(client);

    await waitFor(() =>
      expect(screen.getByText("Garment not found.")).toBeInTheDocument()
    );
  });

  it("back button calls router.back()", async () => {
    const client = new FakeApiClient();
    wrap(client);

    await waitFor(() => expect(screen.getByText("← Wardrobe")).toBeInTheDocument());
    await userEvent.click(screen.getByText("← Wardrobe"));
    expect(mockBack).toHaveBeenCalled();
  });
});
