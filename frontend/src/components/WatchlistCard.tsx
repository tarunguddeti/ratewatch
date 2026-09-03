import { useState } from "react";
import { Link } from "react-router";
import type { Watchlist } from "../types/domain";
import styles from "./WatchlistCard.module.css";

interface WatchlistCardProps {
  watchlist: Watchlist;
  onDelete: (id: string) => Promise<void>;
}

// Click-to-navigate, plus a delete confirmation naming what will be removed -
// itemCount/alertRuleCount on the Watchlist DTO exist specifically so this confirmation can be
// shown here, on the overview page, without an extra call per card.
export function WatchlistCard({ watchlist, onDelete }: WatchlistCardProps) {
  // Busy-tracks this card's own delete request (mirrors RefreshRatesButton's `refreshing`
  // pattern) so a second click can't fire a duplicate DELETE.
  const [deleting, setDeleting] = useState(false);

  const handleDelete = async () => {
    const confirmed = window.confirm(
      `Delete "${watchlist.name}"? This also removes ${watchlist.itemCount} currency pair(s) and ${watchlist.alertRuleCount} alert rule(s).`,
    );
    if (!confirmed) {
      return;
    }
    setDeleting(true);
    try {
      await onDelete(watchlist.id);
    } finally {
      setDeleting(false);
    }
  };

  return (
    <article className={styles.card}>
      <div className={styles.info}>
        <Link to={`/watchlists/${watchlist.id}`} className={styles.link}>
          {watchlist.name}
        </Link>
        <span className={styles.meta}>
          {watchlist.itemCount} pair(s) · {watchlist.alertRuleCount} alert(s)
        </span>
      </div>
      <button type="button" className={styles.deleteButton} onClick={() => void handleDelete()} disabled={deleting}>
        {deleting ? "Deleting…" : "Delete"}
      </button>
    </article>
  );
}
