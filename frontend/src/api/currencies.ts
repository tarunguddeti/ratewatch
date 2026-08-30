import { apiClient } from "./client";
import type { Currency } from "../types/domain";

export const currenciesApi = {
  // Calls OUR /api/currencies, never Frankfurter directly (FR-005) - the frontend has no
  // third-party awareness anywhere in this app.
  list: () => apiClient.get<Currency[]>("/api/currencies"),
};
