import { defineConfig } from '@playwright/test'

export default defineConfig({
  testDir: './e2e-ui',
  outputDir: './test-results/ui',
  fullyParallel: true,
  timeout: 30_000,
  use: { baseURL: 'http://localhost:5173', channel: process.env.PLAYWRIGHT_CHANNEL ?? (process.env.CI ? 'chromium' : 'msedge'), trace: 'retain-on-failure' },
  projects: [
    { name: 'desktop', use: { viewport: { width: 1440, height: 1050 } } },
    { name: 'tablet', use: { viewport: { width: 768, height: 1024 } } },
    { name: 'phone', use: { viewport: { width: 320, height: 900 }, isMobile: true, hasTouch: true } },
  ],
  webServer: { command: 'npm run dev', url: 'http://localhost:5173', reuseExistingServer: !process.env.CI },
})
