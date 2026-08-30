import { useState } from "react";
import { useParams } from "react-router";
import { useWatchlistDetail } from "../hooks/useWatchlistDetail";
import { useAlerts } from "../hooks/useAlerts";
import { CurrencyPairForm } from "../components/CurrencyPairForm";
import { RateTable } from "../components/RateTable";
import { RefreshRatesButton } from "../components/RefreshRatesButton";
import { RateHistoryChart } from "../components/RateHistoryChart";
import { AlertForm } from "../components/AlertForm";
import { AlertList } from "../components/AlertList";
import type { WatchlistItemDetail } from "../types/domain";
import styles from "./WatchlistDetailPage.module.css";

// FR-003/006 (US1) + FR-011/014/015 (US2) + FR-017/018/020/023 (US3).
export function WatchlistDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { data, loading, error, addItem, removeItem, refetch } = useWatchlistDetail(id!);
  const alerts = useAlerts(id!);
  const [selectedPair, setSelectedPair] = useState<WatchlistItemDetail | null>(null);

  if (loading) {
    return <p className={styles.loading}>Loading watchlist…</p>;
  }

  if (error) {
    if (error.status === 404) {
      return <p className={styles.notFound}>This watchlist wasn't found - it may have been deleted.</p>;
    }
    return (
      <div role="alert" className={styles.errorBox}>
        <p>Couldn't load this watchlist: {error.detail ?? error.title}</p>
        <button type="button" className="btn-secondary" onClick={() => void refetch()}>
          Retry
        </button>
      </div>
    );
  }

  if (!data) {
    return null;
  }

  // Now that alert rules are loaded on this page, RateTable's delete warning can be
  // upgraded from a plain confirmation to one that names the alert-rule count on that
  // specific pair (FR-010) - not available back when RateTable was first built (Phase 8 US2),
  // since alerts hadn't been wired in yet.
  const alertCountByItemId = (alerts.data ?? []).reduce<Record<string, number>>((acc, rule) => {
    acc[rule.watchlistItemId] = (acc[rule.watchlistItemId] ?? 0) + 1;
    return acc;
  }, {});

  return (
    <main className={styles.page}>
      <div className={styles.header}>
        <h1>{data.name}</h1>
        <RefreshRatesButton onRefreshed={() => void refetch()} />
      </div>

      <div className={styles.section}>
        <RateTable items={data.items} onRemoveItem={removeItem} alertCountByItemId={alertCountByItemId} />
      </div>

      <div className={styles.section}>
        <h2>Add Currency Pair</h2>
        <CurrencyPairForm onAdd={addItem} />
      </div>

      {data.items.length > 0 && (
        <div className={styles.section}>
          <h2>Rate History</h2>
          <div className={styles.historyButtons}>
            {data.items.map((item) => (
              <button key={item.id} type="button" className="btn-secondary" onClick={() => setSelectedPair(item)}>
                {item.baseCurrency}/{item.quoteCurrency} history
              </button>
            ))}
          </div>
          {selectedPair && <RateHistoryChart baseCurrency={selectedPair.baseCurrency} quoteCurrency={selectedPair.quoteCurrency} />}
        </div>
      )}

      <section className={styles.section}>
        <h2>Alerts</h2>
        <AlertForm items={data.items} onCreate={alerts.create} />

        {alerts.loading && <p className={styles.loading}>Loading alerts…</p>}
        {alerts.error && (
          <div role="alert" className={styles.errorBox}>
            <p>Couldn't load alerts: {alerts.error.detail ?? alerts.error.title}</p>
            <button type="button" className="btn-secondary" onClick={() => void alerts.refetch()}>
              Retry
            </button>
          </div>
        )}
        {!alerts.loading && !alerts.error && alerts.data && (
          <AlertList rules={alerts.data} items={data.items} onEvaluate={alerts.evaluate} />
        )}
      </section>
    </main>
  );
}
