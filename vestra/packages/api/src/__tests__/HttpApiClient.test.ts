import { describe, it, expect, vi, beforeEach } from "vitest";
import { HttpApiClient, FakeApiClient, ApiError } from "../client";

// Minimal fetch response helper
function mockResponse(status: number, body: unknown) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
    text: async () => String(body),
  } as Response;
}

describe("HttpApiClient", () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    fetchMock = vi.fn();
    global.fetch = fetchMock;
  });

  it("attaches Authorization header when token is present", async () => {
    fetchMock.mockResolvedValueOnce(mockResponse(200, []));
    const client = new HttpApiClient("http://api", async () => "my-token");
    await client.listGarments();
    expect(fetchMock).toHaveBeenCalledWith(
      "http://api/api/garments",
      expect.objectContaining({
        headers: expect.objectContaining({ Authorization: "Bearer my-token" }),
      })
    );
  });

  it("omits Authorization header when getToken returns null", async () => {
    fetchMock.mockResolvedValueOnce(mockResponse(200, []));
    const client = new HttpApiClient("http://api", async () => null);
    await client.listGarments();
    const headers = (fetchMock.mock.calls[0][1] as RequestInit).headers as Record<string, string>;
    expect(headers["Authorization"]).toBeUndefined();
  });

  it("calls onUnauthorized and throws ApiError(401) on 401 response", async () => {
    fetchMock.mockResolvedValueOnce(mockResponse(401, "Unauthorized"));
    const onUnauthorized = vi.fn();
    const client = new HttpApiClient("http://api", async () => "token", onUnauthorized);
    const err = await client.listGarments().catch((e) => e);
    expect(err).toBeInstanceOf(ApiError);
    expect(err.status).toBe(401);
    expect(onUnauthorized).toHaveBeenCalledOnce();
  });

  it("throws ApiError on non-401 error response without calling onUnauthorized", async () => {
    fetchMock.mockResolvedValueOnce(mockResponse(500, "Server Error"));
    const onUnauthorized = vi.fn();
    const client = new HttpApiClient("http://api", async () => "token", onUnauthorized);
    await expect(client.listGarments()).rejects.toThrow(ApiError);
    expect(onUnauthorized).not.toHaveBeenCalled();
  });
});

describe("FakeApiClient", () => {
  const client = new FakeApiClient();

  it("getMe returns fake user", async () => {
    const me = await client.getMe();
    expect(me.userId).toBe("usr_fake");
    expect(me.provider).toBe("google");
  });

  it("listGarments returns all seeded fixtures", async () => {
    const garments = await client.listGarments();
    expect(garments).toHaveLength(3);
    expect(garments[0].id).toBe("grm_001");
  });

  it("listGarments filters by category", async () => {
    const tops = await client.listGarments({ category: "top" });
    expect(tops).toHaveLength(1);
    expect(tops[0].traits.category).toBe("top");

    const bottoms = await client.listGarments({ category: "bottom" });
    expect(bottoms).toHaveLength(1);
    expect(bottoms[0].traits.category).toBe("bottom");
  });

  it("getGarment returns correct garment by id", async () => {
    const g = await client.getGarment("grm_002");
    expect(g.id).toBe("grm_002");
    expect(g.traits.category).toBe("bottom");
  });

  it("getGarment throws 404 for unknown id", async () => {
    await expect(client.getGarment("grm_unknown")).rejects.toMatchObject({ status: 404 });
  });

  it("getProfile returns seeded style profile", async () => {
    const profile = await client.getProfile();
    expect(profile).not.toBeNull();
    expect(profile?.dominantStyles).toContain("minimalist");
  });

  it("getRecommendations returns seeded recommendations", async () => {
    const recs = await client.getRecommendations({});
    expect(recs.length).toBeGreaterThan(0);
    expect(recs[0].score).toBeGreaterThan(0);
  });
});
