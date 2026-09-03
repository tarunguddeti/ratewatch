import { useCallback, useEffect, useState } from "react";
import { watchlistsApi } from "../api/watchlists";
import type { ApiError } from "../api/client";
import type { WatchlistDetail } from "../types/domain";

export function useWatchlistDetail(id: string) {
  const [data, setData] = useState<WatchlistDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [mutating, setMutating] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  // `showLoading` is false for reloads triggered by add/remove/refresh, so an in-flight
  // mutation never blanks the already-rendered table/chart/alerts back to the full-page
  // loading state - only the very first mount fetch (and an explicit retry of it) uses the
  // page-level `loading` flag.
  const load = useCallback(
    async (showLoading: boolean) => {
      if (showLoading) setLoading(true);
      setError(null);
      try {
        setData(await watchlistsApi.getDetail(id));
      } catch (err) {
        setError(err as ApiError);
      } finally {
        if (showLoading) setLoading(false);
      }
    },
    [id],
  );

  const refetch = useCallback(() => load(true), [load]);

  useEffect(() => {
    void refetch();
  }, [refetch]);

  // Mutation errors propagate to the caller (inline field error / non-blocking banner) - see
  // useWatchlists.ts for why this hook's own `error` state stays reserved for the page load.
  //
  // `mutating` tracks these in-flight calls (including their trailing reload) instead of
  // `loading`, so the already-rendered table/chart/alerts stay visible throughout.
  const addItem = useCallback(
    async (baseCurrency: string, quoteCurrency: string) => {
      setMutating(true);
      try {
        await watchlistsApi.addItem(id, baseCurrency, quoteCurrency);
        await load(false);
      } finally {
        setMutating(false);
      }
    },
    [id, load],
  );

  const removeItem = useCallback(
    async (itemId: string) => {
      setMutating(true);
      try {
        await watchlistsApi.removeItem(id, itemId);
        await load(false);
      } finally {
        setMutating(false);
      }
    },
    [id, load],
  );

  // Used after a global rate refresh (RefreshRatesButton) to reload this watchlist's rates
  // without blanking the page back to the full-page loading state.
  const reloadAfterRefresh = useCallback(async () => {
    setMutating(true);
    try {
      await load(false);
    } finally {
      setMutating(false);
    }
  }, [load]);

  return { data, loading, mutating, error, addItem, removeItem, refetch, reloadAfterRefresh };
}
