import { defineConfig } from "@playwright/test";

// One full happy-path scenario (Testing Strategy in docs/architecture.md), run against real
// backend + frontend dev servers - not mocked, since this is the one test meant to catch
// "the pieces don't actually fit together" bugs the rest of the suite can't.
export default defineConfig({
  testDir: "./tests/e2e",
  timeout: 30_000,
  use: {
    baseURL: "http://localhost:5173",
  },
  webServer: [
    {
      // Deliberately NOT port 5000 - macOS's AirPlay Receiver squats on it by default and
      // returns a real HTTP response (403), which fools a naive "is the server up" check.
      // 5009 matches the scaffolded launchSettings.json http profile.
      command: "dotnet run --urls http://localhost:5009",
      cwd: "../backend/src/CurrencyWatchlist.Api",
      url: "http://localhost:5009/api/watchlists",
      reuseExistingServer: !process.env.CI,
      timeout: 60_000,
      env: {
        DOTNET_ROOT: `${process.env.HOME}/.dotnet`,
        PATH: `${process.env.HOME}/.dotnet:${process.env.PATH}`,
        ASPNETCORE_ENVIRONMENT: "Development",
      },
    },
    {
      command: "npm run dev -- --port 5173",
      url: "http://localhost:5173",
      reuseExistingServer: !process.env.CI,
      timeout: 30_000,
    },
  ],
});
