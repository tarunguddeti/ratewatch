import { apiClient } from "./client";
import type { RateSnapshot, RefreshSummary } from "../types/domain";

// The single source every path below is built from, instead of each independently restating
// "/api/rates" (specs/004-strong-typing-cleanup/research.md decision 7).
const RATES_BASE = "/api/rates";

export const ratesApi = {
  refresh: () => apiClient.post<RefreshSummary>(`${RATES_BASE}/refresh`),
  getLatest: (baseCurrency: string, quoteCurrency: string) =>
    apiClient.get<RateSnapshot>(`${RATES_BASE}/latest?base=${baseCurrency}&quote=${quoteCurrency}`),
  getHistory: (baseCurrency: string, quoteCurrency: string, from?: string, to?: string) => {
    const params = new URLSearchParams({ base: baseCurrency, quote: quoteCurrency });
    if (from) params.set("from", from);
    if (to) params.set("to", to);
    return apiClient.get<RateSnapshot[]>(`${RATES_BASE}/history?${params.toString()}`);
  },
};
