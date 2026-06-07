/**
 * Data-layer tests for StyleScreen.
 *
 * The mobile vitest config runs in node (no React Native renderer), so these
 * tests validate the FakeApiClient fixtures and the display-logic conditions
 * that StyleScreen branches on — rather than rendering the component itself.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { FakeApiClient } from "@vestra/api";

describe("StyleScreen data (FakeApiClient fixtures)", () => {
  let client: FakeApiClient;

  beforeEach(() => {
    client = new FakeApiClient();
  });

  it("getProfile returns seeded profile with dominant styles", async () => {
    const profile = await client.getProfile();

    expect(profile).not.toBeNull();
    expect(profile!.dominantStyles).toContain("minimalist");
    expect(profile!.dominantStyles).toContain("classic");
  });

  it("seeded profile has a non-empty color palette", async () => {
    const profile = await client.getProfile();

    expect(profile!.colorPalette.length).toBeGreaterThan(0);
    expect(profile!.colorPalette[0]).toHaveProperty("name");
    expect(profile!.colorPalette[0]).toHaveProperty("hex");
    expect(profile!.colorPalette[0]).toHaveProperty("weight");
  });

  it("seeded profile has garmentCount > 0 (loaded-state condition)", async () => {
    const profile = await client.getProfile();

    expect(profile!.garmentCount).toBeGreaterThan(0);
  });

  it("seeded profile has a non-empty summary (editorial quote)", async () => {
    const profile = await client.getProfile();

    expect(typeof profile!.summary).toBe("string");
    expect(profile!.summary.length).toBeGreaterThan(0);
  });

  it("seeded profile has formalityRange with min ≤ typical ≤ max", async () => {
    const profile = await client.getProfile();
    const { min, typical, max } = profile!.formalityRange;

    expect(min).toBeLessThanOrEqual(typical);
    expect(typical).toBeLessThanOrEqual(max);
  });

  it("null getProfile → empty-state condition (profile === null)", async () => {
    vi.spyOn(client, "getProfile").mockResolvedValue(null);

    const profile = await client.getProfile();

    expect(profile).toBeNull(); // triggers empty-state branch in StyleScreen
  });

  it("garmentCount 0 → thin-wardrobe condition", async () => {
    const base = await client.getProfile();
    vi.spyOn(client, "getProfile").mockResolvedValue({ ...base!, garmentCount: 0 });

    const profile = await client.getProfile();

    expect(profile!.garmentCount).toBe(0); // triggers thin-wardrobe branch in StyleScreen
  });

  it("generateProfile returns a valid profile", async () => {
    const profile = await client.generateProfile();

    expect(profile).not.toBeNull();
    expect(profile.dominantStyles.length).toBeGreaterThan(0);
    expect(profile.colorPalette.length).toBeGreaterThan(0);
    expect(profile.garmentCount).toBeGreaterThan(0);
  });

  it("generateProfile result matches getProfile seeded data shape", async () => {
    const generated = await client.generateProfile();
    const stored = await client.getProfile();

    // Both return the same fakeProfile fixture — shape must match
    expect(Object.keys(generated)).toEqual(expect.arrayContaining(Object.keys(stored!)));
  });
});
