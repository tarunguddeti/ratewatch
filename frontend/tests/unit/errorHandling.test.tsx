import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { CreateWatchlistForm } from "../../src/components/CreateWatchlistForm";
import { RefreshRatesButton } from "../../src/components/RefreshRatesButton";
import { WatchlistsPage } from "../../src/pages/WatchlistsPage";
import type { ApiError } from "../../src/api/client";

function mockFetchSequence(...responses: { status: number; body: unknown }[]) {
  const fn = vi.fn();
  for (const { status, body } of responses) {
    fn.mockResolvedValueOnce({ ok: status >= 200 && status < 300, status, json: async () => body });
  }
  vi.stubGlobal("fetch", fn);
  return fn;
}

// NFR-002 - every failure gets a specific, actionable message via one of three treatments,
// never a generic catch-all (docs/architecture.md's Frontend error shape).
describe("the three ApiError-driven UI treatments", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("inline field error: a create-form failure renders next to the form, not as a page takeover", async () => {
    const apiError: ApiError = { status: 400, title: "Validation failed", detail: "Watchlist name is required." };
    const onCreate = vi.fn().mockRejectedValue(apiError);

    render(<CreateWatchlistForm onCreate={onCreate} />);
    await userEvent.click(screen.getByRole("button", { name: /create watchlist/i }));

    const inlineError = await screen.findByRole("alert");
    expect(inlineError).toHaveTextContent("Watchlist name is required.");
    // The form itself is still present - an inline error doesn't blank the surrounding UI.
    expect(screen.getByRole("button", { name: /create watchlist/i })).toBeInTheDocument();
  });

  it("page-level retry: a full-page fetch failure replaces the content area with an error and a Retry button", async () => {
    mockFetchSequence({ status: 500, body: { title: "An unexpected error occurred", status: 500 } });

    render(<WatchlistsPage />);

    const retryButton = await screen.findByRole("button", { name: /retry/i });
    expect(retryButton).toBeInTheDocument();
    expect(screen.queryByText(/no watchlists yet/i)).not.toBeInTheDocument();

    // Retry succeeding replaces the error state with real content.
    mockFetchSequence({ status: 200, body: [] });
    await userEvent.click(retryButton);

    await waitFor(() => expect(screen.getByText(/no watchlists yet/i)).toBeInTheDocument());
  });

  it("non-blocking banner: a refresh failure shows a banner without removing the trigger control", async () => {
    mockFetchSequence({ status: 502, body: { title: "Rate provider unavailable", status: 502, detail: "Could not reach the rate provider." } });

    render(<RefreshRatesButton onRefreshed={() => {}} />);
    await userEvent.click(screen.getByRole("button", { name: /refresh rates/i }));

    const banner = await screen.findByText(/could not reach the rate provider/i);
    expect(banner).toBeInTheDocument();
    // Non-blocking: the button that triggered it is still there, ready to retry.
    expect(screen.getByRole("button", { name: /refresh rates/i })).toBeInTheDocument();
  });
});
