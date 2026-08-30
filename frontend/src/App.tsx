import { BrowserRouter, Route, Routes } from "react-router";
import { WatchlistsPage } from "./pages/WatchlistsPage";
import { WatchlistDetailPage } from "./pages/WatchlistDetailPage";

// Exactly two user-facing views (spec.md Assumptions) - no additional screens are in scope.
export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<WatchlistsPage />} />
        <Route path="/watchlists/:id" element={<WatchlistDetailPage />} />
      </Routes>
    </BrowserRouter>
  );
}
