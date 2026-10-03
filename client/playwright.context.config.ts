import { defineConfig } from '@playwright/test'
import base from './playwright.config'

export default defineConfig({
  ...base,
  testMatch: 'context.spec.ts',
  outputDir: '../.tmp/context-browser',
  timeout: 60_000,
  projects: [
    { name: 'desktop', use: { viewport: { width: 1440, height: 1050 } } },
    { name: 'tablet', use: { viewport: { width: 768, height: 1024 } } },
    { name: 'phone', use: { viewport: { width: 320, height: 900 }, isMobile: true, hasTouch: true } },
  ],
})
