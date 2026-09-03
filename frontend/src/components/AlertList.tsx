import { useState } from "react";
import { EvaluateResultBanner } from "./EvaluateResultBanner";
import type { ApiError } from "../api/client";
import { ALERT_CONDITION_LABELS, type AlertRule, type EvaluateResult, type WatchlistItemDetail } from "../types/domain";
import styles from "./AlertList.module.css";

interface AlertListProps {
  rules: AlertRule[];
  items: WatchlistItemDetail[];
  onEvaluate: (id: string) => Promise<EvaluateResult>;
}

// FR-023 (list) + FR-020 ("Evaluate Now" per row, result rendered for that row only).
export function AlertList({ rules, items, onEvaluate }: AlertListProps) {
  const [evaluating, setEvaluating] = useState<string | null>(null);
  const [results, setResults] = useState<Record<string, EvaluateResult>>({});
  const [errors, setErrors] = useState<Record<string, ApiError>>({});

  const pairLabel = (watchlistItemId: string) => {
    const item = items.find((i) => i.id === watchlistItemId);
    return item ? `${item.baseCurrency}/${item.quoteCurrency}` : "Unknown pair";
  };

  const handleEvaluate = async (ruleId: string) => {
    setEvaluating(ruleId);
    setErrors((prev) => ({ ...prev, [ruleId]: undefined as unknown as ApiError }));
    try {
      const result = await onEvaluate(ruleId);
      setResults((prev) => ({ ...prev, [ruleId]: result }));
    } catch (err) {
      setErrors((prev) => ({ ...prev, [ruleId]: err as ApiError }));
    } finally {
      setEvaluating(null);
    }
  };

  if (rules.length === 0) {
    return <p className={styles.emptyState}>No alert rules yet.</p>;
  }

  return (
    <ul className={styles.list}>
      {rules.map((rule) => (
        <li key={rule.id} className={styles.row}>
          <span className={styles.label}>
            {pairLabel(rule.watchlistItemId)} — {ALERT_CONDITION_LABELS[rule.condition]} {rule.threshold}
          </span>
          <button
            type="button"
            className={styles.evaluateButton}
            onClick={() => void handleEvaluate(rule.id)}
            disabled={evaluating === rule.id}
          >
            {evaluating === rule.id ? "Evaluating…" : "Evaluate Now"}
          </button>
          {results[rule.id] && <EvaluateResultBanner result={results[rule.id]} />}
          {errors[rule.id] && (
            <p role="alert" className={styles.error}>
              Evaluate failed: {errors[rule.id].detail ?? errors[rule.id].title}
            </p>
          )}
        </li>
      ))}
    </ul>
  );
}
