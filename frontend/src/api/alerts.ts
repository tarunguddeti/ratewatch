import { apiClient } from "./client";
import type { AlertCondition, AlertRule, EvaluateResult } from "../types/domain";

export const alertsApi = {
  create: (watchlistItemId: string, condition: AlertCondition, threshold: number) =>
    apiClient.post<AlertRule>("/api/alerts", { watchlistItemId, condition, threshold }),
  listByWatchlist: (watchlistId: string) => apiClient.get<AlertRule[]>(`/api/alerts?watchlistId=${watchlistId}`),
  evaluate: (id: string) => apiClient.post<EvaluateResult>(`/api/alerts/${id}/evaluate`),
};
