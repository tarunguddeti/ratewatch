import { useWatchlists } from "../hooks/useWatchlists";
import { WatchlistCard } from "../components/WatchlistCard";
import { CreateWatchlistForm } from "../components/CreateWatchlistForm";
import { RefreshRatesButton } from "../components/RefreshRatesButton";
import styles from "./WatchlistsPage.module.css";

// FR-001/002/003/004/011. Refresh here is the convenience placement - the required one is on
// WatchlistDetailPage (docs/architecture.md's Frontend & UX decisions).
export function WatchlistsPage() {
  const { data, loading, mutating, error, create, remove, refetch, reloadAfterRefresh } = useWatchlists();

  return (
    <main className={styles.page}>
      <div className={styles.header}>
        <h1>Watchlists</h1>
        <RefreshRatesButton onRefreshed={() => void reloadAfterRefresh()} />
      </div>

      <div className={styles.formSection}>
        <CreateWatchlistForm onCreate={create} />
      </div>

      {loading && <p className={styles.loading}>Loading watchlists…</p>}

      {error && (
        <div role="alert" className={styles.errorBox}>
          <p>Couldn't load watchlists: {error.detail ?? error.title}</p>
          <button type="button" className={styles.retryButton} onClick={() => void refetch()}>
            Retry
          </button>
        </div>
      )}

      {!loading && !error && data && data.length === 0 && (
        <p className={styles.emptyState}>No watchlists yet - create one above.</p>
      )}

      {!loading && !error && data && data.length > 0 && (
        <div className={styles.list}>
          {mutating && (
            <p className={styles.loading} aria-live="polite">
              Updating…
            </p>
          )}
          {data.map((w) => (
            <WatchlistCard key={w.id} watchlist={w} onDelete={remove} />
          ))}
        </div>
      )}
    </main>
  );
}
