import { apiClient } from "./client";
import type { RateSnapshot, RefreshSummary } from "../types/domain";

export const ratesApi = {
  refresh: () => apiClient.post<RefreshSummary>("/api/rates/refresh"),
  getLatest: (baseCurrency: string, quoteCurrency: string) =>
    apiClient.get<RateSnapshot>(`/api/rates/latest?base=${baseCurrency}&quote=${quoteCurrency}`),
  getHistory: (baseCurrency: string, quoteCurrency: string, from?: string, to?: string) => {
    const params = new URLSearchParams({ base: baseCurrency, quote: quoteCurrency });
    if (from) params.set("from", from);
    if (to) params.set("to", to);
    return apiClient.get<RateSnapshot[]>(`/api/rates/history?${params.toString()}`);
  },
};
