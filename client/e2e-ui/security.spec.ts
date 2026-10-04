import { test, expect } from '@playwright/test'

const hostile = '<img src=x onerror="window.senseiInjected=1"><script>window.senseiInjected=1</script>'
const concept = { id: 'security-concept', key: 'security', name: 'Security fixture', description: 'Synthetic', locale: 'en', difficulty: 'Beginner', isActive: true }
const feedback = { attemptId: 'saved-attempt', resultId: 'saved-result', score: 1, selfReview: null, answer: { selected: ['a'] }, note: hostile,
  assisted: false, answerAware: false, previousAttemptId: null, explanation: 'Saved deterministic feedback', referenceAnswer: 'Authored reference', receivedAt: '2026-10-03T12:00:00Z' }

test.beforeEach(async ({ page }) => {
  await page.route('**/health', route => route.fulfill({ json: { status: 'healthy' } }))
  await page.route('**/api/v1/**', route => {
    const path = new URL(route.request().url()).pathname
    const single: Record<string, unknown> = {
      '/api/v1/auth/me': { id: 'security-owner', email: 'security@example.test', displayName: 'Synthetic security test' },
      '/api/v1/auth/csrf': { token: 'csrf' },
      '/api/v1/learning/topics/security-concept': { concept, relations: [], materialVersions: ['security-material'], availableFamilies: 1 },
      '/api/v1/evidence/knowledge/security-concept': { familyCount: 0, assistedCount: 0, qualifyingFamilies: 0, qualifyingSessions: 0, observationIds: [] },
      '/api/v1/learning/materials/security-material': { id: 'security-material', locale: 'en', blocks: [
        { kind: 'paragraph', text: hostile }, { kind: 'code', text: hostile, language: 'html' },
        { kind: 'link', text: 'Executable link', url: 'javascript:window.senseiInjected=1' },
        { kind: 'link', text: 'Credential link', url: 'https://user:password@example.test' },
        { kind: 'image', text: '', alt: 'Unsafe image', url: 'data:image/svg+xml,<svg onload="window.senseiInjected=1"/>' },
        { kind: 'link', text: 'Safe reference', url: 'https://example.test/reference' },
      ] },
      '/api/v1/learning/sessions/security-session': { id: 'security-session', state: 'Active', position: 0, requestedCount: 3, actualCount: 1,
        returnContext: '/learn', versionToken: 'djE6MQ', createdAt: '2026-10-03T12:00:00Z', items: [{ id: 'item', exerciseVersionId: 'exercise', conceptId: concept.id,
          position: 0, type: 'MultipleChoice', locale: 'en', prompt: [{ kind: 'paragraph', text: 'Synthetic question' }],
          interaction: { choices: [{ id: 'a', text: 'A' }], multiple: false, oneToOne: true }, draft: { answer: null, note: '' },
          outcome: 'Answered', hintUsed: false, referenceUsed: false, revealed: false, hintCount: 0, materialVersionId: null, feedback: [feedback] }] },
      '/api/v1/learning/attempts/saved-attempt/assessment-preview': { available: true, reason: 'Synthetic fixture only', digest: 'synthetic', disclosure: {
        systemPrompt: hostile, input: { note: hostile }, schema: '{}', model: 'fixture', provider: 'Fixture', origin: 'Fixture',
        routerPrivacyPolicy: 'javascript:window.senseiInjected=1', providerPrivacyPolicy: 'https://user:password@example.test',
        routerPrivacyReviewedAt: '2026-10-03T12:00:00Z', providerPrivacyReviewedAt: '2026-10-03T12:00:00Z' } },
      '/api/v1/learning/attempts/saved-attempt/assessments': { items: [{ id: 'job', state: 'Succeeded', versionToken: 'djE6MQ', failureCode: null, notBefore: null,
        feedback: { revision: 1, scope: 'Exploratory', origin: 'Fixture', templateVersion: 'test', contractVersion: 'test',
          provenance: { actualModel: null, actualProvider: null }, output: { contextSufficiency: 'sufficient',
            criteria: [{ id: 'reasoning', judgment: 'supported', explanation: hostile, answerQuote: hostile, referenceIds: [] }],
            missingAssumptions: [hostile], nextActions: [hostile] } } }], nextCursor: null },
    }
    if (path === '/api/v1/learning/material-views') return route.fulfill({ status: 204 })
    return route.fulfill({ json: single[path] ?? { items: [], nextCursor: null } })
  })
})

test('hostile material stays text and unsafe URLs cannot navigate', async ({ page }) => {
  await page.goto('/learn/topics/security-concept?section=material')
  const material = page.locator('.material-blocks')
  await expect(material).toContainText(hostile)
  await expect(material.locator('script, img, [onerror]')).toHaveCount(0)
  await expect(material.getByRole('link')).toHaveCount(1)
  await expect(material.getByRole('link', { name: 'Safe reference' })).toHaveAttribute('rel', 'noreferrer')
  expect(await page.evaluate(() => 'senseiInjected' in window)).toBe(false)
})

test('model explanations quotes and disclosure text stay inert', async ({ page }) => {
  await page.goto('/learn/sessions/security-session')
  await page.getByRole('button', { name: 'Optional AI feedback', exact: true }).click()
  const panel = page.getByRole('region', { name: 'Optional assessment' })
  await expect(panel).toContainText(hostile)
  await expect(panel.locator('script, img, [onerror]')).toHaveCount(0)
  await expect(panel.locator('a[href]')).toHaveCount(0)
  await expect(panel.getByRole('button', { name: 'Request optional feedback' })).toBeDisabled()
  expect(await page.evaluate(() => 'senseiInjected' in window)).toBe(false)
})
