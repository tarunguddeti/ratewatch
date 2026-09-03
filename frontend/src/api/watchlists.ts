import { apiClient } from "./client";
import type { Watchlist, WatchlistDetail, WatchlistItem } from "../types/domain";

// The single source every path below is built from, instead of each independently restating
// "/api/watchlists".
const WATCHLISTS_BASE = "/api/watchlists";

export const watchlistsApi = {
  list: () => apiClient.get<Watchlist[]>(WATCHLISTS_BASE),
  create: (name: string) => apiClient.post<Watchlist>(WATCHLISTS_BASE, { name }),
  getDetail: (id: string) => apiClient.get<WatchlistDetail>(`${WATCHLISTS_BASE}/${id}`),
  remove: (id: string) => apiClient.delete(`${WATCHLISTS_BASE}/${id}`),
  addItem: (watchlistId: string, baseCurrency: string, quoteCurrency: string) =>
    apiClient.post<WatchlistItem>(`${WATCHLISTS_BASE}/${watchlistId}/items`, { baseCurrency, quoteCurrency }),
  removeItem: (watchlistId: string, itemId: string) =>
    apiClient.delete(`${WATCHLISTS_BASE}/${watchlistId}/items/${itemId}`),
};
