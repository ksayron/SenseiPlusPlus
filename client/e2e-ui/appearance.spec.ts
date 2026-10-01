import { expect, test, type Page } from '@playwright/test'

const concept = {
  id: 'concept-1', key: 'delivery', name: 'Reliable message delivery',
  description: 'Explain retry boundaries, ownership and the limits of at-least-once delivery.',
  locale: 'en', difficulty: 'Intermediate', isActive: true, createdAt: '2026-10-01T09:00:00Z', versionToken: 'v1',
}
const topic = { concept, relations: [], materialVersions: ['material-1'], availableFamilies: 3 }
const episode = {
  id: 'episode-1', title: 'Made webhook retries observable', setting: 'PersonalProject', eventDate: '2026-10-01',
  role: 'Backend developer', summary: 'Traced duplicate delivery and documented the retry boundary.', isArchived: false,
}
const observation = {
  id: 'signal-1', conceptId: concept.id, aspect: 'Explained retry limits from memory',
  signalKind: 'Explanation', sourceKind: 'Declaration', assistance: 'None', status: 'Active', observedAt: '2026-10-01T09:00:00Z',
}
const experience = {
  id: 'entry-1', isArchived: false, revisions: [{ number: 1, title: 'A safer retry boundary',
    context: 'Investigated duplicated webhooks and made delivery failures inspectable.', role: 'Designed and implemented the retry policy',
    impactState: 'Qualitative', conceptIds: [concept.id], approvalState: 'Draft' }],
}

async function fixtures(page: Page) {
  await page.route('**/health', route => route.fulfill({ json: { status: 'Healthy' } }))
  await page.route('**/api/v1/**', route => {
    const path = new URL(route.request().url()).pathname.replace(/\/$/, '')
    if (path === '/api/v1/learning/material-views') return route.fulfill({ status: 204 })
    if (route.request().method() !== 'GET') return route.fulfill({ status: 503, json: { detail: 'Could not save. Please retry.' } })
    const single: Record<string, unknown> = {
      '/api/v1/learning/topics/concept-1': topic,
      '/api/v1/evidence/knowledge/concept-1': { familyCount: 0, assistedCount: 0 },
      '/api/v1/learning/materials/material-1': { id: 'material-1', locale: 'en', blocks: [
        { kind: 'heading', text: 'Where retries belong' },
        { kind: 'paragraph', text: 'Keep each retry tied to an explicit delivery boundary.' },
        { kind: 'code', language: 'csharp', text: 'await DeliverAsync(message, cancellationToken);' },
      ] },
    }
    if (single[path]) return route.fulfill({ json: single[path] })
    let items: unknown[] = []
    if (path === '/api/v1/learning/concepts') items = [concept]
    if (path === '/api/v1/learning/topics') items = [topic]
    if (path === '/api/v1/work/episodes') items = [episode]
    if (path === '/api/v1/evidence/observations') items = [observation]
    if (path === '/api/v1/experience/entries') items = [experience]
    return route.fulfill({ json: { items, nextCursor: null } })
  })
}

test.beforeEach(async ({ page }) => { await fixtures(page) })

test('all workspace routes reflow in both appearances', async ({ page }, info) => {
  const errors: string[] = []
  page.on('pageerror', error => errors.push(error.message))
  for (const appearance of ['coastal', 'coastal-night']) {
    await page.goto('/')
    if (appearance === 'coastal-night') await page.getByRole('button', { name: 'Night appearance' }).click()
    for (const route of ['/', '/learn', '/reflect', '/evidence', '/experience']) {
      await page.goto(route)
      await expect(page.locator('h1')).toBeVisible()
      await expect(page.locator('html')).toHaveAttribute('data-theme', appearance)
      await expect(page.getByRole('navigation', { name: 'Main navigation' }).locator('[aria-current=page]')).toHaveCount(1)
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
      await page.screenshot({ path: `../docs/ui/spa-screenshots/${info.project.name}-${appearance}-${route.slice(1) || 'today'}.png`, fullPage: true })
    }
  }
  expect(errors).toEqual([])
})

test('appearance preserves task input, route and reload preference', async ({ page }) => {
  await page.goto('/learn')
  const input = page.getByLabel('What would you like to work on?')
  await input.fill('Explain retries without relying on notes.')
  await page.getByRole('button', { name: 'Night appearance' }).click()
  await expect(input).toHaveValue('Explain retries without relying on notes.')
  await expect(page).toHaveURL(/\/learn$/)
  await expect(page.locator('html')).toHaveClass('dark')
  await expect(page.getByRole('button', { name: 'Night appearance' })).toBeFocused()
  await page.reload()
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'coastal-night')
})

test('dialogs contain keyboard focus, retain failed input and return focus', async ({ page }) => {
  await page.goto('/reflect')
  const trigger = page.getByRole('button', { name: 'Capture work', exact: true })
  await trigger.click()
  const dialog = page.getByRole('dialog', { name: 'Capture meaningful work' })
  await expect(dialog).toBeVisible()
  await page.getByLabel('Episode title').fill('Keep this draft after failure')
  await page.getByLabel('Your role', { exact: true }).fill('Developer')
  await page.getByLabel('What happened?', { exact: true }).fill('Investigated a retry boundary.')
  await page.getByRole('button', { name: 'Capture episode', exact: true }).click()
  await expect(dialog.getByRole('alert')).toHaveText('Could not save. Please retry.')
  await expect(page.getByLabel('Episode title')).toHaveValue('Keep this draft after failure')
  for (let i = 0; i < 12; i++) {
    await page.keyboard.press('Tab')
    await expect.poll(() => page.evaluate(() => !!document.activeElement?.closest('[role=dialog]'))).toBe(true)
  }
  await page.keyboard.press('Escape')
  await expect(dialog).not.toBeVisible()
  await expect(trigger).toBeFocused()
})

test('topic tabs support keyboard, URLs and technical content in both themes', async ({ page }, info) => {
  await page.goto('/learn/topics/concept-1?section=overview')
  const overview = page.getByRole('tab', { name: 'overview', exact: true })
  await expect(overview).toHaveAttribute('aria-selected', 'true')
  await overview.focus()
  await page.keyboard.press('ArrowRight')
  await page.keyboard.press('Enter')
  await expect(page).toHaveURL(/section=material/)
  await expect(page.getByRole('tabpanel')).toContainText('Where retries belong')
  for (const appearance of ['coastal', 'coastal-night']) {
    if (appearance === 'coastal-night') await page.getByRole('button', { name: 'Night appearance' }).click()
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
    await page.screenshot({ path: `../docs/ui/spa-screenshots/${info.project.name}-${appearance}-material.png`, fullPage: true })
  }
  await page.reload()
  await expect(page.getByRole('tab', { name: 'material', exact: true })).toHaveAttribute('aria-selected', 'true')
})

test('enlarged text and reduced motion keep actions available', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' })
  await page.goto('/experience')
  await page.addStyleTag({ content: 'html { font-size: 200%; }' })
  await expect(page.getByRole('button', { name: 'New entry' })).toBeVisible()
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
  await page.getByRole('button', { name: 'New entry' }).click()
  await expect(page.getByRole('dialog')).toBeVisible()
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
})

test('action labels and identifying field boundaries meet contrast targets', async ({ page }) => {
  await page.goto('/experience')
  for (const appearance of ['coastal', 'coastal-night']) {
    if (appearance === 'coastal-night') await page.getByRole('button', { name: 'Night appearance' }).click()
    const action = page.getByRole('button', { name: 'New entry' })
    for (const hover of [false, true]) {
      if (hover) await action.hover()
      const ratio = await action.evaluate(element => {
        const luminance = (color: string) => {
          const rgb = color.match(/[\d.]+/g)!.slice(0, 3).map(Number).map(value => {
            const v = value / 255
            return v <= .04045 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4
          })
          return rgb[0] * .2126 + rgb[1] * .7152 + rgb[2] * .0722
        }
        const style = getComputedStyle(element)
        const a = luminance(style.color), b = luminance(style.backgroundColor)
        return (Math.max(a, b) + .05) / (Math.min(a, b) + .05)
      })
      expect(ratio).toBeGreaterThanOrEqual(4.5)
    }
    await action.click()
    const ratio = await page.getByLabel('Story title').evaluate(element => {
      const luminance = (color: string) => {
        const rgb = color.match(/[\d.]+/g)!.slice(0, 3).map(Number).map(value => {
          const v = value / 255
          return v <= .04045 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4
        })
        return rgb[0] * .2126 + rgb[1] * .7152 + rgb[2] * .0722
      }
      const style = getComputedStyle(element)
      const a = luminance(style.borderColor), b = luminance(style.backgroundColor)
      return (Math.max(a, b) + .05) / (Math.min(a, b) + .05)
    })
    expect(ratio).toBeGreaterThanOrEqual(3)
    await page.keyboard.press('Escape')
    await page.getByRole('heading', { level: 1 }).hover()
  }
})
