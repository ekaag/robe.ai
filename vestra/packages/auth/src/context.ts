import { createContext, useContext, createElement } from "react";
import type { ReactNode } from "react";
import type { IAuthProvider } from "./types";

export const AuthContext = createContext<IAuthProvider | null>(null);

export function AuthContextProvider({
  auth,
  children,
}: {
  auth: IAuthProvider;
  children: ReactNode;
}) {
  return createElement(AuthContext.Provider, { value: auth }, children);
}

export function useAuth(): IAuthProvider {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside AuthContextProvider");
  return ctx;
}
