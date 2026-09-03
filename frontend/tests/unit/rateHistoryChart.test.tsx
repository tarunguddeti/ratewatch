import { render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RateHistoryChart } from "../../src/components/RateHistoryChart";

interface FetchResult {
  ok: boolean;
  status: number;
  json: () => Promise<unknown>;
}

function deferred() {
  let resolve!: (value: FetchResult) => void;
  const promise = new Promise<FetchResult>((res) => {
    resolve = res;
  });
  return { promise, resolve };
}

// "Apply Range" can't be used to start an overlapping request while one is in flight, and a
// response from a superseded request must never overwrite the chart with data for a
// pair/range the user no longer has selected.
describe("RateHistoryChart", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("disables Apply Range while the initial request is in flight and re-enables it after", async () => {
    const request = deferred();
    vi.stubGlobal("fetch", vi.fn().mockReturnValue(request.promise));

    render(<RateHistoryChart baseCurrency="USD" quoteCurrency="EUR" />);

    expect(await screen.findByRole("button", { name: /applying/i })).toBeDisabled();

    request.resolve({ ok: true, status: 200, json: async () => [] });

    expect(await screen.findByRole("button", { name: /^apply range$/i })).not.toBeDisabled();
  });

  it("discards a stale response when a newer request has since started, even if it resolves last", async () => {
    const firstRequest = deferred();
    const secondRequest = deferred();

    vi.stubGlobal(
      "fetch",
      vi.fn((url: string) => {
        if (url.includes("base=USD")) return firstRequest.promise;
        if (url.includes("base=GBP")) return secondRequest.promise;
        throw new Error(`Unexpected fetch: ${url}`);
      }),
    );

    const { rerender } = render(<RateHistoryChart baseCurrency="USD" quoteCurrency="EUR" />);
    await screen.findByRole("button", { name: /applying/i });

    // Switching pairs before the first (stale) request resolves fires a second, newer request -
    // the same race that can occur between an in-flight Apply Range call and this effect.
    rerender(<RateHistoryChart baseCurrency="GBP" quoteCurrency="JPY" />);

    // Resolve the newer request first with an empty result (so a correct guard renders the
    // "No history" text-only branch, never touching the chart itself), then resolve the stale
    // first request afterwards with non-empty data it must not be allowed to apply.
    secondRequest.resolve({ ok: true, status: 200, json: async () => [] });
    await screen.findByText(/no history available/i);

    firstRequest.resolve({
      ok: true,
      status: 200,
      json: async () => [{ rate: 1.1, sourceTimestamp: "2026-01-01T00:00:00Z" }],
    });

    // The stale response must not resurrect a loading/chart state for the pair the user left.
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(screen.getByText(/no history available/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^apply range$/i })).not.toBeDisabled();
  });
});
