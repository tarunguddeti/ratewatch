import { useState, type FormEvent } from "react";
import { CurrencySelect } from "./CurrencySelect";
import type { ApiError } from "../api/client";
import styles from "./CurrencyPairForm.module.css";

interface CurrencyPairFormProps {
  onAdd: (baseCurrency: string, quoteCurrency: string) => Promise<void>;
}

// FR-006/007/009 - the backend re-validates everything regardless (docs/architecture.md's
// client-side-validation-is-a-shortcut-not-the-source-of-truth stance), so this component
// just surfaces whatever the backend actually says (409 duplicate, 400 base==quote or
// unsupported currency) as an inline field error (NFR-002).
export function CurrencyPairForm({ onAdd }: CurrencyPairFormProps) {
  const [baseCurrency, setBaseCurrency] = useState("");
  const [quoteCurrency, setQuoteCurrency] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      await onAdd(baseCurrency, quoteCurrency);
      setBaseCurrency("");
      setQuoteCurrency("");
    } catch (err) {
      setError(err as ApiError);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <form onSubmit={(e) => void handleSubmit(e)} className={styles.form}>
      <CurrencySelect label="Base currency" value={baseCurrency} onChange={setBaseCurrency} />
      <CurrencySelect label="Quote currency" value={quoteCurrency} onChange={setQuoteCurrency} />
      <button type="submit" className={`btn-primary ${styles.submitButton}`} disabled={submitting || !baseCurrency || !quoteCurrency}>
        {submitting ? "Adding…" : "Add Currency Pair"}
      </button>
      {error && (
        <p role="alert" className={styles.error}>
          {error.detail ?? error.title}
        </p>
      )}
    </form>
  );
}
