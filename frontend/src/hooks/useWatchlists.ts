import { useCallback, useEffect, useState } from "react";
import { watchlistsApi } from "../api/watchlists";
import type { ApiError } from "../api/client";
import type { Watchlist } from "../types/domain";

export function useWatchlists() {
  const [data, setData] = useState<Watchlist[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [mutating, setMutating] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  // `showLoading` is false for reloads triggered by create/remove, so an in-flight mutation
  // never blanks the already-rendered list back to the full-page loading state - only the very
  // first mount fetch (and an explicit retry of it) uses the page-level `loading` flag.
  const load = useCallback(async (showLoading: boolean) => {
    if (showLoading) setLoading(true);
    setError(null);
    try {
      setData(await watchlistsApi.list());
    } catch (err) {
      setError(err as ApiError);
    } finally {
      if (showLoading) setLoading(false);
    }
  }, []);

  const refetch = useCallback(() => load(true), [load]);

  useEffect(() => {
    void refetch();
  }, [refetch]);

  // create/remove intentionally don't catch into this hook's shared `error` state - a
  // create-form failure needs an inline field error, and a delete failure needs a
  // non-blocking banner, not a page-level error replacing the whole list
  // (docs/architecture.md's three UI error treatments). Callers catch these themselves.
  //
  // `mutating` tracks these in-flight calls (including their trailing reload) instead of
  // `loading`, so the already-rendered list stays visible throughout.
  const create = useCallback(
    async (name: string) => {
      setMutating(true);
      try {
        await watchlistsApi.create(name);
        await load(false);
      } finally {
        setMutating(false);
      }
    },
    [load],
  );

  const remove = useCallback(
    async (id: string) => {
      setMutating(true);
      try {
        await watchlistsApi.remove(id);
        await load(false);
      } finally {
        setMutating(false);
      }
    },
    [load],
  );

  // Used after a global rate refresh (RefreshRatesButton) to reload the list without blanking
  // it back to the full-page loading state - same reasoning as create/remove above.
  const reloadAfterRefresh = useCallback(async () => {
    setMutating(true);
    try {
      await load(false);
    } finally {
      setMutating(false);
    }
  }, [load]);

  return { data, loading, mutating, error, create, remove, refetch, reloadAfterRefresh };
}
