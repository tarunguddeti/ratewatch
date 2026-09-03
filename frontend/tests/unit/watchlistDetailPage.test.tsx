import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";
import { WatchlistDetailPage } from "../../src/pages/WatchlistDetailPage";

const WATCHLIST_ID = "11111111-1111-1111-1111-111111111111";

function mockFetchByUrl(responses: { pattern: string; status: number; body: unknown }[]) {
  vi.stubGlobal(
    "fetch",
    vi.fn((url: string) => {
      const match = responses.find((r) => url.includes(r.pattern));
      if (!match) {
        throw new Error(`Unexpected fetch call: ${url}`);
      }
      return Promise.resolve({
        ok: match.status >= 200 && match.status < 300,
        status: match.status,
        json: async () => match.body,
      });
    }),
  );
}

function renderAtDetailRoute(path = `/watchlists/${WATCHLIST_ID}`) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/watchlists/:id" element={<WatchlistDetailPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

// A back link to the Watchlists overview must exist wherever this page can leave a user
// stuck - not just the happy path, since a broken/not-found detail page is exactly when an
// escape hatch matters most.
describe("WatchlistDetailPage back navigation", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("shows a link back to the Watchlists overview once the watchlist loads", async () => {
    mockFetchByUrl([
      { pattern: "/api/watchlists/", status: 200, body: { id: WATCHLIST_ID, name: "Travel Fund", createdAt: "2026-01-01", items: [] } },
      { pattern: "/api/alerts", status: 200, body: [] },
    ]);

    renderAtDetailRoute();

    const backLink = await screen.findByRole("link", { name: /back to watchlists/i });
    expect(backLink).toHaveAttribute("href", "/");
  });

  it("shows a link back to the Watchlists overview when the watchlist is not found (404)", async () => {
    mockFetchByUrl([
      { pattern: "/api/watchlists/", status: 404, body: { title: "Not found", status: 404 } },
      { pattern: "/api/alerts", status: 200, body: [] },
    ]);

    renderAtDetailRoute();

    expect(await screen.findByText(/wasn't found/i)).toBeInTheDocument();
    const backLink = screen.getByRole("link", { name: /back to watchlists/i });
    expect(backLink).toHaveAttribute("href", "/");
  });

  it("shows a link back to the Watchlists overview on a generic load failure", async () => {
    mockFetchByUrl([
      { pattern: "/api/watchlists/", status: 500, body: { title: "An unexpected error occurred", status: 500 } },
      { pattern: "/api/alerts", status: 200, body: [] },
    ]);

    renderAtDetailRoute();

    expect(await screen.findByRole("alert")).toBeInTheDocument();
    const backLink = screen.getByRole("link", { name: /back to watchlists/i });
    expect(backLink).toHaveAttribute("href", "/");
  });
});

// specs/006-fix-ui-loading-bugs FR-001 - removing a currency pair (or any other mutation) must
// not blank the already-rendered rate table back to the full-page "Loading watchlist…" message;
// only a small localized indicator should reflect the in-progress state.
describe("WatchlistDetailPage keeps content visible during a mutation", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("keeps the rate table on screen while removing an item, instead of showing the full-page loading message", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(true);

    let resolveDelete!: () => void;
    const deletePending = new Promise<void>((resolve) => {
      resolveDelete = resolve;
    });

    const detailBody = {
      id: WATCHLIST_ID,
      name: "Travel Fund",
      createdAt: "2026-01-01",
      items: [{ id: "item-1", baseCurrency: "USD", quoteCurrency: "EUR", latestRate: null }],
    };

    vi.stubGlobal(
      "fetch",
      vi.fn((url: string, init?: RequestInit) => {
        if (init?.method === "DELETE") {
          return deletePending.then(() => ({ ok: true, status: 204, json: async () => undefined }));
        }
        if (url.includes("/api/alerts")) {
          return Promise.resolve({ ok: true, status: 200, json: async () => [] });
        }
        return Promise.resolve({ ok: true, status: 200, json: async () => detailBody });
      }),
    );

    renderAtDetailRoute();

    const removeButton = await screen.findByRole("button", { name: /remove/i });
    fireEvent.click(removeButton);

    // While the DELETE request is still pending, the row and the rest of the page must remain
    // visible - never replaced by the full-page "Loading watchlist…" message.
    expect(await screen.findByText(/updating/i)).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "USD/EUR" })).toBeInTheDocument();
    expect(screen.queryByText(/loading watchlist/i)).not.toBeInTheDocument();

    resolveDelete();
    await waitFor(() => expect(screen.queryByText(/updating/i)).not.toBeInTheDocument());
  });
});
