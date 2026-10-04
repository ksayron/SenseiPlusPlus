import { expect, test } from '@playwright/test'

const entryId = '6cf179da-6bf6-4855-83a5-013de22ab566'
const imported = { eventId: 'a26bf0db-0f12-42c1-a1ac-56d77575d7b8', schemaVersion: 1, setting: 'PersonalProject', eventDate: '2026-10-03', title: 'Investigated retry behavior', context: 'A service needed reliable retries', role: 'Developer', actions: 'Investigated the failure', alternatives: 'Considered retry alternatives', outcome: 'Outcome remains unknown', uncertainty: 'Not measured', assistance: 'Peer review', conceptIds: [], claimProvenance: 'client_reported' }

test.beforeEach(async ({ page }) => {
  await page.route('**/health', route => route.fulfill({ json: { status: 'healthy' } }))
  await page.route('**/api/v1/**', route => {
    const path = new URL(route.request().url()).pathname.replace(/\/$/, '')
    if (path === '/api/v1/auth/me') return route.fulfill({ json: { id: 'account-1', email: 'agent@example.test', displayName: 'Agent tester' } })
    if (path === '/api/v1/auth/csrf') return route.fulfill({ json: { token: 'csrf-fixture' } })
    if (path === '/api/v1/auth/cli-consent/fixture') return route.fulfill({ json: { clientName: 'Sensei++ local evidence agent', scopes: ['concepts:read', 'professional-events:write', 'professional-events:read', 'offline_access'], account: 'account-1' } })
    if (path === '/api/v1/auth/connections') return route.fulfill({ json: [{ id: 'connection-1', createdAt: '2026-10-03T10:00:00Z', expiresAt: '2026-11-03T10:00:00Z', revoked: false }] })
    if (path === '/api/v1/auth/reauthenticate' || path.endsWith('/revoke')) return route.fulfill({ status: 204 })
    if (path === `/api/v1/experience/entries/${entryId}/import`) return route.fulfill({ json: imported })
    if (path === '/api/v1/experience/entries') return route.fulfill({ json: { items: [{ id: entryId, ownerId: 'account-1', isArchived: false, createdAt: '2026-10-03T10:00:00Z', versionToken: 'v1', revisions: [{ ...imported, number: 1, impactState: 'Unknown', approvalState: 'Draft', createdAt: '2026-10-03T10:00:00Z', approvedAt: null }] }], nextCursor: null } })
    return route.fulfill({ json: { items: [], nextCursor: null } })
  })
})

test('consent makes permissions and the current account inspectable', async ({ page }, info) => {
  await page.goto('/agent/authorize?request=fixture')
  await expect(page.getByRole('heading', { name: 'Connect your local evidence agent' })).toBeVisible()
  await expect(page.getByText('agent@example.test', { exact: true })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Allow this connection' })).toBeEnabled()
  await expect(page.getByText('Watching stays local.', { exact: false })).toBeVisible()
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBeTruthy()
  await page.screenshot({ path: `.tmp/agent-auth-visuals/${info.project.name}-consent.png`, fullPage: true })
})

test('connection revocation requires password confirmation', async ({ page }) => {
  await page.goto('/connections')
  await page.getByRole('button', { name: 'Revoke connection', exact: true }).click()
  await page.getByLabel('Confirm your account password').fill('fixture password')
  await page.getByRole('button', { name: 'Confirm revocation', exact: true }).click()
  await expect(page.getByText('Revoked', { exact: true })).toBeVisible()
  await expect(page.getByLabel('Confirm your account password')).toHaveCount(0)
})

test('original contribution retains assistance uncertainty and draft status', async ({ page }, info) => {
  await page.goto(`/experience?entry=${entryId}`)
  await expect(page.getByText('Client reported · Not assessed', { exact: false })).toBeVisible()
  await page.locator('.imported-contribution summary').click()
  await expect(page.getByText('Peer review', { exact: true })).toBeVisible()
  await expect(page.getByText('Not measured', { exact: true })).toBeVisible()
  await expect(page.getByText('Draft', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Revise', exact: true }).click()
  await expect(page.getByLabel('Uncertainty', { exact: true })).toHaveValue('Not measured')
  await expect(page.getByLabel('Assistance', { exact: true })).toHaveValue('Peer review')
  await page.screenshot({ path: `.tmp/agent-auth-visuals/${info.project.name}-revision.png`, fullPage: true })
})
