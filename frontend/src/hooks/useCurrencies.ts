import { useCallback, useEffect, useState } from "react";
import { currenciesApi } from "../api/currencies";
import type { ApiError } from "../api/client";
import type { Currency } from "../types/domain";

// Fetch once, module-level cache - no React Query needed for one static list. Shared across
// every CurrencySelect on the page.
let cache: Currency[] | null = null;
let inFlight: Promise<Currency[]> | null = null;

export function useCurrencies() {
  const [data, setData] = useState<Currency[] | null>(cache);
  const [loading, setLoading] = useState(cache === null);
  const [error, setError] = useState<ApiError | null>(null);

  const load = useCallback(async () => {
    if (cache) {
      setData(cache);
      setLoading(false);
      return;
    }

    setLoading(true);
    setError(null);
    try {
      inFlight ??= currenciesApi.list();
      const result = await inFlight;
      cache = result;
      setData(result);
    } catch (err) {
      setError(err as ApiError);
    } finally {
      inFlight = null;
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  // Exposed so CurrencySelect's error state can offer a real Retry, not just a dead banner.
  const retry = useCallback(() => {
    cache = null;
    return load();
  }, [load]);

  return { data, loading, error, retry };
}
