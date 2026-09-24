import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api, setOwnerId } from './api'

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

  it('adds the temporary owner header only to owner-scoped calls', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response('[]', {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }))
    vi.stubGlobal('fetch', fetchMock)
    setOwnerId('7fc2c0e0-f92f-4c36-8473-0380cd0e6910')

    await api.episodes.listPage()

    const headers = (fetchMock.mock.calls[0][1] as RequestInit).headers as Headers
    expect(headers.get('X-Owner-Id')).toBe('7fc2c0e0-f92f-4c36-8473-0380cd0e6910')
  })

  it('sends the opaque version token as a strong If-Match ETag', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)

    await api.concepts.deactivate({ id: 'concept-id', versionToken: 'opaque-token' } as never)

    const headers = (fetchMock.mock.calls[0][1] as RequestInit).headers as Headers
    expect(headers.get('If-Match')).toBe('"opaque-token"')
  })
})
