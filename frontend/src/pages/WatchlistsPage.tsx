import { useWatchlists } from "../hooks/useWatchlists";
import { WatchlistCard } from "../components/WatchlistCard";
import { CreateWatchlistForm } from "../components/CreateWatchlistForm";
import { RefreshRatesButton } from "../components/RefreshRatesButton";

// FR-001/002/003/004/011. Refresh here is the convenience placement - the required one is on
// WatchlistDetailPage (docs/architecture.md's Frontend & UX decisions).
export function WatchlistsPage() {
  const { data, loading, error, create, remove, refetch } = useWatchlists();

  return (
    <main>
      <h1>Watchlists</h1>
      <RefreshRatesButton onRefreshed={() => void refetch()} />
      <CreateWatchlistForm onCreate={create} />

      {loading && <p>Loading watchlists…</p>}

      {error && (
        <div role="alert">
          <p>Couldn't load watchlists: {error.detail ?? error.title}</p>
          <button type="button" onClick={() => void refetch()}>
            Retry
          </button>
        </div>
      )}

      {!loading && !error && data && data.length === 0 && <p>No watchlists yet - create one above.</p>}

      {!loading && !error && data && data.length > 0 && (
        <div>
          {data.map((w) => (
            <WatchlistCard key={w.id} watchlist={w} onDelete={remove} />
          ))}
        </div>
      )}
    </main>
  );
}
