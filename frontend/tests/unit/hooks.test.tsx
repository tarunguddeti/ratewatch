import { act, renderHook, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { useWatchlists } from "../../src/hooks/useWatchlists";
import { useWatchlistDetail } from "../../src/hooks/useWatchlistDetail";

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

// Every hook exposes { data, loading, error }, starting loading, then resolving to either data
// or error, never leaving the consumer without a way to render an in-progress state.
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

// create/remove (and the refresh reload) must track their own `mutating` flag, not the
// page-level `loading` flag, so the already-rendered list never gets blanked back to a
// full-page loading state during a routine action.
describe("useWatchlists - mutating tracks create/remove without touching loading", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("sets mutating (not loading) while create is in flight, and clears it on success", async () => {
    mockFetchOnce(200, []);
    const { result } = renderHook(() => useWatchlists());
    await waitFor(() => expect(result.current.loading).toBe(false));

    let resolvePost!: () => void;
    const pending = new Promise<void>((resolve) => {
      resolvePost = resolve;
    });
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(() => pending.then(() => ({ ok: true, status: 201, json: async () => [] }))),
    );

    let createCall!: Promise<void>;
    act(() => {
      createCall = result.current.create("Travel Fund");
    });

    await waitFor(() => expect(result.current.mutating).toBe(true));
    expect(result.current.loading).toBe(false);

    resolvePost();
    await act(async () => {
      await createCall;
    });

    expect(result.current.mutating).toBe(false);
    expect(result.current.loading).toBe(false);
  });

  it("clears mutating even when remove fails", async () => {
    mockFetchOnce(200, [{ id: "1", name: "Travel Fund", createdAt: "2026-01-01", itemCount: 0, alertRuleCount: 0 }]);
    const { result } = renderHook(() => useWatchlists());
    await waitFor(() => expect(result.current.loading).toBe(false));

    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));

    await act(async () => {
      await expect(result.current.remove("1")).rejects.toThrow();
    });

    expect(result.current.mutating).toBe(false);
    expect(result.current.loading).toBe(false);
  });
});

// Same contract for the detail hook: addItem/removeItem and the refresh-triggered reload use
// `mutating`, never the page-level `loading` flag.
describe("useWatchlistDetail - mutating tracks addItem/removeItem/reloadAfterRefresh without touching loading", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("sets mutating (not loading) while addItem is in flight, and clears it on success", async () => {
    mockFetchOnce(200, { id: "wl-1", name: "Travel Fund", createdAt: "2026-01-01", items: [] });
    const { result } = renderHook(() => useWatchlistDetail("wl-1"));
    await waitFor(() => expect(result.current.loading).toBe(false));

    let resolvePost!: () => void;
    const pending = new Promise<void>((resolve) => {
      resolvePost = resolve;
    });
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(() =>
        pending.then(() => ({
          ok: true,
          status: 201,
          json: async () => ({ id: "wl-1", name: "Travel Fund", createdAt: "2026-01-01", items: [] }),
        })),
      ),
    );

    let addCall!: Promise<void>;
    act(() => {
      addCall = result.current.addItem("USD", "EUR");
    });

    await waitFor(() => expect(result.current.mutating).toBe(true));
    expect(result.current.loading).toBe(false);

    resolvePost();
    await act(async () => {
      await addCall;
    });

    expect(result.current.mutating).toBe(false);
    expect(result.current.loading).toBe(false);
  });

  it("sets mutating (not loading) while reloadAfterRefresh is in flight", async () => {
    mockFetchOnce(200, { id: "wl-1", name: "Travel Fund", createdAt: "2026-01-01", items: [] });
    const { result } = renderHook(() => useWatchlistDetail("wl-1"));
    await waitFor(() => expect(result.current.loading).toBe(false));

    let resolveGet!: () => void;
    const pending = new Promise<void>((resolve) => {
      resolveGet = resolve;
    });
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(() =>
        pending.then(() => ({
          ok: true,
          status: 200,
          json: async () => ({ id: "wl-1", name: "Travel Fund", createdAt: "2026-01-01", items: [] }),
        })),
      ),
    );

    let reloadCall!: Promise<void>;
    act(() => {
      reloadCall = result.current.reloadAfterRefresh();
    });

    await waitFor(() => expect(result.current.mutating).toBe(true));
    expect(result.current.loading).toBe(false);

    resolveGet();
    await act(async () => {
      await reloadCall;
    });

    expect(result.current.mutating).toBe(false);
    expect(result.current.loading).toBe(false);
  });
});
