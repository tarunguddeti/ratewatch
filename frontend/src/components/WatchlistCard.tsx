import { Link } from "react-router";
import type { Watchlist } from "../types/domain";

interface WatchlistCardProps {
  watchlist: Watchlist;
  onDelete: (id: string) => Promise<void>;
}

// FR-003 (select-to-navigate) and FR-004/SC-006 (delete confirmation naming what will be
// removed) - itemCount/alertRuleCount on the Watchlist DTO exist specifically so this
// confirmation can be shown here, on the overview page, without an extra call per card
// (contracts/api-contracts.md).
export function WatchlistCard({ watchlist, onDelete }: WatchlistCardProps) {
  const handleDelete = () => {
    const confirmed = window.confirm(
      `Delete "${watchlist.name}"? This also removes ${watchlist.itemCount} currency pair(s) and ${watchlist.alertRuleCount} alert rule(s).`,
    );
    if (confirmed) {
      void onDelete(watchlist.id);
    }
  };

  return (
    <article>
      <Link to={`/watchlists/${watchlist.id}`}>{watchlist.name}</Link>
      <span>
        {watchlist.itemCount} pair(s) · {watchlist.alertRuleCount} alert(s)
      </span>
      <button type="button" onClick={handleDelete}>
        Delete
      </button>
    </article>
  );
}
