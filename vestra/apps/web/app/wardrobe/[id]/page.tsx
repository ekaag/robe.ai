// Garment IDs are only known at runtime (auth-gated, fetched from the API).
// One dummy path satisfies Next.js static-export's requirement that dynamic
// segments have at least one pre-generated path. SWA's navigationFallback
// serves /index.html for real /wardrobe/<id> deep links; the client router
// handles them via useParams() — the "_" shell is never navigated to in practice.
export function generateStaticParams() { return [{ id: "_" }]; }

import GarmentDetailPage from "./GarmentDetailPage";
export default function Page() {
  return <GarmentDetailPage />;
}
