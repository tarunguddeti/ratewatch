import { expect, test } from "@playwright/test";

// The one full happy path (Testing Strategy): create watchlist -> add pair via the dropdown ->
// refresh -> see a rate -> create alert -> evaluate. Creates its own watchlist rather than
// relying on the seeded "Travel Fund" data, so this test is self-contained and repeatable.
test("create watchlist, add pair, refresh, see a rate, create alert, evaluate", async ({ page }) => {
  const watchlistName = `E2E Test ${Date.now()}`;

  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Watchlists" })).toBeVisible();

  // Create watchlist (FR-001).
  await page.getByLabel("Watchlist name").fill(watchlistName);
  await page.getByRole("button", { name: "Create Watchlist" }).click();
  const watchlistLink = page.getByRole("link", { name: watchlistName });
  await expect(watchlistLink).toBeVisible();

  // Select-to-navigate (FR-003).
  await watchlistLink.click();
  await expect(page.getByRole("heading", { name: watchlistName })).toBeVisible();

  // Add a currency pair via the dropdowns, never free text (FR-006).
  await page.getByLabel("Base currency").selectOption("USD");
  await page.getByLabel("Quote currency").selectOption("AUD");
  await page.getByRole("button", { name: "Add Currency Pair" }).click();
  const pairRow = page.getByRole("row").filter({ hasText: "USD/AUD" });
  await expect(pairRow).toBeVisible();
  await expect(pairRow.getByText("Not fetched yet")).toBeVisible();

  // Refresh Rates (FR-011) - see a real rate (FR-014).
  await page.getByRole("button", { name: "Refresh Rates" }).click();
  await expect(page.getByText("Not fetched yet")).not.toBeVisible({ timeout: 10_000 });

  // Create an alert rule well below any plausible USD/AUD rate, so it reliably triggers
  // (FR-017).
  await page.getByLabel("Pair").selectOption({ label: "USD/AUD" });
  await page.getByLabel("Condition").selectOption("Above");
  await page.getByLabel("Threshold").fill("0.01");
  await page.getByRole("button", { name: "Create Alert Rule" }).click();
  await expect(page.getByText("USD/AUD — Above 0.01")).toBeVisible();

  // Evaluate Now (FR-020) - triggered result renders for that row (FR-022).
  await page.getByRole("button", { name: "Evaluate Now" }).click();
  await expect(page.getByText(/Triggered: USD\/AUD is above 0\.01/)).toBeVisible({ timeout: 10_000 });

  // Back to Watchlists - the escape hatch off the detail page, landing back where the
  // watchlist created at the start of this test is still visible.
  await page.getByRole("link", { name: /back to watchlists/i }).click();
  await expect(page.getByRole("heading", { name: "Watchlists" })).toBeVisible();
  await expect(page.getByRole("link", { name: watchlistName })).toBeVisible();
});
