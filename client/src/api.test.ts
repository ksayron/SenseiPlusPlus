import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api, setAuthenticatedOwner, resetCsrf } from './api'

const storage = new Map<string, string>()

Object.defineProperty(globalThis, 'localStorage', {
  value: {
    getItem: (key: string) => storage.get(key) ?? null,
    setItem: (key: string, value: string) => storage.set(key, value),
    removeItem: (key: string) => storage.delete(key),
    clear: () => storage.clear(),
  },
  configurable: true,
})

describe('API transport', () => {
  beforeEach(() => {
    storage.clear()
    setAuthenticatedOwner('')
    resetCsrf()
    vi.restoreAllMocks()
  })

  it('parses successful JSON responses', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(
      JSON.stringify({ status: 'healthy' }),
      { status: 200, headers: { 'Content-Type': 'application/json' } },
    )))

    await expect(api.health()).resolves.toEqual({ status: 'healthy' })
  })

  it('surfaces RFC Problem Details messages', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(
      JSON.stringify({ title: 'Validation failed', detail: 'The key is required.' }),
      { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
    )))

    await expect(api.concepts.listPage()).rejects.toMatchObject({
      name: 'ApiError',
      message: 'The key is required.',
      status: 400,
    })
  })

  it('sends an account precondition with cookies without selecting an owner', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response('[]', {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }))
    vi.stubGlobal('fetch', fetchMock)
    setAuthenticatedOwner('7fc2c0e0-f92f-4c36-8473-0380cd0e6910')

    await api.episodes.listPage()

    const headers = (fetchMock.mock.calls[0][1] as RequestInit).headers as Headers
    expect(headers.has('X-Owner-Id')).toBe(false)
    expect(headers.get('X-Expected-Owner')).toBe('7fc2c0e0-f92f-4c36-8473-0380cd0e6910')
    expect(fetchMock.mock.calls[0][1].credentials).toBe('include')
  })

  it('sends the opaque version token as a strong If-Match ETag', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({ token: 'csrf-token' }), { status: 200 })).mockResolvedValueOnce(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)

    await api.concepts.deactivate({ id: 'concept-id', versionToken: 'opaque-token' } as never)

    const headers = (fetchMock.mock.calls[1][1] as RequestInit).headers as Headers
    expect(headers.get('X-CSRF-Token')).toBe('csrf-token')
    expect(headers.get('If-Match')).toBe('"opaque-token"')
  })
})
