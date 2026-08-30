import { useState, type FormEvent } from "react";
import type { ApiError } from "../api/client";
import type { AlertCondition, WatchlistItemDetail } from "../types/domain";

interface AlertFormProps {
  items: WatchlistItemDetail[];
  onCreate: (watchlistItemId: string, condition: AlertCondition, threshold: number) => Promise<void>;
}

// FR-017 - condition (Above/Below) + a positive threshold. Client-side validation here is a
// UX shortcut only, to save a round trip - the backend re-validates everything regardless
// (docs/architecture.md:333).
export function AlertForm({ items, onCreate }: AlertFormProps) {
  const [watchlistItemId, setWatchlistItemId] = useState("");
  const [condition, setCondition] = useState<AlertCondition>("Above");
  const [threshold, setThreshold] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      await onCreate(watchlistItemId, condition, Number(threshold));
      setThreshold("");
    } catch (err) {
      setError(err as ApiError);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <form onSubmit={(e) => void handleSubmit(e)}>
      <label>
        Pair
        <select value={watchlistItemId} onChange={(e) => setWatchlistItemId(e.target.value)} required>
          <option value="" disabled>
            Select a pair
          </option>
          {items.map((item) => (
            <option key={item.id} value={item.id}>
              {item.baseCurrency}/{item.quoteCurrency}
            </option>
          ))}
        </select>
      </label>
      <label>
        Condition
        <select value={condition} onChange={(e) => setCondition(e.target.value as AlertCondition)}>
          <option value="Above">Above</option>
          <option value="Below">Below</option>
        </select>
      </label>
      <label>
        Threshold
        <input type="number" step="any" min="0.000001" value={threshold} onChange={(e) => setThreshold(e.target.value)} required />
      </label>
      <button type="submit" disabled={submitting || !watchlistItemId || !threshold}>
        {submitting ? "Creating…" : "Create Alert Rule"}
      </button>
      {error && <p role="alert">{error.detail ?? error.title}</p>}
    </form>
  );
}
