import { useCallback, useEffect, useState } from "react";
import { alertsApi } from "../api/alerts";
import type { ApiError } from "../api/client";
import type { AlertCondition, AlertRule, EvaluateResult } from "../types/domain";

export function useAlerts(watchlistId: string) {
  const [data, setData] = useState<AlertRule[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);

  const refetch = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setData(await alertsApi.listByWatchlist(watchlistId));
    } catch (err) {
      setError(err as ApiError);
    } finally {
      setLoading(false);
    }
  }, [watchlistId]);

  useEffect(() => {
    void refetch();
  }, [refetch]);

  // create/evaluate propagate their own errors to the caller - a create-form failure needs
  // an inline field error, and an evaluate failure renders in that row's own
  // EvaluateResultBanner, not a page-level error (same reasoning as useWatchlists.ts).
  const create = useCallback(
    async (watchlistItemId: string, condition: AlertCondition, threshold: number) => {
      await alertsApi.create(watchlistItemId, condition, threshold);
      await refetch();
    },
    [refetch],
  );

  const evaluate = useCallback(async (id: string): Promise<EvaluateResult> => alertsApi.evaluate(id), []);

  return { data, loading, error, create, evaluate, refetch };
}
