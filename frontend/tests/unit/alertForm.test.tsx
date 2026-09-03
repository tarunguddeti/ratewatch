import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { AlertForm } from "../../src/components/AlertForm";
import type { AlertCondition, WatchlistItemDetail } from "../../src/types/domain";

const items: WatchlistItemDetail[] = [{ id: "item-1", baseCurrency: "USD", quoteCurrency: "EUR", latestRate: null }];

// The condition dropdown must offer all four conditions in plain language, and still submit
// the raw AlertCondition value underneath.
describe("AlertForm condition dropdown", () => {
  it("renders all four conditions with plain-language labels", () => {
    render(<AlertForm items={items} onCreate={vi.fn().mockResolvedValue(undefined)} />);

    const select = screen.getByLabelText("Condition") as HTMLSelectElement;
    const optionTexts = Array.from(select.options).map((o) => o.textContent);

    expect(optionTexts).toEqual(["Above", "Below", "At or above", "At or below"]);
  });

  it("submits the raw AlertCondition value for a selected inclusive condition", async () => {
    const onCreate = vi.fn().mockResolvedValue(undefined);
    render(<AlertForm items={items} onCreate={onCreate} />);

    fireEvent.change(screen.getByLabelText("Pair"), { target: { value: "item-1" } });
    fireEvent.change(screen.getByLabelText("Condition"), { target: { value: "AboveOrEqual" as AlertCondition } });
    fireEvent.change(screen.getByLabelText("Threshold"), { target: { value: "1.5" } });
    fireEvent.click(screen.getByRole("button", { name: /create alert rule/i }));

    expect(onCreate).toHaveBeenCalledWith("item-1", "AboveOrEqual", 1.5);
  });
});
