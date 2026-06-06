export const tokens = {
  color: {
    bg:       "#ECE4D6",
    bg2:      "#E2D7C4",
    surface:  "#FBF8F1",
    surface2: "#F2ECDF",
    ink:      "#221C15",
    ink2:     "#5A5043",
    muted:    "#988C78",
    line:     "#D8CDBA",
    accent:   "#9C4A2E",
    accent2:  "#C2774E",
    gold:     "#A98C4B",
  },
  font: {
    display: "Fraunces",
    body:    "Hanken Grotesk",
  },
  radius: { sm: 9, md: 14, lg: 18, pill: 100 },
  space:  { xs: 6, sm: 10, md: 14, lg: 22, xl: 32 },
} as const;

export type Tokens = typeof tokens;
export type ColorToken = keyof typeof tokens.color;
export type SpaceToken = keyof typeof tokens.space;
export type RadiusToken = keyof typeof tokens.radius;
