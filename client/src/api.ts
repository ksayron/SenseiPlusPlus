import type {
  Concept,
  EvidenceObservation,
  EvidenceStatus,
  ExperienceEntry,
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

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

async function request<T>(path: string, init?: RequestInit, ownerScoped = false): Promise<T> {
  const headers = new Headers(init?.headers)
  if (init?.body) headers.set('Content-Type', 'application/json')
  if (ownerScoped) headers.set('X-Owner-Id', getOwnerId())

  const response = await fetch(`${API_BASE}${path}`, { ...init, headers })
  if (!response.ok) {
    let message = `Request failed (${response.status})`
    try {
      const problem = await response.json() as { title?: string; detail?: string }
      message = problem.detail || problem.title || message
    } catch {
      // Keep the useful status fallback for empty responses.
    }
    throw new ApiError(message, response.status)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

const json = (body: unknown): RequestInit => ({ body: JSON.stringify(body) })

export const api = {
  health: () => request<{ status: string }>('/health'),
  users: {
    list: () => request<UserAccount[]>('/api/v1/identity/users/'),
    create: (body: Omit<UserAccount, 'id' | 'createdAt' | 'version'>) =>
      request<UserAccount>('/api/v1/identity/users/', { method: 'POST', ...json(body) }),
  },
  concepts: {
    list: () => request<Concept[]>('/api/v1/learning/concepts/?includeInactive=true'),
    create: (body: Pick<Concept, 'key' | 'name' | 'description' | 'locale' | 'difficulty'>) =>
      request<Concept>('/api/v1/learning/concepts/', { method: 'POST', ...json(body) }),
    update: (concept: Concept, body: Pick<Concept, 'name' | 'description' | 'locale' | 'difficulty'>) =>
      request<Concept>(`/api/v1/learning/concepts/${concept.id}`, {
        method: 'PUT', ...json({ ...body, expectedVersion: concept.version }),
      }),
    deactivate: (concept: Concept) =>
      request<void>(`/api/v1/learning/concepts/${concept.id}?expectedVersion=${concept.version}`, { method: 'DELETE' }),
  },
  episodes: {
    list: () => request<WorkEpisode[]>('/api/v1/work/episodes/?includeArchived=true', undefined, true),
    create: (body: Pick<WorkEpisode, 'title' | 'setting' | 'eventDate' | 'role' | 'summary'>) =>
      request<WorkEpisode>('/api/v1/work/episodes/', { method: 'POST', ...json(body) }, true),
    update: (episode: WorkEpisode, body: Pick<WorkEpisode, 'title' | 'setting' | 'eventDate' | 'role' | 'summary'>) =>
      request<WorkEpisode>(`/api/v1/work/episodes/${episode.id}`, {
        method: 'PUT', ...json({ ...body, expectedVersion: episode.version }),
      }, true),
    archive: (episode: WorkEpisode) =>
      request<void>(`/api/v1/work/episodes/${episode.id}?expectedVersion=${episode.version}`, { method: 'DELETE' }, true),
  },
  evidence: {
    list: () => request<EvidenceObservation[]>('/api/v1/evidence/observations/?includeInactive=true', undefined, true),
    create: (body: Omit<EvidenceObservation, 'id' | 'ownerId' | 'status' | 'observedAt' | 'version'>) =>
      request<EvidenceObservation>('/api/v1/evidence/observations/', { method: 'POST', ...json(body) }, true),
    status: (item: EvidenceObservation, status: EvidenceStatus) =>
      request<EvidenceObservation>(`/api/v1/evidence/observations/${item.id}/status`, {
        method: 'PUT', ...json({ status, expectedVersion: item.version }),
      }, true),
    withdraw: (item: EvidenceObservation) =>
      request<void>(`/api/v1/evidence/observations/${item.id}?expectedVersion=${item.version}`, { method: 'DELETE' }, true),
  },
  experience: {
    list: () => request<ExperienceEntry[]>('/api/v1/experience/entries/?includeArchived=true', undefined, true),
    create: (body: Record<string, unknown>) =>
      request<ExperienceEntry>('/api/v1/experience/entries/', { method: 'POST', ...json(body) }, true),
    revise: (entry: ExperienceEntry, body: Record<string, unknown>) =>
      request<ExperienceEntry>(`/api/v1/experience/entries/${entry.id}`, {
        method: 'PUT', ...json({ ...body, expectedVersion: entry.version }),
      }, true),
    approve: (entry: ExperienceEntry, revisionNumber: number) =>
      request<ExperienceEntry>(`/api/v1/experience/entries/${entry.id}/revisions/${revisionNumber}/approval`, {
        method: 'POST', ...json({ expectedVersion: entry.version }),
      }, true),
    archive: (entry: ExperienceEntry) =>
      request<void>(`/api/v1/experience/entries/${entry.id}?expectedVersion=${entry.version}`, { method: 'DELETE' }, true),
  },
}
