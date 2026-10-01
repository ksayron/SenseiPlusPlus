import { defineConfig, devices } from "@playwright/test";
export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  timeout: 45_000,
  use: {
    baseURL: "http://127.0.0.1:5173",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    channel: process.env.PLAYWRIGHT_CHANNEL,
  },
  projects: [
    { name: "desktop", use: { ...devices["Desktop Chrome"] } },
    {
      name: "narrow",
      use: { ...devices["Pixel 7"], defaultBrowserType: "chromium" },
    },
  ],
  webServer: [
    {
      command:
        "dotnet run --project ../backend/src/Sensei.Host --no-launch-profile --configuration Release --no-build",
      url: "http://localhost:5062/health",
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      env: {
        ASPNETCORE_URLS: "http://localhost:5062",
        ASPNETCORE_ENVIRONMENT: "Development",
        ConnectionStrings__Sensei:
          process.env.ConnectionStrings__Sensei ??
          "Host=localhost;Port=5432;Database=sensei;Username=sensei;Password=sensei_local_dev",
      },
    },
    {
      command: "npm run dev -- --host 127.0.0.1",
      url: "http://127.0.0.1:5173",
      reuseExistingServer: !process.env.CI,
    },
  ],
});
