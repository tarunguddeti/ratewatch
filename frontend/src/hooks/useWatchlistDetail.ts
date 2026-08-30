import { useCallback, useEffect, useState } from "react";
import { watchlistsApi } from "../api/watchlists";
import type { ApiError } from "../api/client";
import type { WatchlistDetail } from "../types/domain";

export function useWatchlistDetail(id: string) {
  const [data, setData] = useState<WatchlistDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);

  const refetch = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setData(await watchlistsApi.getDetail(id));
    } catch (err) {
      setError(err as ApiError);
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    void refetch();
  }, [refetch]);

  // Mutation errors propagate to the caller (inline field error / non-blocking banner) - see
  // useWatchlists.ts for why this hook's own `error` state stays reserved for the page load.
  const addItem = useCallback(
    async (baseCurrency: string, quoteCurrency: string) => {
      await watchlistsApi.addItem(id, baseCurrency, quoteCurrency);
      await refetch();
    },
    [id, refetch],
  );

  const removeItem = useCallback(
    async (itemId: string) => {
      await watchlistsApi.removeItem(id, itemId);
      await refetch();
    },
    [id, refetch],
  );

  return { data, loading, error, addItem, removeItem, refetch };
}
