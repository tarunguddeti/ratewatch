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

export type AlertCondition = "Above" | "Below";

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
