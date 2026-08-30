import { useCallback, useEffect, useState } from "react";
import { watchlistsApi } from "../api/watchlists";
import type { ApiError } from "../api/client";
import type { Watchlist } from "../types/domain";

export function useWatchlists() {
  const [data, setData] = useState<Watchlist[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);

  const refetch = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setData(await watchlistsApi.list());
    } catch (err) {
      setError(err as ApiError);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void refetch();
  }, [refetch]);

  // create/remove intentionally don't catch into this hook's shared `error` state - a
  // create-form failure needs an inline field error, and a delete failure needs a
  // non-blocking banner, not a page-level error replacing the whole list
  // (docs/architecture.md's three UI error treatments). Callers catch these themselves.
  const create = useCallback(
    async (name: string) => {
      await watchlistsApi.create(name);
      await refetch();
    },
    [refetch],
  );

  const remove = useCallback(
    async (id: string) => {
      await watchlistsApi.remove(id);
      await refetch();
    },
    [refetch],
  );

  return { data, loading, error, create, remove, refetch };
}
