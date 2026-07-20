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
  modifiedAt: string;
  createdByUserId: string;
  modifiedByUserId: string;
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
  createdAt: string;
  modifiedAt: string;
  createdByUserId: string;
  modifiedByUserId: string;
}

// ---- API #4: Recommendation matching ----

export interface InventoryItem {
  id: string;
  vendorId: string;
  name: string;
  description: string;
  traits: GarmentTraits;
  price: number;
  currency: string;
  url: string;
  imageUrl: string;
  inStock: boolean;
}

export interface RecommendationContext {
  maxBudget?: number;
  currency?: string;
  categories?: GarmentCategory[];
  occasion?: string;
  count?: number;
}

export interface Recommendation {
  inventoryItem: InventoryItem;
  score: number;    // 0–1 fit score
  reasoning: string;
}

// ---- Auth / Me ----

export interface MeUser {
  userId: string;
  name?: string;
  provider: string;
}

// ---- API #1 Batch: analyze multiple images ----

export interface BatchImageInput {
  imageId?: string;
  imageBase64: string;
  mimeType: string;
}

export interface ColorTraitResult {
  normalized: string;
  shade?: string | null;
}

// Normalized (0-1, top-left origin) box, resolution-independent — scale by the
// rendered image's displayed width/height to draw it.
export interface BoundingBox {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface ClothingItemTraitsResult {
  category: string;
  type: string;
  subtype?: string | null;
  primaryColor?: ColorTraitResult | null;
  secondaryColors: ColorTraitResult[];
  pattern?: string | null;
  material?: string | null;
  fit?: string | null;
  length?: string | null;
  sleeveLength?: string | null;
  neckline?: string | null;
  collarType?: string | null;
  waistRise?: string | null;
  closureType?: string | null;
  details: string[];
  visibleText?: string | null;
  brand?: string | null;
  logo?: string | null;
  condition?: string | null;
  styleTags: string[];
  confidence: number;
  boundingBox?: BoundingBox | null;
}

export interface PersonTraitsResult {
  personId: string;
  position?: string | null;
  overallStyle: string[];
  styleTags: string[];
  clothingItems: ClothingItemTraitsResult[];
  overallConfidence: number;
  faceBoundingBox?: BoundingBox | null;
}

export interface ImageTraitsResult {
  imageId: string;
  people: PersonTraitsResult[];
  warnings: string[];
}

export interface BatchAnalyzeResult {
  images: ImageTraitsResult[];
  modelVersion: string;
}
