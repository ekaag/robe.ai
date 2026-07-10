// Server component — owns generateStaticParams for static export.
// Garment IDs are only known at runtime (fetched from the API), so no paths
// are pre-generated. SWA's navigationFallback serves root HTML for deep links;
// the client router then renders GarmentDetailPage with useParams().
export function generateStaticParams() { return []; }
export const dynamicParams = false;

import GarmentDetailPage from "./GarmentDetailPage";
export default function Page() {
  return <GarmentDetailPage />;
}
