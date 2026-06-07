import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ApiClientProvider, FakeApiClient } from "@vestra/api";
import WardrobePage from "../app/wardrobe/page";

const mockPush = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: mockPush, back: vi.fn() }),
  usePathname: () => "/wardrobe",
  useParams: () => ({}),
}));

function wrap(client: FakeApiClient) {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={qc}>
      <ApiClientProvider client={client}>
        <WardrobePage />
      </ApiClientProvider>
    </QueryClientProvider>
  );
}

// Synchronous FileReader mock
class MockFileReader {
  onload: ((e: { target: { result: string } }) => void) | null = null;
  readAsDataURL(_file: Blob) {
    this.onload?.({ target: { result: "data:image/jpeg;base64,abc123" } });
  }
}

describe("WardrobePage", () => {
  let client: FakeApiClient;

  beforeEach(() => {
    client = new FakeApiClient();
    mockPush.mockClear();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("renders a card for each seeded garment", async () => {
    wrap(client);
    await waitFor(() =>
      expect(screen.getAllByRole("button", { name: /garment/i })).toHaveLength(4) // 3 cards + AddTile
    );
    expect(screen.getByAltText("top garment")).toBeInTheDocument();
    expect(screen.getByAltText("bottom garment")).toBeInTheDocument();
    expect(screen.getByAltText("outerwear garment")).toBeInTheDocument();
  });

  it("top filter chip shows only top garments", async () => {
    wrap(client);
    await waitFor(() => screen.getByAltText("top garment"));

    await userEvent.click(screen.getByRole("button", { name: /^top$/i }));

    await waitFor(() => {
      expect(screen.getByAltText("top garment")).toBeInTheDocument();
      expect(screen.queryByAltText("bottom garment")).not.toBeInTheDocument();
      expect(screen.queryByAltText("outerwear garment")).not.toBeInTheDocument();
    });
  });

  it("all chip restores the full list after filtering", async () => {
    wrap(client);
    await waitFor(() => screen.getByAltText("bottom garment"));

    await userEvent.click(screen.getByRole("button", { name: /^top$/i }));
    await waitFor(() => expect(screen.queryByAltText("bottom garment")).not.toBeInTheDocument());

    await userEvent.click(screen.getByRole("button", { name: /^all$/i }));
    await waitFor(() => {
      expect(screen.getByAltText("bottom garment")).toBeInTheDocument();
      expect(screen.getByAltText("outerwear garment")).toBeInTheDocument();
    });
  });

  it("clicking a garment card navigates to /wardrobe/<id>", async () => {
    wrap(client);
    await waitFor(() => screen.getByAltText("top garment"));

    await userEvent.click(screen.getByAltText("top garment").closest("button")!);
    expect(mockPush).toHaveBeenCalledWith("/wardrobe/grm_001");
  });

  it("clicking AddTile opens the upload modal", async () => {
    wrap(client);
    await waitFor(() => screen.getByLabelText("Add garment"));

    await userEvent.click(screen.getByLabelText("Add garment"));
    expect(screen.getByText("Add garment", { selector: "h2" })).toBeInTheDocument();
  });

  it("upload flow: file picked → analyze called → review step shown → confirm calls addGarment", async () => {
    vi.stubGlobal("FileReader", MockFileReader);
    const analyzeSpy = vi.spyOn(client, "analyzeGarment");
    const addSpy = vi.spyOn(client, "addGarment");

    wrap(client);
    await waitFor(() => screen.getByLabelText("Add garment"));

    // Open modal
    await userEvent.click(screen.getByLabelText("Add garment"));
    await waitFor(() => screen.getByLabelText("Upload garment photo"));

    // Upload a file
    const fileInput = screen.getByLabelText("Upload garment photo");
    const file = new File(["img"], "garment.jpg", { type: "image/jpeg" });
    await userEvent.upload(fileInput, file);

    // Review step should appear with trait rows
    await waitFor(() => expect(screen.getByText("Category")).toBeInTheDocument());
    expect(analyzeSpy).toHaveBeenCalledWith({ imageBase64: "abc123", mimeType: "image/jpeg" });

    // Confirm saves the garment
    await userEvent.click(screen.getByLabelText("Confirm and save"));
    await waitFor(() => expect(addSpy).toHaveBeenCalled());
  });

  it("upload flow: analyze failure shows error state", async () => {
    vi.stubGlobal("FileReader", MockFileReader);
    vi.spyOn(client, "analyzeGarment").mockRejectedValue(new Error("model error"));

    wrap(client);
    await waitFor(() => screen.getByLabelText("Add garment"));
    await userEvent.click(screen.getByLabelText("Add garment"));
    await waitFor(() => screen.getByLabelText("Upload garment photo"));

    const fileInput = screen.getByLabelText("Upload garment photo");
    const file = new File(["img"], "garment.jpg", { type: "image/jpeg" });
    await userEvent.upload(fileInput, file);

    await waitFor(() =>
      expect(screen.getByText(/could not analyze/i)).toBeInTheDocument()
    );
  });
});
