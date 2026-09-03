import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { AlertList } from "../../src/components/AlertList";
import type { AlertRule, WatchlistItemDetail } from "../../src/types/domain";

const items: WatchlistItemDetail[] = [{ id: "item-1", baseCurrency: "USD", quoteCurrency: "EUR", latestRate: null }];

function rule(condition: AlertRule["condition"], id: string): AlertRule {
  return { id, watchlistItemId: "item-1", condition, threshold: 1.5, createdAt: "2026-01-01" };
}

// Every condition, old (strict) or new (inclusive), must render as a plain-language label,
// never the raw enum string.
describe("AlertList condition display", () => {
  it("renders a plain-language label for all four condition types", () => {
    const rules: AlertRule[] = [
      rule("Above", "r1"),
      rule("Below", "r2"),
      rule("AboveOrEqual", "r3"),
      rule("BelowOrEqual", "r4"),
    ];

    render(<AlertList rules={rules} items={items} onEvaluate={vi.fn()} />);

    expect(screen.getByText(/USD\/EUR — Above 1\.5/)).toBeInTheDocument();
    expect(screen.getByText(/USD\/EUR — Below 1\.5/)).toBeInTheDocument();
    expect(screen.getByText(/USD\/EUR — At or above 1\.5/)).toBeInTheDocument();
    expect(screen.getByText(/USD\/EUR — At or below 1\.5/)).toBeInTheDocument();
    expect(screen.queryByText(/AboveOrEqual/)).not.toBeInTheDocument();
    expect(screen.queryByText(/BelowOrEqual/)).not.toBeInTheDocument();
  });

  it("still displays a pre-existing strict-condition rule exactly as before", () => {
    render(<AlertList rules={[rule("Above", "r1")]} items={items} onEvaluate={vi.fn()} />);

    expect(screen.getByText("USD/EUR — Above 1.5")).toBeInTheDocument();
  });
});
