import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";
import { WatchlistCard } from "../../src/components/WatchlistCard";
import type { Watchlist } from "../../src/types/domain";

const watchlist: Watchlist = {
  id: "wl-1",
  name: "Travel Fund",
  createdAt: "2026-01-01",
  itemCount: 2,
  alertRuleCount: 1,
};

function renderCard(onDelete: (id: string) => Promise<void>) {
  return render(
    <MemoryRouter>
      <WatchlistCard watchlist={watchlist} onDelete={onDelete} />
    </MemoryRouter>,
  );
}

// Repeated clicks on Delete while a request is already in flight must never fire a second
// onDelete call for the same watchlist.
describe("WatchlistCard delete busy state", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("disables Delete and invokes onDelete only once when clicked repeatedly before it resolves", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(true);
    let resolveDelete!: () => void;
    const onDelete = vi.fn(
      () =>
        new Promise<void>((resolve) => {
          resolveDelete = resolve;
        }),
    );

    renderCard(onDelete);
    const button = screen.getByRole("button", { name: /delete/i });

    fireEvent.click(button);
    expect(await screen.findByRole("button", { name: /deleting/i })).toBeDisabled();

    // A second click while the first request is still in flight must not fire again.
    fireEvent.click(screen.getByRole("button", { name: /deleting/i }));
    expect(onDelete).toHaveBeenCalledTimes(1);

    resolveDelete();
    expect(await screen.findByRole("button", { name: /^delete$/i })).not.toBeDisabled();
  });

  it("does not call onDelete when the confirmation is dismissed", () => {
    vi.spyOn(window, "confirm").mockReturnValue(false);
    const onDelete = vi.fn().mockResolvedValue(undefined);

    renderCard(onDelete);
    fireEvent.click(screen.getByRole("button", { name: /delete/i }));

    expect(onDelete).not.toHaveBeenCalled();
  });
});
