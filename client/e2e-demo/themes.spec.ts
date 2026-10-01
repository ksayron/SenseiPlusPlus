import { test, expect } from '@playwright/test'
import { themes } from '../src/demo/themes'

test('resumes a retired palette without losing reflection content', async ({ page }) => {
  await page.goto('/ui-demo.html')
  await page.getByLabel('Your explanation', { exact: true }).fill('Keep my draft while migrating the palette.')
  await expect(page.getByText('Saved on this device')).toBeVisible()
  await page.evaluate(() => localStorage.setItem('sensei-ui-demo:color-theme:v1', 'dusk'))
  await page.reload()
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'coastal-night')
  await expect(page.getByRole('button', { name: 'Choose color theme. Current: Coastal Night' })).toBeVisible()
  await expect(page.getByLabel('Your explanation', { exact: true })).toHaveValue('Keep my draft while migrating the palette.')
  await page.evaluate(() => localStorage.setItem('sensei-ui-demo:color-theme:v1', 'botanical'))
  await page.reload()
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'coastal')
})

test('previews each palette, preserves the draft, and resumes the chosen theme', async ({ page }, testInfo) => {
  await page.goto('/ui-demo.html')
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'coastal')
  await page.getByLabel('Your explanation', { exact: true }).fill('A retry could create the invoice twice.')
  for (const theme of themes) {
    const picker = page.getByRole('button', { name: /^Choose color theme/ })
    await picker.click()
    const option = page.getByRole('button', { name: `${theme.name} ${theme.description}`, exact: true })
    await option.click()
    await expect(page.locator('html')).toHaveAttribute('data-theme', theme.id)
    await expect(option).toHaveAttribute('aria-pressed', 'true')
    await page.keyboard.press('Escape')
    await expect(picker).toBeFocused()
    await expect(page.getByLabel('Your explanation', { exact: true })).toHaveValue('A retry could create the invoice twice.')
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)
    await page.screenshot({ path: `../docs/ui/demo-screenshots/${testInfo.project.name}-theme-${theme.id}.png`, fullPage: true })
  }
  await page.reload()
  const lastTheme = themes.at(-1)!
  await expect(page.locator('html')).toHaveAttribute('data-theme', lastTheme.id)
  await expect(page.getByLabel('Your explanation', { exact: true })).toHaveValue('A retry could create the invoice twice.')
  await page.getByRole('tab', { name: 'Component gallery' }).click()
  await expect(page.getByText(lastTheme.tokens.primary, { exact: true })).toBeVisible()
  await page.screenshot({ path: `../docs/ui/demo-screenshots/${testInfo.project.name}-theme-${lastTheme.id}-gallery.png`, fullPage: true })
  await page.getByRole('button', { name: /^Choose color theme/ }).click()
  await expect(page.getByRole('region', { name: 'Light themes' }).getByRole('button')).toHaveCount(3)
  await expect(page.getByRole('region', { name: 'Dark themes' }).getByRole('button')).toHaveCount(3)
  await page.screenshot({ path: `../docs/ui/demo-screenshots/${testInfo.project.name}-theme-picker.png` })
})
