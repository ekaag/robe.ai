import type {
  GarmentTraits,
  Garment,
  AddGarmentInput,
  GarmentQuery,
  StyleProfile,
  RecommendationContext,
  Recommendation,
  ImageInput,
  InventoryItem,
  ColorWeight,
  StyleTag,
  GarmentFit,
  Season,
  MeUser,
  GarmentCategory,
  BatchImageInput,
  BatchAnalyzeResult,
} from "@vestra/types";

export interface IApiClient {
  getMe(): Promise<MeUser>;
  analyzeGarment(image: ImageInput): Promise<GarmentTraits>;
  analyzeBatch(images: BatchImageInput[]): Promise<BatchAnalyzeResult>;
  addGarment(input: AddGarmentInput): Promise<Garment>;
  listGarments(q?: GarmentQuery): Promise<Garment[]>;
  getGarment(id: string): Promise<Garment>;
  deleteGarment(id: string): Promise<void>;
  generateProfile(): Promise<StyleProfile>;
  getProfile(): Promise<StyleProfile | null>;
  getRecommendations(ctx: RecommendationContext): Promise<Recommendation[]>;
}

export class ApiError extends Error {
  constructor(readonly status: number, message: string) {
    super(message);
    this.name = "ApiError";
  }
}

export class HttpApiClient implements IApiClient {
  constructor(
    private readonly baseUrl: string,
    private readonly getToken: () => Promise<string | null>,
    private readonly onUnauthorized?: () => void
  ) {}

  private async request<T>(method: string, path: string, body?: unknown): Promise<T> {
    const token = await this.getToken();
    const headers: Record<string, string> = {};
    if (body !== undefined) headers["Content-Type"] = "application/json";
    if (token) headers["Authorization"] = `Bearer ${token}`;

    const res = await fetch(`${this.baseUrl}${path}`, {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
      mode: "cors",
      referrerPolicy: "no-referrer",
    });
    if (res.status === 401) {
      this.onUnauthorized?.();
      throw new ApiError(401, "Unauthorized");
    }
    if (!res.ok) throw new ApiError(res.status, await res.text());
    if (res.status === 204) return undefined as T;
    return res.json() as Promise<T>;
  }

  private async requestNullable<T>(method: string, path: string): Promise<T | null> {
    try {
      return await this.request<T>(method, path);
    } catch (e) {
      if (e instanceof ApiError && e.status === 404) return null;
      throw e;
    }
  }

  getMe(): Promise<MeUser> {
    return this.request("GET", "/api/me");
  }

  async analyzeGarment(image: ImageInput): Promise<GarmentTraits> {
    const res = await this.request<{ traits: GarmentTraits; modelVersion: string }>(
      "POST", "/api/garments/analyze", image
    );
    return res.traits;
  }

  analyzeBatch(images: BatchImageInput[]): Promise<BatchAnalyzeResult> {
    return this.request("POST", "/api/garments/analyze-batch", { images });
  }

  addGarment(input: AddGarmentInput): Promise<Garment> {
    return this.request("POST", "/api/garments", input);
  }

  async listGarments(q?: GarmentQuery): Promise<Garment[]> {
    const params = new URLSearchParams();
    if (q?.category) params.set("category", q.category);
    if (q?.page != null) params.set("page", String(q.page));
    if (q?.pageSize != null) params.set("pageSize", String(q.pageSize));
    const qs = params.size > 0 ? `?${params}` : "";
    const res = await this.request<{ items: Garment[] }>("GET", `/api/garments${qs}`);
    return res.items;
  }

  getGarment(id: string): Promise<Garment> {
    return this.request("GET", `/api/garments/${id}`);
  }

  deleteGarment(id: string): Promise<void> {
    return this.request("DELETE", `/api/garments/${id}`);
  }

  generateProfile(): Promise<StyleProfile> {
    return this.request("POST", "/api/users/me/profile/generate");
  }

  getProfile(): Promise<StyleProfile | null> {
    return this.requestNullable("GET", "/api/users/me/profile");
  }

  async getRecommendations(ctx: RecommendationContext): Promise<Recommendation[]> {
    const res = await this.request<{ recommendations: Recommendation[] }>(
      "POST", "/api/users/me/recommendations", ctx
    );
    return res.recommendations;
  }
}

// ---- Fixtures shared by FakeApiClient ----

const fakeTraits: GarmentTraits = {
  category: "top",
  subcategory: "t-shirt",
  primaryColor: { name: "navy", hex: "#1f2a44" },
  secondaryColors: [{ name: "white", hex: "#ffffff" }],
  pattern: "solid",
  material: "cotton",
  fit: "regular",
  formality: 2,
  seasonality: ["spring", "summer"],
  styleTags: ["minimalist", "casual"],
  occasions: ["everyday", "weekend"],
  confidence: 0.92,
};

const fakeGarment: Garment = {
  id: "grm_001",
  userId: "usr_fake",
  traits: fakeTraits,
  imageUrl: "https://placehold.co/300x400/ECE4D6/221C15?text=Garment",
  createdAt: "2026-01-01T00:00:00Z",
  modifiedAt: "2026-01-01T00:00:00Z",
  createdByUserId: "usr_fake",
  modifiedByUserId: "usr_fake",
};

const fakeGarment2: Garment = {
  id: "grm_002",
  userId: "usr_fake",
  traits: {
    category: "bottom",
    subcategory: "jeans",
    primaryColor: { name: "indigo", hex: "#3b3f6e" },
    secondaryColors: [],
    pattern: "solid",
    material: "denim",
    fit: "slim",
    formality: 1,
    seasonality: ["spring", "summer", "fall", "winter"],
    styleTags: ["casual", "classic"],
    occasions: ["everyday", "weekend"],
    confidence: 0.88,
  },
  imageUrl: "https://placehold.co/300x400/E2D7C4/221C15?text=Jeans",
  createdAt: "2026-01-02T00:00:00Z",
  modifiedAt: "2026-01-02T00:00:00Z",
  createdByUserId: "usr_fake",
  modifiedByUserId: "usr_fake",
};

const fakeGarment3: Garment = {
  id: "grm_003",
  userId: "usr_fake",
  traits: {
    category: "outerwear",
    subcategory: "jacket",
    primaryColor: { name: "tan", hex: "#c4a882" },
    secondaryColors: [],
    pattern: "solid",
    material: "wool",
    fit: "regular",
    formality: 3,
    seasonality: ["fall", "winter"],
    styleTags: ["classic", "minimalist"],
    occasions: ["everyday", "work"],
    confidence: 0.85,
  },
  imageUrl: "https://placehold.co/300x400/FBF8F1/221C15?text=Jacket",
  createdAt: "2026-01-03T00:00:00Z",
  modifiedAt: "2026-01-03T00:00:00Z",
  createdByUserId: "usr_fake",
  modifiedByUserId: "usr_fake",
};

const fakeGarments: Garment[] = [fakeGarment, fakeGarment2, fakeGarment3];

const fakeProfile: StyleProfile = {
  dominantStyles: ["minimalist", "classic"] as StyleTag[],
  colorPalette: [
    { name: "navy", hex: "#1f2a44", weight: 0.4 } as ColorWeight,
    { name: "white", hex: "#ffffff", weight: 0.3 } as ColorWeight,
  ],
  formalityRange: { min: 1, max: 3, typical: 2 },
  preferredFits: ["regular"] as GarmentFit[],
  seasonalSkew: { spring: 0.3, summer: 0.4, fall: 0.2, winter: 0.1 } as Partial<Record<Season, number>>,
  summary: "Leans minimalist and neutral, favors regular-fit everyday pieces.",
  garmentCount: 5,
  createdAt: "2026-01-01T00:00:00Z",
  modifiedAt: "2026-01-01T00:00:00Z",
  createdByUserId: "usr_fake",
  modifiedByUserId: "usr_fake",
};

const fakeInventoryItems: InventoryItem[] = [
  {
    id: "inv_001",
    vendorId: "demo-store",
    name: "Classic Navy Tee",
    description: "A timeless cotton crew-neck in deep navy.",
    traits: fakeTraits,
    price: 49.0,
    currency: "USD",
    url: "https://example.com/item/001",
    imageUrl: "https://placehold.co/300x400/E2D7C4/221C15?text=Item+1",
    inStock: true,
  },
  {
    id: "inv_002",
    vendorId: "demo-store",
    name: "Slim Chinos",
    description: "Versatile slim-fit chinos in khaki.",
    traits: {
      ...fakeTraits,
      category: "bottom" as const,
      subcategory: "chinos",
      primaryColor: { name: "khaki", hex: "#c3b091" },
      fit: "slim",
    },
    price: 65.0,
    currency: "USD",
    url: "https://example.com/item/002",
    imageUrl: "https://placehold.co/300x400/F2ECDF/221C15?text=Item+2",
    inStock: true,
  },
  {
    id: "inv_003",
    vendorId: "demo-store",
    name: "Wool Blend Blazer",
    description: "A refined wool-blend blazer for layered looks.",
    traits: {
      ...fakeTraits,
      category: "outerwear" as const,
      subcategory: "blazer",
      primaryColor: { name: "charcoal", hex: "#36454f" },
      material: "wool blend",
      formality: 4,
      styleTags: ["classic", "formal"] as StyleTag[],
    },
    price: 189.0,
    currency: "USD",
    url: "https://example.com/item/003",
    imageUrl: "https://placehold.co/300x400/FBF8F1/221C15?text=Item+3",
    inStock: true,
  },
];

const fakeRecommendations: Recommendation[] = [
  {
    inventoryItem: fakeInventoryItems[0],
    score: 0.92,
    reasoning: "Matches your minimalist, neutral palette; regular fit.",
  },
  {
    inventoryItem: fakeInventoryItems[1],
    score: 0.85,
    reasoning: "Complements your casual-classic wardrobe; slim fit you prefer.",
  },
  {
    inventoryItem: fakeInventoryItems[2],
    score: 0.71,
    reasoning: "A step up in formality that rounds out your wardrobe.",
  },
];

export class FakeApiClient implements IApiClient {
  async getMe(): Promise<MeUser> {
    return { userId: "usr_fake", name: "Dev User", provider: "google" };
  }

  async analyzeGarment(_image: ImageInput): Promise<GarmentTraits> {
    return fakeTraits;
  }

  async analyzeBatch(images: BatchImageInput[]): Promise<BatchAnalyzeResult> {
    return {
      images: images.map((img, i) => ({
        imageId: img.imageId ?? `img-${i + 1}`,
        people: [
          {
            personId: "person-1",
            position: "center",
            overallStyle: ["minimalist"],
            styleTags: ["casual"],
            clothingItems: [
              {
                category: "top",
                type: "t-shirt",
                subtype: null,
                primaryColor: { normalized: "navy", shade: "dark" },
                secondaryColors: [],
                pattern: "solid",
                material: "cotton",
                fit: "regular",
                length: null,
                sleeveLength: "short",
                neckline: "crew",
                collarType: null,
                waistRise: null,
                closureType: null,
                details: [],
                visibleText: null,
                brand: null,
                logo: null,
                condition: "good",
                styleTags: ["minimalist", "casual"],
                confidence: 0.9,
              },
            ],
            overallConfidence: 0.9,
          },
        ],
        warnings: [],
      })),
      modelVersion: "fake-model",
    };
  }

  async addGarment(input: AddGarmentInput): Promise<Garment> {
    const now = new Date().toISOString();
    return { ...fakeGarment, traits: input.traits, id: `grm_${Date.now()}`, createdAt: now, modifiedAt: now };
  }
  async listGarments(q?: GarmentQuery): Promise<Garment[]> {
    if (q?.category) return fakeGarments.filter((g) => g.traits.category === q.category);
    return fakeGarments;
  }
  async getGarment(id: string): Promise<Garment> {
    const found = fakeGarments.find((g) => g.id === id);
    if (!found) throw new ApiError(404, `Garment ${id} not found`);
    return found;
  }
  async deleteGarment(_id: string): Promise<void> {}
  async generateProfile(): Promise<StyleProfile> {
    return fakeProfile;
  }
  async getProfile(): Promise<StyleProfile | null> {
    return fakeProfile;
  }
  async getRecommendations(ctx: RecommendationContext): Promise<Recommendation[]> {
    let results = fakeRecommendations;
    if (ctx.maxBudget != null) {
      results = results.filter((r) => r.inventoryItem.price <= ctx.maxBudget!);
    }
    if (ctx.categories && ctx.categories.length > 0) {
      results = results.filter((r) => ctx.categories!.includes(r.inventoryItem.traits.category));
    }
    return results;
  }
}
