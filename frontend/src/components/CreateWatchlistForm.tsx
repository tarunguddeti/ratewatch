import { useState, type FormEvent } from "react";
import type { ApiError } from "../api/client";

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
    <form onSubmit={(e) => void handleSubmit(e)}>
      <label>
        Watchlist name
        <input value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Travel Fund" />
      </label>
      <button type="submit" disabled={submitting}>
        {submitting ? "Creating…" : "Create Watchlist"}
      </button>
      {error && <p role="alert">{error.detail ?? error.title}</p>}
    </form>
  );
}
