import { test, expect } from '@playwright/test'

test.beforeEach(async ({ page }) => { await page.goto('/ui-demo.html') })

test('renders without overflow and exposes actual shared gallery controls', async ({ page }, testInfo) => {
  await expect(page.getByRole('heading', { name: 'Making background jobs retry-safe' })).toBeVisible()
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)
  await page.screenshot({ path: `../docs/ui/demo-screenshots/${testInfo.project.name}-reflection.png`, fullPage: true })
  await page.getByRole('tab', { name: 'Component gallery' }).click()
  await expect(page.getByRole('heading', { name: 'A little kit. A coherent campus.' })).toBeVisible()
  await page.getByRole('button', { name: 'Continue', exact: true }).click()
  await expect(page.getByRole('button', { name: 'Selected' })).toBeVisible()
  await page.screenshot({ path: `../docs/ui/demo-screenshots/${testInfo.project.name}-gallery.png`, fullPage: true })
})

test('preserves input through provider failure, retry, and reload', async ({ page }, testInfo) => {
  const answer = 'The invoice exists but the marker does not. A retry may create another invoice. I would use an order id at the invoice boundary.'
  await page.getByLabel('Your explanation', { exact: true }).fill(answer)
  await expect(page.getByText('Saved on this device')).toBeVisible()
  await page.getByRole('switch', { name: 'Try a feedback timeout' }).check()
  await page.getByRole('button', { name: 'Get sample feedback', exact: true }).click()
  await expect(page.getByText('Feedback unavailable', { exact: true })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Get sample feedback', exact: true })).toBeFocused()
  await expect(page.getByLabel('Your explanation', { exact: true })).toHaveValue(answer)
  await page.screenshot({ path: `../docs/ui/demo-screenshots/${testInfo.project.name}-recovery.png`, fullPage: true })
  await page.getByRole('button', { name: 'Retry feedback' }).click()
  await expect(page.getByText('Sample feedback', { exact: true })).toBeVisible()
  await page.reload()
  await expect(page.getByLabel('Your explanation', { exact: true })).toHaveValue(answer)
  await expect(page.getByRole('button', { name: 'Review & acknowledge' })).toBeEnabled()
})

test('dialog traps focus, closes with Escape, and records assisted use', async ({ page }) => {
  const trigger = page.getByRole('button', { name: 'Get a hint' })
  await trigger.click()
  await expect(page.getByRole('dialog')).toBeVisible()
  for (let index = 0; index < 5; index++) {
    await page.keyboard.press('Tab')
    await expect.poll(() => page.getByRole('dialog').evaluate((element) => element.contains(document.activeElement))).toBe(true)
  }
  await page.keyboard.press('Escape')
  await expect(page.getByRole('dialog')).not.toBeVisible()
  await expect(trigger).toBeFocused()
  await expect(page.getByText('Assisted revision', { exact: true })).toBeVisible()
})

test('requires input and explicit acknowledgement tied to the source version', async ({ page }) => {
  await page.getByRole('button', { name: 'Get sample feedback', exact: true }).click()
  await expect(page.getByLabel('Your explanation', { exact: true })).toBeFocused()
  await expect(page.getByText('Write an explanation before requesting feedback.')).toBeVisible()
  await page.getByLabel('Your explanation', { exact: true }).fill('A retry reaches invoice creation again.')
  await page.getByRole('button', { name: 'Keep an open question' }).click()
  await page.getByLabel('What would you investigate next?').fill('Is there a unique constraint on the order id?')
  await page.getByRole('button', { name: 'Review & acknowledge' }).click()
  await expect(page.getByRole('dialog')).toContainText('snapshot-03')
  await expect(page.getByRole('dialog')).toContainText('Is there a unique constraint on the order id?')
  await page.getByRole('button', { name: 'Acknowledge reflection', exact: true }).click()
  await expect(page.getByRole('button', { name: 'Reflection acknowledged' })).toBeDisabled()
  await page.getByLabel('Your explanation', { exact: true }).fill('Changed explanation invalidates acknowledgement.')
  await expect(page.getByRole('button', { name: 'Review & acknowledge' })).toBeEnabled()
})

test('supports keyboard tabs, reduced motion, and enlarged text', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' })
  await page.getByRole('tab', { name: 'Reflection studio' }).focus()
  await page.keyboard.press('ArrowRight')
  await page.keyboard.press('Enter')
  await expect(page.getByRole('tab', { name: 'Component gallery' })).toHaveAttribute('aria-selected', 'true')
  await page.evaluate(() => { document.documentElement.style.fontSize = '200%' })
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)
})
