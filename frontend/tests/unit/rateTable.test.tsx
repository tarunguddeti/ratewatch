import { fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RateTable } from "../../src/components/RateTable";
import type { WatchlistItemDetail } from "../../src/types/domain";

const items: WatchlistItemDetail[] = [
  { id: "item-1", baseCurrency: "USD", quoteCurrency: "EUR", latestRate: null },
  { id: "item-2", baseCurrency: "GBP", quoteCurrency: "JPY", latestRate: null },
];

function rowFor(pairLabel: string) {
  return screen.getByRole("cell", { name: pairLabel }).closest("tr") as HTMLElement;
}

// Repeated clicks on one row's Remove while its request is in flight must never fire a second
// onRemoveItem call for that row, and must not affect other rows' controls.
describe("RateTable per-row remove busy state", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("disables only the removed row's button and invokes onRemoveItem once even if clicked repeatedly", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(true);
    let resolveRemove!: () => void;
    const onRemoveItem = vi.fn(
      () =>
        new Promise<void>((resolve) => {
          resolveRemove = resolve;
        }),
    );

    render(<RateTable items={items} onRemoveItem={onRemoveItem} />);

    const firstRow = rowFor("USD/EUR");
    const secondRow = rowFor("GBP/JPY");
    const firstRemoveButton = within(firstRow).getByRole("button", { name: /remove/i });

    fireEvent.click(firstRemoveButton);

    const busyButton = await within(firstRow).findByRole("button", { name: /removing/i });
    expect(busyButton).toBeDisabled();

    // A second click on the same (now busy) row must not fire again.
    fireEvent.click(busyButton);
    expect(onRemoveItem).toHaveBeenCalledTimes(1);
    expect(onRemoveItem).toHaveBeenCalledWith("item-1");

    // The other row's Remove control stays independently enabled the whole time.
    expect(within(secondRow).getByRole("button", { name: /^remove$/i })).not.toBeDisabled();

    resolveRemove();
    expect(await within(firstRow).findByRole("button", { name: /^remove$/i })).not.toBeDisabled();
  });

  it("does not call onRemoveItem when the confirmation is dismissed", () => {
    vi.spyOn(window, "confirm").mockReturnValue(false);
    const onRemoveItem = vi.fn().mockResolvedValue(undefined);

    render(<RateTable items={items} onRemoveItem={onRemoveItem} />);
    fireEvent.click(within(rowFor("USD/EUR")).getByRole("button", { name: /remove/i }));

    expect(onRemoveItem).not.toHaveBeenCalled();
  });
});
