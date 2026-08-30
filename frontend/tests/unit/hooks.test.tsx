import { renderHook, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { useWatchlists } from "../../src/hooks/useWatchlists";

function mockFetchOnce(status: number, body: unknown) {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue({
      ok: status >= 200 && status < 300,
      status,
      json: async () => body,
    }),
  );
}

// NFR-001 - every hook exposes { data, loading, error }, starting loading, then resolving to
// either data or error, never leaving the consumer without a way to render an in-progress state.
describe("useWatchlists - the { data, loading, error } contract", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("starts in a loading state with no data or error", () => {
    mockFetchOnce(200, []);
    const { result } = renderHook(() => useWatchlists());

    expect(result.current.loading).toBe(true);
    expect(result.current.data).toBeNull();
    expect(result.current.error).toBeNull();
  });

  it("resolves to data with loading false on success", async () => {
    const watchlists = [{ id: "1", name: "Travel Fund", createdAt: "2026-01-01", itemCount: 2, alertRuleCount: 0 }];
    mockFetchOnce(200, watchlists);

    const { result } = renderHook(() => useWatchlists());

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.data).toEqual(watchlists);
    expect(result.current.error).toBeNull();
  });

  it("resolves to an ApiError with loading false on failure", async () => {
    mockFetchOnce(500, { title: "An unexpected error occurred", status: 500 });

    const { result } = renderHook(() => useWatchlists());

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.data).toBeNull();
    expect(result.current.error).not.toBeNull();
    expect(result.current.error?.status).toBe(500);
  });

  it("normalizes a network-level failure (fetch throws) into ApiError with status: null", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));

    const { result } = renderHook(() => useWatchlists());

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.error?.status).toBeNull();
  });
});
