import type {
  ApiProblem,
  Concept,
  EvidenceObservation,
  EvidenceStatus,
  ExperienceEntry,
  PageEnvelope,
  UserAccount,
  WorkEpisode,
} from './types'

const API_BASE = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')
let accountId = ''
let csrfToken: Promise<string> | null = null
export const getOwnerId = () => accountId
export const setAuthenticatedOwner = (id: string) => {
  if (accountId === id) return
  accountId = id
  csrfToken = null
  if (typeof window !== 'undefined') window.dispatchEvent(new Event('sensei-owner-change'))
}
export const resetCsrf = () => { csrfToken = null }
export const securityToken = () => {
  if (!csrfToken) csrfToken = fetch(`${API_BASE}/api/v1/auth/csrf`, { credentials: 'include' })
    .then(async response => {
      if (!response.ok) throw new Error('Could not load a security token. Retry connecting.')
      return (await response.json() as { token: string }).token
    }).catch(error => { csrfToken = null; throw error })
  return csrfToken
}

export class ApiError extends Error {
  status: number
  code?: string
  traceId?: string

  constructor(message: string, status: number, problem?: ApiProblem) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = problem?.code
    this.traceId = problem?.traceId
  }
}

export async function request<T>(path: string, init?: RequestInit, ownerScoped = false): Promise<T> {
  const headers = new Headers(init?.headers)
  if (init?.body) headers.set('Content-Type', 'application/json')
  const requestOwner = ownerScoped ? getOwnerId() : null
  if (accountId && (ownerScoped || !path.startsWith('/api/v1/auth/') || path.endsWith('/logout'))) headers.set('X-Expected-Owner', accountId)
  if (init?.method && !['GET', 'HEAD', 'OPTIONS'].includes(init.method.toUpperCase())) headers.set('X-CSRF-Token', await securityToken())
  if (requestOwner && requestOwner !== getOwnerId()) throw new ApiError('Account changed. Reload this page.', 409)

  const response = await fetch(`${API_BASE}${path}`, { ...init, headers, credentials: 'include' })
  if (requestOwner && requestOwner !== getOwnerId()) throw new ApiError('Owner changed. Reload this page.', 409)
  if (!response.ok) {
    let message = `Request failed (${response.status})`
    let problem: ApiProblem | undefined
    try {
      problem = await response.json() as ApiProblem
      message = problem.detail || problem.title || message
    } catch {
      // Keep the useful status fallback for empty responses.
    }
    if (response.status === 401 && ownerScoped && typeof window !== 'undefined') window.dispatchEvent(new Event('sensei-session-expired'))
    if (problem?.code === 'antiforgery_failed') resetCsrf()
    throw new ApiError(message, response.status, problem)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

const json = (body: unknown): RequestInit => ({ body: JSON.stringify(body) })
const versioned = (versionToken: string, init: RequestInit = {}): RequestInit => ({
  ...init,
  headers: { ...Object.fromEntries(new Headers(init.headers)), 'If-Match': `"${versionToken}"` },
})
const pagePath = (path: string, query: Record<string, string>, cursor?: string) => {
  const parameters = new URLSearchParams({ ...query, limit: '25' })
  if (cursor) parameters.set('cursor', cursor)
  return `${path}?${parameters}`
}

export const api = {
  health: () => request<{ status: string }>('/health'),
  users: {
    listPage: (cursor?: string) => request<PageEnvelope<UserAccount>>(pagePath('/api/v1/identity/users/', {}, cursor)),
    create: (body: Omit<UserAccount, 'id' | 'createdAt' | 'versionToken'>) =>
      request<UserAccount>('/api/v1/identity/users/', { method: 'POST', ...json(body) }),
  },
  concepts: {
    listPage: (cursor?: string) => request<PageEnvelope<Concept>>(pagePath('/api/v1/learning/concepts/', { includeInactive: 'true' }, cursor)),
    create: (body: Pick<Concept, 'key' | 'name' | 'description' | 'locale' | 'difficulty'>) =>
      request<Concept>('/api/v1/learning/concepts/', { method: 'POST', ...json(body) }),
    update: (concept: Concept, body: Pick<Concept, 'name' | 'description' | 'locale' | 'difficulty'>) =>
      request<Concept>(`/api/v1/learning/concepts/${concept.id}`, versioned(concept.versionToken, {
        method: 'PUT', ...json(body),
      })),
    deactivate: (concept: Concept) =>
      request<void>(`/api/v1/learning/concepts/${concept.id}`, versioned(concept.versionToken, { method: 'DELETE' })),
  },
  episodes: {
    listPage: (cursor?: string) => request<PageEnvelope<WorkEpisode>>(pagePath('/api/v1/work/episodes/', { includeArchived: 'true' }, cursor), undefined, true),
    create: (body: Pick<WorkEpisode, 'title' | 'setting' | 'eventDate' | 'role' | 'summary'>) =>
      request<WorkEpisode>('/api/v1/work/episodes/', { method: 'POST', ...json(body) }, true),
    update: (episode: WorkEpisode, body: Pick<WorkEpisode, 'title' | 'setting' | 'eventDate' | 'role' | 'summary'>) =>
      request<WorkEpisode>(`/api/v1/work/episodes/${episode.id}`, versioned(episode.versionToken, {
        method: 'PUT', ...json(body),
      }), true),
    archive: (episode: WorkEpisode) =>
      request<void>(`/api/v1/work/episodes/${episode.id}`, versioned(episode.versionToken, { method: 'DELETE' }), true),
  },
  evidence: {
    listPage: (cursor?: string) => request<PageEnvelope<EvidenceObservation>>(pagePath('/api/v1/evidence/observations/', { includeInactive: 'true' }, cursor), undefined, true),
    create: (body: Omit<EvidenceObservation, 'id' | 'ownerId' | 'status' | 'observedAt' | 'versionToken'>) =>
      request<EvidenceObservation>('/api/v1/evidence/observations/', { method: 'POST', ...json(body) }, true),
    status: (item: EvidenceObservation, status: EvidenceStatus) =>
      request<EvidenceObservation>(`/api/v1/evidence/observations/${item.id}/status`, versioned(item.versionToken, {
        method: 'PUT', ...json({ status }),
      }), true),
    withdraw: (item: EvidenceObservation) =>
      request<void>(`/api/v1/evidence/observations/${item.id}`, versioned(item.versionToken, { method: 'DELETE' }), true),
  },
  experience: {
    listPage: (cursor?: string) => request<PageEnvelope<ExperienceEntry>>(pagePath('/api/v1/experience/entries/', { includeArchived: 'true' }, cursor), undefined, true),
    create: (body: Record<string, unknown>) =>
      request<ExperienceEntry>('/api/v1/experience/entries/', { method: 'POST', ...json(body) }, true),
    revise: (entry: ExperienceEntry, body: Record<string, unknown>) =>
      request<ExperienceEntry>(`/api/v1/experience/entries/${entry.id}`, versioned(entry.versionToken, {
        method: 'PUT', ...json(body),
      }), true),
    approve: (entry: ExperienceEntry, revisionNumber: number) =>
      request<ExperienceEntry>(`/api/v1/experience/entries/${entry.id}/revisions/${revisionNumber}/approval`,
        versioned(entry.versionToken, { method: 'POST' }), true),
    archive: (entry: ExperienceEntry) =>
      request<void>(`/api/v1/experience/entries/${entry.id}`, versioned(entry.versionToken, { method: 'DELETE' }), true),
  },
}
