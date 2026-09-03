import { useState } from "react";
import type { WatchlistItemDetail } from "../types/domain";
import styles from "./RateTable.module.css";

interface RateTableProps {
  items: WatchlistItemDetail[];
  onRemoveItem: (itemId: string) => Promise<void>;
  /** Alert rule count per item, when known (populated once alerts are loaded - Phase 8 US3
   * wires this in). Undefined for an item means "not known yet," not "zero." */
  alertCountByItemId?: Record<string, number>;
}

// FR-014 ("Not fetched yet" state) and FR-010 (per-row delete, with an alert-aware warning
// once alertCountByItemId is available - docs/architecture.md's Screens & API Calls table
// places this action on the same component that shows rates).
export function RateTable({ items, onRemoveItem, alertCountByItemId }: RateTableProps) {
  // specs/006-fix-ui-loading-bugs FR-004 - busy-tracks only the row being removed (mirrors
  // AlertList's `evaluating` pattern) so a second click on the same row can't fire a duplicate
  // DELETE, while other rows stay independently interactive.
  const [removingItemId, setRemovingItemId] = useState<string | null>(null);

  const handleRemove = async (item: WatchlistItemDetail) => {
    const alertCount = alertCountByItemId?.[item.id] ?? 0;
    const message =
      alertCount > 0
        ? `Remove ${item.baseCurrency}/${item.quoteCurrency}? This also removes ${alertCount} alert rule(s) on this pair.`
        : `Remove ${item.baseCurrency}/${item.quoteCurrency} from this watchlist?`;

    if (!window.confirm(message)) {
      return;
    }
    setRemovingItemId(item.id);
    try {
      await onRemoveItem(item.id);
    } finally {
      setRemovingItemId(null);
    }
  };

  if (items.length === 0) {
    return <p className={styles.emptyState}>No currency pairs tracked yet - add one below.</p>;
  }

  return (
    <div className={styles.tableWrapper}>
      <table className={styles.table}>
        <thead>
          <tr>
            <th>Pair</th>
            <th>Latest Rate</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={item.id}>
              <td>
                {item.baseCurrency}/{item.quoteCurrency}
              </td>
              <td>
                {item.latestRate ? (
                  item.latestRate.rate
                ) : (
                  <span className={styles.notFetched}>Not fetched yet — click Refresh Rates</span>
                )}
              </td>
              <td>
                <button
                  type="button"
                  className={styles.removeButton}
                  onClick={() => void handleRemove(item)}
                  disabled={removingItemId === item.id}
                >
                  {removingItemId === item.id ? "Removing…" : "Remove"}
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
