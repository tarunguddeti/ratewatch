import { useCurrencies } from "../hooks/useCurrencies";
import styles from "./CurrencySelect.module.css";

interface CurrencySelectProps {
  label: string;
  value: string;
  onChange: (code: string) => void;
}

// Dropdown-only, no text-input fallback - the only way a currency reaches the backend is by
// being selected from this list. If the list can't load, adding a pair is simply unavailable
// until Retry succeeds.
export function CurrencySelect({ label, value, onChange }: CurrencySelectProps) {
  const { data, loading, error, retry } = useCurrencies();

  if (loading) {
    return (
      <label className={styles.field}>
        {label}
        <select className={styles.select} disabled>
          <option>Loading currencies…</option>
        </select>
      </label>
    );
  }

  if (error) {
    return (
      <div role="alert" className={styles.errorBox}>
        <span>Couldn't load currencies: {error.detail ?? error.title}</span>
        <button type="button" className="btn-secondary" onClick={() => void retry()}>
          Retry
        </button>
      </div>
    );
  }

  return (
    <label className={styles.field}>
      {label}
      <select className={styles.select} value={value} onChange={(e) => onChange(e.target.value)} required>
        <option value="" disabled>
          Select a currency
        </option>
        {data?.map((c) => (
          <option key={c.code} value={c.code}>
            {c.code} — {c.name}
          </option>
        ))}
      </select>
    </label>
  );
}
