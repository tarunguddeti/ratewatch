import { useState, type FormEvent } from "react";
import type { ApiError } from "../api/client";
import styles from "./CreateWatchlistForm.module.css";

interface CreateWatchlistFormProps {
  onCreate: (name: string) => Promise<void>;
}

export function CreateWatchlistForm({ onCreate }: CreateWatchlistFormProps) {
  const [name, setName] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      await onCreate(name);
      setName("");
    } catch (err) {
      setError(err as ApiError);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <form onSubmit={(e) => void handleSubmit(e)} className={styles.form}>
      <label className={styles.field}>
        Watchlist name
        <input value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Travel Fund" />
      </label>
      <button type="submit" className="btn-primary" disabled={submitting}>
        {submitting ? "Creating…" : "Create Watchlist"}
      </button>
      {error && (
        <p role="alert" className={styles.error}>
          {error.detail ?? error.title}
        </p>
      )}
    </form>
  );
}
