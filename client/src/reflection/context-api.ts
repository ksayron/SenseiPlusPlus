import { request } from '../api'
import type { PageEnvelope, WorkEpisode } from '../types'

export interface SelectedExcerpt { label: string; versionLabel: string; text: string }
export interface ContextSource { kind: 'ManualWeb'; excerpts: SelectedExcerpt[]; intent: string; constraints: string }
export type DataScope = 'Restricted' | 'PublicSynthetic'
export interface DisclosureManifest {
  schemaVersion: number; mode: 'LocalOnly' | 'PublicProviderPreview'; dataScope: DataScope
  sourceFingerprint: string; selectedContent: ContextSource
  recipients: { kind: string; name: string; policyUrl: string; policyCheckedAt: string | null }[]
  route: { revision: string; modelId: string; providerName: string; digest: string } | null
  notice: string
}
export interface ContextPreview { sourceFingerprint: string; fingerprintVersion: string; manifest: DisclosureManifest; previewToken: string }
export interface ContextWorkspace { episodeId: string; currentSnapshotId: string | null; acceptedInterpretationId: string | null; isArchived: boolean; versionToken: string }
export interface ContextSnapshot { id: string; episodeId: string; number: number; source: ContextSource; sourceFingerprint: string; fingerprintVersion: string; dataScope: DataScope; disclosure: DisclosureManifest; disclosureDigest: string; createdAt: string }
export interface InterpretationText { intent: string; constraints: string; missingInformation: string; correction: string }
export interface InterpretationRevision { id: string; snapshotId: string; number: number; supersedesId: string | null; origin: 'User' | 'Fixture' | 'Provider'; text: InterpretationText; provenanceJson: string | null; createdAt: string }
export interface ContextConfirmation { id: string; snapshotId: string; interpretationId: string; sourceFingerprint: string; createdAt: string }
export interface ContextMutation<T> { item: T; currentSnapshotId: string | null; acceptedInterpretationId: string | null; versionToken: string }
const root = (episode: string) => `/api/v1/work/episodes/${episode}/context`
const page = (path: string, cursor?: string) => `${path}?${new URLSearchParams({ limit: '25', ...(cursor ? { cursor } : {}) })}`
const post = <T>(path: string, body: unknown, token?: string) => request<T>(path, {
  method: 'POST', body: JSON.stringify(body), ...(token ? { headers: { 'If-Match': `"${token}"` } } : {}),
}, true)
export const contextApi = {
  episode: (id: string) => request<WorkEpisode>(`/api/v1/work/episodes/${id}`, undefined, true),
  workspace: (id: string) => request<ContextWorkspace>(root(id), undefined, true),
  snapshots: (id: string, cursor?: string) => request<PageEnvelope<ContextSnapshot>>(page(`${root(id)}/snapshots`, cursor), undefined, true),
  snapshot: (id: string, snapshot: string) => request<ContextSnapshot>(`${root(id)}/snapshots/${snapshot}`, undefined, true),
  interpretations: (id: string, snapshot: string, cursor?: string) => request<PageEnvelope<InterpretationRevision>>(page(`${root(id)}/snapshots/${snapshot}/interpretations`, cursor), undefined, true),
  confirmations: (id: string, snapshot: string, cursor?: string) => request<PageEnvelope<ContextConfirmation>>(page(`${root(id)}/snapshots/${snapshot}/confirmations`, cursor), undefined, true),
  preview: (id: string, source: ContextSource, dataScope: DataScope) => post<ContextPreview>(`${root(id)}/preview`, { source, dataScope }),
  save: (id: string, token: string, body: { operationId: string; source: ContextSource; dataScope: DataScope; previewToken: string }) => post<ContextMutation<ContextSnapshot>>(`${root(id)}/snapshots`, body, token),
  revise: (id: string, snapshot: string, token: string, body: { operationId: string; supersedesId: string | null; text: InterpretationText }) => post<ContextMutation<InterpretationRevision>>(`${root(id)}/snapshots/${snapshot}/interpretations`, body, token),
  confirm: (id: string, snapshot: string, token: string, body: { operationId: string; interpretationId: string; sourceFingerprint: string }) => post<ContextMutation<ContextConfirmation>>(`${root(id)}/snapshots/${snapshot}/confirmation`, body, token),
}
