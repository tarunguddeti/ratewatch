import { useEffect, useState } from "react";
import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { ratesApi } from "../api/rates";
import type { ApiError } from "../api/client";
import type { RateSnapshot } from "../types/domain";

interface RateHistoryChartProps {
  baseCurrency: string;
  quoteCurrency: string;
}

// FR-015 - a small, single-pair line chart, not a multi-pair dashboard (NFR-003). Defaults to
// the last 30 days (the backend applies that default when from/to are omitted).
export function RateHistoryChart({ baseCurrency, quoteCurrency }: RateHistoryChartProps) {
  const [data, setData] = useState<RateSnapshot[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      setData(await ratesApi.getHistory(baseCurrency, quoteCurrency, from || undefined, to || undefined));
    } catch (err) {
      setError(err as ApiError);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [baseCurrency, quoteCurrency]);

  return (
    <section>
      <h3>
        {baseCurrency}/{quoteCurrency} History
      </h3>
      <label>
        From <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
      </label>
      <label>
        To <input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
      </label>
      <button type="button" onClick={() => void load()}>
        Apply Range
      </button>

      {loading && <p>Loading history…</p>}
      {error && <p role="alert">Couldn't load history: {error.detail ?? error.title}</p>}

      {!loading && !error && data && data.length > 0 && (
        <ResponsiveContainer width="100%" height={200}>
          <LineChart data={data}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey="sourceTimestamp" />
            <YAxis domain={["auto", "auto"]} />
            <Tooltip />
            <Line type="monotone" dataKey="rate" dot={false} />
          </LineChart>
        </ResponsiveContainer>
      )}

      {!loading && !error && data && data.length === 0 && <p>No history available for this range.</p>}
    </section>
  );
}
