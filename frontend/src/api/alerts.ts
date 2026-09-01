import { apiClient } from "./client";
import type { AlertCondition, AlertRule, EvaluateResult } from "../types/domain";

// The single source every path below is built from, instead of each independently restating
// "/api/alerts" (specs/004-strong-typing-cleanup/research.md decision 7).
const ALERTS_BASE = "/api/alerts";

export const alertsApi = {
  create: (watchlistItemId: string, condition: AlertCondition, threshold: number) =>
    apiClient.post<AlertRule>(ALERTS_BASE, { watchlistItemId, condition, threshold }),
  listByWatchlist: (watchlistId: string) => apiClient.get<AlertRule[]>(`${ALERTS_BASE}?watchlistId=${watchlistId}`),
  evaluate: (id: string) => apiClient.post<EvaluateResult>(`${ALERTS_BASE}/${id}/evaluate`),
};
