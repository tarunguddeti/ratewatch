import { apiClient } from "./client";
import type { Watchlist, WatchlistDetail, WatchlistItem } from "../types/domain";

export const watchlistsApi = {
  list: () => apiClient.get<Watchlist[]>("/api/watchlists"),
  create: (name: string) => apiClient.post<Watchlist>("/api/watchlists", { name }),
  getDetail: (id: string) => apiClient.get<WatchlistDetail>(`/api/watchlists/${id}`),
  remove: (id: string) => apiClient.delete(`/api/watchlists/${id}`),
  addItem: (watchlistId: string, baseCurrency: string, quoteCurrency: string) =>
    apiClient.post<WatchlistItem>(`/api/watchlists/${watchlistId}/items`, { baseCurrency, quoteCurrency }),
  removeItem: (watchlistId: string, itemId: string) =>
    apiClient.delete(`/api/watchlists/${watchlistId}/items/${itemId}`),
};
