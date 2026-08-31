import { render, screen } from "@testing-library/react";
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
