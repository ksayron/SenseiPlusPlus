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
const OWNER_KEY = 'sensei.owner-id'
const DEFAULT_OWNER_ID = '61c9ed1a-2333-46a2-9d48-3a840228e61b'

export const getOwnerId = () => {
  const stored = localStorage.getItem(OWNER_KEY)
  if (stored) return stored
  localStorage.setItem(OWNER_KEY, DEFAULT_OWNER_ID)
  return DEFAULT_OWNER_ID
}

export const setOwnerId = (id: string) => localStorage.setItem(OWNER_KEY, id)

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

async function request<T>(path: string, init?: RequestInit, ownerScoped = false): Promise<T> {
  const headers = new Headers(init?.headers)
  if (init?.body) headers.set('Content-Type', 'application/json')
  if (ownerScoped) headers.set('X-Owner-Id', getOwnerId())

  const response = await fetch(`${API_BASE}${path}`, { ...init, headers })
  if (!response.ok) {
    let message = `Request failed (${response.status})`
    let problem: ApiProblem | undefined
    try {
      problem = await response.json() as ApiProblem
      message = problem.detail || problem.title || message
    } catch {
      // Keep the useful status fallback for empty responses.
    }
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
