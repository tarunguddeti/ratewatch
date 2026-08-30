import { useState } from "react";
import { ratesApi } from "../api/rates";
import type { ApiError } from "../api/client";
import type { RefreshSummary } from "../types/domain";
import styles from "./RefreshRatesButton.module.css";

interface RefreshRatesButtonProps {
  onRefreshed: (summary: RefreshSummary) => void;
}

// FR-011 - global across all watchlists, placed on both screens with the identical caption
// stating its real scope (docs/architecture.md's Frontend & UX decisions: "Both instances
// carry the same short caption ... so wherever it's clicked from, its real scope is stated
// up front instead of discovered as a surprise").
export function RefreshRatesButton({ onRefreshed }: RefreshRatesButtonProps) {
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  const handleClick = async () => {
    setRefreshing(true);
    setError(null);
    try {
      const summary = await ratesApi.refresh();
      onRefreshed(summary);
    } catch (err) {
      setError(err as ApiError);
    } finally {
      setRefreshing(false);
    }
  };

  return (
    <div className={styles.wrapper}>
      <button
        type="button"
        className={`btn-secondary ${refreshing ? styles.refreshing : ""}`}
        onClick={() => void handleClick()}
        disabled={refreshing}
      >
        {refreshing ? "Refreshing…" : "Refresh Rates"}
      </button>
      <small className={styles.caption}>updates every currency pair across all your watchlists</small>
      {error && (
        <p role="alert" className={styles.error}>
          Refresh failed: {error.detail ?? error.title}
        </p>
      )}
    </div>
  );
}
