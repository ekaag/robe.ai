import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import type { AddGarmentInput, GarmentQuery, RecommendationContext, ImageInput } from "@vestra/types";
import type { IApiClient } from "./client";
import { useApiClient } from "./context";

export function createApiHooks(api: IApiClient) {
  function useGarments(q?: GarmentQuery) {
    return useQuery({
      queryKey: ["garments", q],
      queryFn: () => api.listGarments(q),
    });
  }

  function useGarment(id: string) {
    return useQuery({
      queryKey: ["garments", id],
      queryFn: () => api.getGarment(id),
      enabled: !!id,
    });
  }

  function useAddGarment() {
    const qc = useQueryClient();
    return useMutation({
      mutationFn: (input: AddGarmentInput) => api.addGarment(input),
      onSuccess: () => qc.invalidateQueries({ queryKey: ["garments"] }),
    });
  }

  function useDeleteGarment() {
    const qc = useQueryClient();
    return useMutation({
      mutationFn: (id: string) => api.deleteGarment(id),
      onSuccess: () => qc.invalidateQueries({ queryKey: ["garments"] }),
    });
  }

  function useStyleProfile() {
    return useQuery({
      queryKey: ["profile"],
      queryFn: () => api.getProfile(),
    });
  }

  function useGenerateProfile() {
    const qc = useQueryClient();
    return useMutation({
      mutationFn: () => api.generateProfile(),
      onSuccess: (data) => qc.setQueryData(["profile"], data),
    });
  }

  function useRecommendations(ctx: RecommendationContext) {
    return useQuery({
      queryKey: ["recommendations", ctx],
      queryFn: () => api.getRecommendations(ctx),
    });
  }

  return {
    useGarments,
    useGarment,
    useAddGarment,
    useDeleteGarment,
    useStyleProfile,
    useGenerateProfile,
    useRecommendations,
  };
}

export type ApiHooks = ReturnType<typeof createApiHooks>;

// ---- Standalone hooks (components use these; context provides the api client) ----

export function useGarments(q?: GarmentQuery) {
  const api = useApiClient();
  return useQuery({ queryKey: ["garments", q], queryFn: () => api.listGarments(q) });
}

export function useGarment(id: string) {
  const api = useApiClient();
  return useQuery({ queryKey: ["garments", id], queryFn: () => api.getGarment(id), enabled: !!id });
}

export function useAddGarment() {
  const api = useApiClient();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: AddGarmentInput) => api.addGarment(input),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["garments"] }),
  });
}

export function useDeleteGarment() {
  const api = useApiClient();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.deleteGarment(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["garments"] }),
  });
}

export function useAnalyzeGarment() {
  const api = useApiClient();
  return useMutation({ mutationFn: (image: ImageInput) => api.analyzeGarment(image) });
}

export function useStyleProfile() {
  const api = useApiClient();
  return useQuery({ queryKey: ["profile"], queryFn: () => api.getProfile() });
}

export function useGenerateProfile() {
  const api = useApiClient();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api.generateProfile(),
    onSuccess: (data) => qc.setQueryData(["profile"], data),
  });
}

export function useRecommendations(ctx: RecommendationContext, enabled = true) {
  const api = useApiClient();
  return useQuery({ queryKey: ["recommendations", ctx], queryFn: () => api.getRecommendations(ctx), enabled });
}
