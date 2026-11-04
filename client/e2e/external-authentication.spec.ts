import { test, expect } from '@playwright/test'

// UI-only doubles. Real OAuth/OIDC callback validation lives in PostgreSQL integration tests.
const profiles = [{ provider: 'GitHub', displayName: 'Connected developer', username: 'connected-developer', profileUrl: 'https://github.com/connected-developer', connectedAt: '2026-10-04T10:00:00Z', refreshedAt: '2026-10-04T10:00:00Z', enabled: true }]

test('provider sign-in preserves an encoded learning search', async ({ page }) => {
  const returnPath = '/learn?search=%D1%82%D0%B5%D1%81%D1%82'
  await page.route('**/api/v1/auth/providers', route => route.fulfill({ json: [{ provider: 'Google', displayName: 'Google' }] }))
  await page.route('**/api/v1/auth/external/Google/start', async route => {
    expect(route.request().postDataJSON()).toMatchObject({ purpose: 'signin', returnPath })
    await route.fulfill({ json: { redirect: '/api/v1/auth/external/challenge?flow=' + 'C'.repeat(64) } })
  })
  await page.route('**/api/v1/auth/external/challenge?*', route => route.fulfill({ status: 302, headers: { Location: '/auth/result?error=external_cancelled' } }))
  await page.goto(returnPath)
  await page.getByRole('button', { name: 'Continue with Google' }).click()
  await expect(page.getByRole('alert')).toContainText('cancelled')
})

test('provider buttons preserve the requested route and report cancelled and conflicting sign-in', async ({ page }, info) => {
  await page.route('**/api/v1/auth/providers', route => route.fulfill({ json: [{ provider: 'Google', displayName: 'Google' }, { provider: 'GitHub', displayName: 'GitHub' }] }))
  await page.route('**/api/v1/auth/external/Google/start', async route => {
    expect(route.request().postDataJSON()).toMatchObject({ purpose: 'signin', returnPath: '/agent/authorize?request=example-ticket' })
    expect(route.request().headers()['x-csrf-token']).toBeTruthy()
    await route.fulfill({ json: { redirect: '/api/v1/auth/external/challenge?flow=' + 'A'.repeat(64) } })
  })
  await page.route('**/api/v1/auth/external/challenge?*', route => route.fulfill({ status: 302, headers: { Location: '/auth/result?error=external_cancelled' } }))
  await page.goto('/agent/authorize?request=example-ticket')
  await expect(page.getByRole('button', { name: 'Continue with Google' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Continue with GitHub' })).toBeVisible()
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
  await page.screenshot({ path: `test-results/external-signin-${info.project.name}.png`, fullPage: true })
  await page.getByRole('button', { name: 'Night appearance' }).click()
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'coastal-night')
  await expect(page.getByRole('button', { name: 'Continue with GitHub' })).toBeVisible()
  await page.screenshot({ path: `test-results/external-signin-night-${info.project.name}.png`, fullPage: true })
  await page.getByRole('button', { name: 'Continue with Google' }).click()
  await expect(page.getByRole('alert')).toContainText('cancelled')
  await page.goto('/auth/result?error=email_conflict')
  await expect(page.getByRole('alert')).toContainText('Sign in to that account')
  await expect(page.getByRole('link', { name: 'Connections', exact: true })).toBeVisible()
})

test('completion refreshes antiforgery and updates another tab to the current account', async ({ page, context }) => {
  const register = async (displayName: string) => {
    const csrf = await (await page.request.get('/api/v1/auth/csrf')).json()
    const email = `external-tab-${crypto.randomUUID()}@example.test`
    const response = await page.request.post('/api/v1/auth/register', { headers: { 'X-CSRF-Token': csrf.token }, data: { email, password: 'Browser-Test-Password1!', displayName } })
    expect(response.status()).toBe(201)
    return email
  }
  const initialEmail = await register('Initial tab account')
  await page.goto('/learn')
  const other = await context.newPage()
  await other.goto('/learn')
  await expect(other.getByLabel('Signed in as Initial tab account')).toHaveAttribute('title', initialEmail)
  await other.evaluate(() => {
    const channel = new BroadcastChannel('sensei-account')
    channel.onmessage = () => document.documentElement.dataset.accountNotified = 'true'
  })
  // Simulate the shared session cookie set by a successful middleware completion.
  const email = await register('Returned tab account')
  const renewedCsrf = page.waitForResponse(response => response.url().endsWith('/api/v1/auth/csrf') && response.status() === 200)
  await page.goto('/auth/result?returnPath=%2Flearn')
  expect((await renewedCsrf).status()).toBe(200)
  await expect(page.getByLabel('Signed in as Returned tab account')).toHaveAttribute('title', email)
  await expect(other.locator('html')).toHaveAttribute('data-account-notified', 'true')
  await expect(other.getByLabel('Signed in as Returned tab account')).toHaveAttribute('title', email)
})

test('connections expose profiles and provider confirmation for a passwordless account', async ({ page }, info) => {
  const email = `external-ui-${crypto.randomUUID()}@example.test`
  const csrf = await (await page.request.get('/api/v1/auth/csrf')).json()
  const created = await page.request.post('/api/v1/auth/register', { headers: { 'X-CSRF-Token': csrf.token }, data: { email, password: 'Browser-Test-Password1!', displayName: 'Connected developer' } })
  expect(created.status()).toBe(201)
  await page.route('**/api/v1/auth/providers', route => route.fulfill({ json: [{ provider: 'Google', displayName: 'Google' }, { provider: 'GitHub', displayName: 'GitHub' }] }))
  await page.route('**/api/v1/auth/methods', route => route.fulfill({ json: { hasPassword: false, profiles } }))
  await page.route('**/api/v1/auth/connections', route => route.fulfill({ json: [{ id: '8d2f0081-3e4a-4986-bf86-9a1268c2da57', createdAt: '2026-10-04T10:00:00Z', expiresAt: '2026-11-04T10:00:00Z', revoked: false }] }))
  let recent = false
  await page.route('**/api/v1/auth/reauthentication', route => route.fulfill({ status: recent ? 204 : 403 }))
  await page.route('**/api/v1/auth/external/GitHub/start', async route => {
    expect(route.request().postDataJSON()).toMatchObject({ purpose: 'reauthenticate', returnPath: '/connections' })
    expect(route.request().headers()['x-expected-owner']).toBeTruthy()
    await route.fulfill({ json: { redirect: '/api/v1/auth/external/challenge?flow=' + 'B'.repeat(64) } })
  })
  await page.route('**/api/v1/auth/external/challenge?*', route => { recent = true; return route.fulfill({ status: 302, headers: { Location: '/auth/result?returnPath=%2Fconnections' } }) })
  await page.goto('/connections')
  await expect(page.getByRole('heading', { name: 'Sign-in profiles' })).toBeVisible()
  await expect(page.getByRole('link', { name: 'View GitHub profile' })).toHaveAttribute('href', 'https://github.com/connected-developer')
  await expect(page.getByText('Profile connection only.', { exact: false })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Connect Google' })).toBeVisible()
  await page.screenshot({ path: `test-results/external-connections-${info.project.name}.png`, fullPage: true })
  await page.getByRole('button', { name: 'Revoke connection', exact: true }).click()
  await page.getByRole('button', { name: 'Confirm change' }).click()
  await expect(page.getByRole('heading', { name: 'Confirm your account' })).toBeVisible()
  await expect(page.getByLabel('Account password')).toHaveCount(0)
  await page.getByRole('button', { name: 'Confirm with GitHub' }).click()
  await expect(page.getByRole('heading', { name: 'Connections', exact: true })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Revoke evidence agent' })).toHaveCount(0)
  await page.getByRole('button', { name: 'Disconnect GitHub' }).click()
  await page.route('**/api/v1/auth/external/GitHub/disconnect', route => route.fulfill({ status: 409, json: { code: 'last_signin_method', detail: 'Connect another enabled provider or keep a password before disconnecting this sign-in method.' } }))
  await page.getByRole('button', { name: 'Confirm change' }).click()
  await expect(page.getByRole('alert')).toContainText('Connect another enabled provider')
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
})
