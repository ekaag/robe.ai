// ---- Shared primitives ----

export type GarmentCategory =
  | "top" | "bottom" | "dress" | "outerwear"
  | "footwear" | "accessory" | "other";

export type GarmentPattern =
  | "solid" | "striped" | "plaid" | "checked"
  | "floral" | "graphic" | "other";

export type GarmentFit = "slim" | "regular" | "loose" | "oversized";

export type Season = "spring" | "summer" | "fall" | "winter" | "all";

export type StyleTag =
  | "minimalist" | "classic" | "casual" | "streetwear"
  | "formal" | "bohemian" | "sporty" | "vintage"
  | "preppy" | "edgy" | "romantic";

export type Occasion =
  | "everyday" | "work" | "formal" | "sport" | "event" | "weekend";

export interface ColorEntry {
  name: string;
  hex: string;
}

// ---- GarmentTraits — source of truth for all downstream APIs ----

export interface GarmentTraits {
  category: GarmentCategory;
  subcategory: string;
  primaryColor: ColorEntry;
  secondaryColors: ColorEntry[];
  pattern: GarmentPattern;
  material: string | null;
  fit: GarmentFit | null;
  formality: number;        // 1 (very casual) – 5 (formal)
  seasonality: Season[];
  styleTags: StyleTag[];
  occasions: Occasion[];
  confidence: number;       // 0–1 overall extraction confidence
}

export interface ImageInput {
  imageBase64: string;
  mimeType: string;
}

// ---- API #2: Storage + auth ----

export interface Garment {
  id: string;
  userId: string;
  traits: GarmentTraits;
  imageUrl: string;
  createdAt: string;
}

export interface AddGarmentInput extends ImageInput {
  traits: GarmentTraits;
}

export interface GarmentQuery {
  category?: GarmentCategory;
  page?: number;
  pageSize?: number;
}

// ---- API #3: Style profile ----

export interface ColorWeight extends ColorEntry {
  weight: number;
}

export interface FormalityRange {
  min: number;
  max: number;
  typical: number;
}

export interface StyleProfile {
  dominantStyles: StyleTag[];
  colorPalette: ColorWeight[];
  formalityRange: FormalityRange;
  preferredFits: GarmentFit[];
  seasonalSkew: Partial<Record<Season, number>>;
  summary: string;
  garmentCount: number;
  generatedAt: string;
}

// ---- API #4: Recommendation matching ----

export interface Price {
  amount: number;
  currency: string;
}

export interface InventoryItem {
  id: string;
  merchant: string;
  traits: GarmentTraits;
  price: Price;
  productUrl: string;
  imageUrl: string;
}

export interface RecommendationContext {
  budgetMax?: number;
  categories?: GarmentCategory[];
  excludeOwned?: boolean;
}

export interface Recommendation {
  item: InventoryItem;
  score: number;    // 0–1 fit score
  reason: string;
}

// ---- Auth / Me ----

export interface MeUser {
  userId: string;
  name?: string;
  provider: string;
}
