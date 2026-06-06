import { createContext, useContext, createElement } from "react";
import type { ReactNode } from "react";
import type { IApiClient } from "./client";

const ApiClientContext = createContext<IApiClient | null>(null);

export function ApiClientProvider({
  client,
  children,
}: {
  client: IApiClient;
  children: ReactNode;
}) {
  return createElement(ApiClientContext.Provider, { value: client }, children);
}

export function useApiClient(): IApiClient {
  const ctx = useContext(ApiClientContext);
  if (!ctx) throw new Error("useApiClient must be used inside ApiClientProvider");
  return ctx;
}
