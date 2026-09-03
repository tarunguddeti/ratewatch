import { useState, type FormEvent } from "react";
import type { ApiError } from "../api/client";
import { ALERT_CONDITIONS, ALERT_CONDITION_LABELS, type AlertCondition, type WatchlistItemDetail } from "../types/domain";
import styles from "./AlertForm.module.css";

interface AlertFormProps {
  items: WatchlistItemDetail[];
  onCreate: (watchlistItemId: string, condition: AlertCondition, threshold: number) => Promise<void>;
}

// The smallest positive value the backend's 6-decimal-place storage scale (HasPrecision(18, 6))
// can represent - matches the backend's exclusive-of-zero threshold rule at the UI layer
// (specs/004-strong-typing-cleanup).
const MIN_THRESHOLD_INPUT = "0.000001";

// FR-017 - condition (Above/Below/AboveOrEqual/BelowOrEqual, specs/007-inclusive-alert-conditions)
// + a positive threshold. Client-side validation here is a UX shortcut only, to save a round
// trip - the backend re-validates everything regardless (docs/architecture.md:333).
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
    <form onSubmit={(e) => void handleSubmit(e)} className={styles.form}>
      <label className={styles.field}>
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
      <label className={styles.conditionField}>
        Condition
        <select value={condition} onChange={(e) => setCondition(e.target.value as AlertCondition)}>
          {ALERT_CONDITIONS.map((c) => (
            <option key={c} value={c}>
              {ALERT_CONDITION_LABELS[c]}
            </option>
          ))}
        </select>
      </label>
      <label className={styles.thresholdField}>
        Threshold
        <input type="number" step="any" min={MIN_THRESHOLD_INPUT} value={threshold} onChange={(e) => setThreshold(e.target.value)} required />
      </label>
      <button type="submit" className={`btn-primary ${styles.submitButton}`} disabled={submitting || !watchlistItemId || !threshold}>
        {submitting ? "Creating…" : "Create Alert Rule"}
      </button>
      {error && (
        <p role="alert" className={styles.error}>
          {error.detail ?? error.title}
        </p>
      )}
    </form>
  );
}
