import { useState } from "react";
import { useParams } from "react-router";
import { useWatchlistDetail } from "../hooks/useWatchlistDetail";
import { CurrencyPairForm } from "../components/CurrencyPairForm";
import { RateTable } from "../components/RateTable";
import { RefreshRatesButton } from "../components/RefreshRatesButton";
import { RateHistoryChart } from "../components/RateHistoryChart";
import type { WatchlistItemDetail } from "../types/domain";

// FR-003/006 (US1) + FR-011/014/015 (US2). Alerts section (US3) lands in Phase 8's next
// slice, including upgrading RateTable's delete warning to be alert-count-aware.
export function WatchlistDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { data, loading, error, addItem, removeItem, refetch } = useWatchlistDetail(id!);
  const [selectedPair, setSelectedPair] = useState<WatchlistItemDetail | null>(null);

  if (loading) {
    return <p>Loading watchlist…</p>;
  }

  if (error) {
    if (error.status === 404) {
      return <p>This watchlist wasn't found - it may have been deleted.</p>;
    }
    return (
      <div role="alert">
        <p>Couldn't load this watchlist: {error.detail ?? error.title}</p>
        <button type="button" onClick={() => void refetch()}>
          Retry
        </button>
      </div>
    );
  }

  if (!data) {
    return null;
  }

  return (
    <main>
      <h1>{data.name}</h1>

      <RefreshRatesButton onRefreshed={() => void refetch()} />

      <RateTable items={data.items} onRemoveItem={removeItem} />

      <CurrencyPairForm onAdd={addItem} />

      <div>
        {data.items.map((item) => (
          <button key={item.id} type="button" onClick={() => setSelectedPair(item)}>
            {item.baseCurrency}/{item.quoteCurrency} history
          </button>
        ))}
      </div>

      {selectedPair && <RateHistoryChart baseCurrency={selectedPair.baseCurrency} quoteCurrency={selectedPair.quoteCurrency} />}
    </main>
  );
}
