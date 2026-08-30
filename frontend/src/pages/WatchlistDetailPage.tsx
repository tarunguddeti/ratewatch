import { useParams } from "react-router";
import { useWatchlistDetail } from "../hooks/useWatchlistDetail";
import { CurrencyPairForm } from "../components/CurrencyPairForm";

// FR-003 (view detail + 404 handling) and FR-006 (add pair). US1 skeleton - RateTable
// (Phase 8 US2) replaces the plain item list below, and the Alerts section (Phase 8 US3)
// gets added once those components exist.
export function WatchlistDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { data, loading, error, addItem, removeItem, refetch } = useWatchlistDetail(id!);

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

  const handleRemoveItem = async (itemId: string) => {
    await removeItem(itemId);
  };

  return (
    <main>
      <h1>{data.name}</h1>

      <ul>
        {data.items.map((item) => (
          <li key={item.id}>
            {item.baseCurrency}/{item.quoteCurrency} —{" "}
            {item.latestRate ? item.latestRate.rate : "Not fetched yet — click Refresh Rates"}
            <button type="button" onClick={() => void handleRemoveItem(item.id)}>
              Remove
            </button>
          </li>
        ))}
      </ul>

      <CurrencyPairForm onAdd={addItem} />
    </main>
  );
}
