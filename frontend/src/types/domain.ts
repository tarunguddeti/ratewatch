// Mirrors the DTO shapes in backend/src/CurrencyWatchlist.Application/Dtos/ and
// specs/001-currency-watchlist-alerts/contracts/api-contracts.md. Field names are camelCase,
// matching System.Text.Json's default serialization of the backend's C# records.

export interface Watchlist {
  id: string;
  name: string;
  createdAt: string;
  itemCount: number;
  alertRuleCount: number;
}

export interface WatchlistDetail {
  id: string;
  name: string;
  createdAt: string;
  items: WatchlistItemDetail[];
}

export interface WatchlistItem {
  id: string;
  watchlistId: string;
  baseCurrency: string;
  quoteCurrency: string;
}

export interface WatchlistItemDetail {
  id: string;
  baseCurrency: string;
  quoteCurrency: string;
  latestRate: RateSnapshot | null;
}

export interface RateSnapshot {
  baseCurrency: string;
  quoteCurrency: string;
  rate: number;
  sourceTimestamp: string;
  fetchedAt: string;
}

export interface FailedPair {
  pair: string;
  reason: string;
}

export interface RefreshSummary {
  refreshed: RateSnapshot[];
  failed: FailedPair[];
}

export interface Currency {
  code: string;
  name: string;
}

export type AlertCondition = "Above" | "Below" | "AboveOrEqual" | "BelowOrEqual";

// The single source AlertForm's option list is derived from, so a third condition would mean
// changing one array entry rather than a hand-edited JSX block (specs/004-strong-typing-cleanup).
export const ALERT_CONDITIONS: readonly AlertCondition[] = ["Above", "Below", "AboveOrEqual", "BelowOrEqual"];

// Plain-language labels for each condition, used everywhere a condition is shown to a user
// (specs/007-inclusive-alert-conditions) - the raw enum string is never displayed directly.
export const ALERT_CONDITION_LABELS: Record<AlertCondition, string> = {
  Above: "Above",
  Below: "Below",
  AboveOrEqual: "At or above",
  BelowOrEqual: "At or below",
};

export interface AlertRule {
  id: string;
  watchlistItemId: string;
  condition: AlertCondition;
  threshold: number;
  createdAt: string;
}

export interface EvaluateResult {
  triggered: boolean;
  currentRate: number;
  threshold: number;
  condition: AlertCondition;
  message: string;
  evaluatedAt: string;
}
