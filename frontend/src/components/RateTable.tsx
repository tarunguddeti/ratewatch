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
  const handleRemove = (item: WatchlistItemDetail) => {
    const alertCount = alertCountByItemId?.[item.id] ?? 0;
    const message =
      alertCount > 0
        ? `Remove ${item.baseCurrency}/${item.quoteCurrency}? This also removes ${alertCount} alert rule(s) on this pair.`
        : `Remove ${item.baseCurrency}/${item.quoteCurrency} from this watchlist?`;

    if (window.confirm(message)) {
      void onRemoveItem(item.id);
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
                <button type="button" className={styles.removeButton} onClick={() => handleRemove(item)}>
                  Remove
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
