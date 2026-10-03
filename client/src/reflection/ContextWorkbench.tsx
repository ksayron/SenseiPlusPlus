import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { ArrowDown, ArrowLeft, ArrowUp, Check, FileText, Plus, RefreshCw, ShieldCheck, X } from 'lucide-react'
import { getOwnerId } from '../api'
import { Button } from '../components/ui/button'
import { Input } from '../components/ui/input'
import { Textarea } from '../components/ui/textarea'
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '../components/ui/dialog'
import { contextApi, type ContextConfirmation, type ContextPreview, type ContextSnapshot, type ContextSource,
  type ContextWorkspace, type DataScope, type DisclosureManifest, type InterpretationRevision, type InterpretationText } from './context-api'
import type { WorkEpisode } from '../types'
import './context.css'

interface Draft { source: ContextSource; scope: DataScope; text: InterpretationText; supersedesId: string | null; operations: Map<string, string> }
const drafts = new Map<string, Draft>()
window.addEventListener('sensei-owner-change', () => drafts.clear())
const emptyText = (): InterpretationText => ({ intent: '', constraints: '', missingInformation: '', correction: '' })
const emptySource = (): ContextSource => ({ kind: 'ManualWeb', excerpts: [{ label: 'Selected excerpt', versionLabel: '', text: '' }], intent: '', constraints: '' })
const date = (value: string) => new Date(value).toLocaleString('en', { dateStyle: 'medium', timeStyle: 'short' })
function SourceView({ source }: { source: ContextSource }) {
  return <div className="context-source-view">{source.excerpts.map((part, index) => <section key={index}><h4>{index + 1}. {part.label}</h4><p className="context-meta">Source label: {part.versionLabel || 'Not supplied'}</p><pre tabIndex={0} aria-label={`Selected text: ${part.label}`}>{part.text}</pre></section>)}<h4>Declared intent</h4><p className="context-exact-text">{source.intent}</p><h4>Declared constraints</h4><p className="context-exact-text">{source.constraints || 'None supplied'}</p></div>
}
function Disclosure({ manifest }: { manifest: DisclosureManifest }) {
  return <div className="context-disclosure"><p><ShieldCheck size={16} aria-hidden="true" /><strong>{manifest.mode === 'LocalOnly' ? 'Local storage only' : 'External disclosure preview'}</strong></p><p>Classification: {manifest.dataScope === 'Restricted' ? 'Private / restricted' : 'Public synthetic example'}</p><ul>{manifest.recipients.length ? manifest.recipients.map((recipient) => <li key={recipient.kind}><strong>{recipient.name}</strong> · {recipient.kind === 'Router' ? 'Routing service' : 'Inference provider'} · <a href={recipient.policyUrl} target="_blank" rel="noreferrer">Privacy policy</a></li>) : <li>Recipient: your local Sensei++ server. External recipients: none.</li>}</ul><p>{manifest.notice}</p></div>
}
function InterpretationView({ revision }: { revision: InterpretationRevision }) {
  return <div className="context-interpretation"><p className="context-meta">Revision {revision.number} · {revision.origin === 'User' ? 'Your interpretation' : revision.origin === 'Fixture' ? 'Fixture suggestion' : 'Provider suggestion'} · {date(revision.createdAt)}</p><h4>Interpreted intent</h4><p className="context-exact-text">{revision.text.intent}</p><h4>Interpreted constraints</h4><p className="context-exact-text">{revision.text.constraints || 'None recorded'}</p><h4>Missing information</h4><p className="context-exact-text context-unknown">{revision.text.missingInformation || 'None recorded; this does not establish completeness.'}</p>{revision.text.correction && <><h4>Your correction</h4><p className="context-exact-text">{revision.text.correction}</p></>}{revision.provenanceJson && <details><summary>Interpretation provenance</summary><pre>{revision.provenanceJson}</pre></details>}</div>
}

export function ContextWorkbench({ episodeId }: { episodeId: string }) {
  const draftKey = `${getOwnerId()}:${episodeId}`
  const existing = drafts.get(draftKey)
  const [draft, setDraft] = useState<Draft>(() => existing ?? { source: emptySource(), scope: 'Restricted', text: emptyText(), supersedesId: null, operations: new Map() })
  const draftRef = useRef(draft)
  const [episode, setEpisode] = useState<WorkEpisode | null>(null)
  const [workspace, setWorkspace] = useState<ContextWorkspace | null>(null)
  const [snapshots, setSnapshots] = useState<ContextSnapshot[]>([])
  const [snapshotCursor, setSnapshotCursor] = useState<string | null>(null)
  const [snapshot, setSnapshot] = useState<ContextSnapshot | null>(null)
  const [revisions, setRevisions] = useState<InterpretationRevision[]>([])
  const [revisionCursor, setRevisionCursor] = useState<string | null>(null)
  const [confirmations, setConfirmations] = useState<ContextConfirmation[]>([])
  const [confirmationCursor, setConfirmationCursor] = useState<string | null>(null)
  const [selectedRevisionId, setSelectedRevisionId] = useState<string | null>(null)
  const [preview, setPreview] = useState<ContextPreview | null>(null)
  const [previewAccepted, setPreviewAccepted] = useState(false)
  const [confirmOpen, setConfirmOpen] = useState(false)
  const [confirmAccepted, setConfirmAccepted] = useState(false)
  const [editingSource, setEditingSource] = useState(!existing)
  const [loading, setLoading] = useState(true)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const epoch = useRef(0)
  const selected = useRef<string | null>(null)
  const initialDraft = useRef(!!existing)
  const updateDraft = (next: Draft) => { draftRef.current = next; drafts.set(draftKey, next); setDraft(next) }

  const load = useCallback(async (chosen?: string) => {
    const generation = ++epoch.current
    setLoading(true)
    try {
      const [work, journal, list] = await Promise.all([contextApi.workspace(episodeId), contextApi.episode(episodeId), contextApi.snapshots(episodeId)])
      const id = chosen ?? selected.current ?? work.currentSnapshotId
      const source = id ? list.items.find(x => x.id === id) ?? await contextApi.snapshot(episodeId, id) : null
      const [interpretations, actions] = source ? await Promise.all([contextApi.interpretations(episodeId, source.id), contextApi.confirmations(episodeId, source.id)]) : [{ items: [], nextCursor: null }, { items: [], nextCursor: null }]
      if (generation !== epoch.current) return
      setWorkspace(work); setEpisode(journal); setSnapshots(list.items); setSnapshotCursor(list.nextCursor)
      setSnapshot(source); selected.current = source?.id ?? null
      setRevisions(interpretations.items); setRevisionCursor(interpretations.nextCursor)
      setConfirmations(actions.items); setConfirmationCursor(actions.nextCursor)
      setSelectedRevisionId(interpretations.items[0]?.id ?? null)
      if (!initialDraft.current) {
        const next = { ...draftRef.current, source: source?.source ?? { ...emptySource(), excerpts: [{ label: 'Episode summary', versionLabel: journal.versionToken, text: journal.summary }] } }
        draftRef.current = next; drafts.set(draftKey, next); setDraft(next)
        initialDraft.current = true; setEditingSource(!source)
      }
      if (!source || JSON.stringify(draftRef.current.source) !== JSON.stringify(source.source)) setEditingSource(true)
      setError(null)
    } catch (failure) {
      if (generation === epoch.current) setError(failure instanceof Error ? failure.message : 'Could not load saved context. Retry loading.')
    } finally { if (generation === epoch.current) setLoading(false) }
  }, [episodeId, draftKey])
  useEffect(() => { void load(); return () => { epoch.current++ } }, [load])
  const operation = (kind: string, value: unknown) => {
    const key = `${kind}:${JSON.stringify(value)}`
    const saved = draftRef.current.operations.get(key)
    if (saved) return saved
    const id = crypto.randomUUID(); draftRef.current.operations.set(key, id); return id
  }
  const perform = async (action: () => Promise<void>) => {
    setPending(true); setError(null); setNotice(null)
    try { await action() }
    catch (failure) { setError(failure instanceof Error ? failure.message : 'Could not save. Your edits are still in this tab.') }
    finally { setPending(false) }
  }
  const editSource = (source: ContextSource, scope = draft.scope) => {
    updateDraft({ ...draft, source, scope }); setPreview(null); setPreviewAccepted(false)
  }
  const editExcerpt = (index: number, field: keyof ContextSource['excerpts'][number], value: string) => {
    editSource({ ...draft.source, excerpts: draft.source.excerpts.map((part, i) => i === index ? { ...part, [field]: value } : part) })
  }
  const moveExcerpt = (index: number, offset: number) => {
    const parts = [...draft.source.excerpts]; [parts[index], parts[index + offset]] = [parts[index + offset], parts[index]]
    editSource({ ...draft.source, excerpts: parts })
  }
  const previewSource = (event: FormEvent) => {
    event.preventDefault(); void perform(async () => { setPreview(await contextApi.preview(episodeId, draft.source, draft.scope)); setPreviewAccepted(false) })
  }
  const saveSource = () => { if (!workspace || !preview || !previewAccepted) return; void perform(async () => {
    const body = { source: draft.source, dataScope: draft.scope, previewToken: preview.previewToken }
    const result = await contextApi.save(episodeId, workspace.versionToken, { ...body, operationId: operation('snapshot', body) })
    updateDraft({ ...draft, source: result.item.source, text: emptyText(), supersedesId: null })
    setPreview(null); setPreviewAccepted(false); setEditingSource(false)
    await load(result.item.id); setNotice(`Source version ${result.item.number} saved on the local server. No provider call was made.`)
  }) }
  const saveInterpretation = (event: FormEvent) => { event.preventDefault(); if (!snapshot || !workspace) return; void perform(async () => {
    const body = { supersedesId: draft.supersedesId, text: draft.text }
    const result = await contextApi.revise(episodeId, snapshot.id, workspace.versionToken, { ...body, operationId: operation(`interpretation:${snapshot.id}`, body) })
    updateDraft({ ...draft, text: emptyText(), supersedesId: null })
    await load(snapshot.id); setSelectedRevisionId(result.item.id); setNotice('Interpretation saved separately. Review and confirm its exact revision when ready.')
  }) }
  const revision = revisions.find(x => x.id === selectedRevisionId) ?? null
  const current = !!snapshot && workspace?.currentSnapshotId === snapshot.id
  const enabled = !pending && !loading && !!workspace && !workspace.isArchived
  const confirm = () => { if (!snapshot || !workspace || !revision || !confirmAccepted) return; void perform(async () => {
    const body = { interpretationId: revision.id, sourceFingerprint: snapshot.sourceFingerprint }
    await contextApi.confirm(episodeId, snapshot.id, workspace.versionToken, { ...body, operationId: operation(`confirm:${snapshot.id}`, body) })
    setConfirmOpen(false); setConfirmAccepted(false); await load(snapshot.id)
    setNotice('Your confirmation was recorded for this exact source and interpretation. It does not verify the repository.')
  }) }

  return <section className="context-workbench" aria-label="Work context workspace">
    <header className="context-heading"><Link to="/reflect" className="context-back"><ArrowLeft size={16} /> All work episodes</Link><div><p className="eyebrow">Selected context</p><h2>{episode?.title ?? 'Work context'}</h2><p>Keep your source intact. Review interpretations as separate, attributed records.</p></div><Button variant="outline" onClick={() => void load()} disabled={pending || loading}><RefreshCw size={16} /> Reload saved state</Button></header>
    <p className="context-draft-note">Unsaved edits stay in this tab while you navigate or change appearance. Reloading the browser discards unsaved edits.</p>
    {loading && <p role="status">Loading saved context…</p>}
    {notice && <p className="context-saved" role="status">{notice}</p>}
    {error && <p className="form-error" role="alert">{error} Your unsaved edits remain available. Reload saved state before retrying a version conflict.</p>}
    {workspace?.isArchived && <p className="context-unknown">This episode is archived. Its saved source and interpretation history remain readable.</p>}
    <div className="context-grid">
      <div className="context-column"><section className="context-panel"><header><FileText size={18} /><h3>Your supplied source</h3><span className="context-state">{snapshot ? `Version ${snapshot.number} · ${current ? 'Current' : 'Historical'}` : 'Not saved yet'}</span></header>
        {snapshot && <><SourceView source={snapshot.source} /><details><summary>Fingerprint and disclosure</summary><p className="context-meta">{snapshot.fingerprintVersion} · Only the supplied source is fingerprinted.</p><code className="context-fingerprint">{snapshot.sourceFingerprint}</code><Disclosure manifest={snapshot.disclosure} /></details></>}
        <details open={editingSource} onToggle={event => setEditingSource(event.currentTarget.open)} className="context-editor"><summary>{snapshot ? 'Prepare a new source version' : 'Select the context to save'}</summary><p>Paste only the excerpts you choose to discuss. Changing assessed text or constraints creates a new snapshot.</p><form onSubmit={previewSource}>
          {draft.source.excerpts.map((part, index) => <fieldset key={index} disabled={pending || loading}><legend>Excerpt {index + 1}</legend><label htmlFor={`context-label-${index}`}>Excerpt label</label><Input id={`context-label-${index}`} value={part.label} maxLength={240} required onChange={event => editExcerpt(index, 'label', event.target.value)} /><label htmlFor={`context-version-${index}`}>Source version label (optional)</label><Input id={`context-version-${index}`} value={part.versionLabel} maxLength={240} onChange={event => editExcerpt(index, 'versionLabel', event.target.value)} /><label htmlFor={`context-text-${index}`}>Selected text</label><Textarea id={`context-text-${index}`} className="context-code-input" rows={7} maxLength={16000} required value={part.text} onChange={event => editExcerpt(index, 'text', event.target.value)} /><div className="context-actions"><Button type="button" variant="outline" aria-label={`Move excerpt ${index + 1} up`} disabled={index === 0} onClick={() => moveExcerpt(index, -1)}><ArrowUp size={16} /></Button><Button type="button" variant="outline" aria-label={`Move excerpt ${index + 1} down`} disabled={index === draft.source.excerpts.length - 1} onClick={() => moveExcerpt(index, 1)}><ArrowDown size={16} /></Button><Button type="button" variant="ghost" disabled={draft.source.excerpts.length === 1} onClick={() => editSource({ ...draft.source, excerpts: draft.source.excerpts.filter((_, i) => i !== index) })}><X size={16} /> Remove excerpt</Button></div></fieldset>)}
          <Button type="button" variant="outline" disabled={pending || draft.source.excerpts.length >= 8} onClick={() => editSource({ ...draft.source, excerpts: [...draft.source.excerpts, { label: 'Selected excerpt', versionLabel: '', text: '' }] })}><Plus size={16} /> Add excerpt</Button>
          <label htmlFor="context-intent">Declared intent</label><Textarea id="context-intent" rows={3} maxLength={4000} required value={draft.source.intent} disabled={pending || loading} onChange={event => editSource({ ...draft.source, intent: event.target.value })} />
          <label htmlFor="context-constraints">Declared constraints</label><Textarea id="context-constraints" rows={3} maxLength={8000} value={draft.source.constraints} disabled={pending || loading} onChange={event => editSource({ ...draft.source, constraints: event.target.value })} />
          <label htmlFor="context-classification">Data classification</label><select id="context-classification" value={draft.scope} disabled={pending || loading} onChange={event => editSource(draft.source, event.target.value as DataScope)}><option value="Restricted">Private / restricted</option><option value="PublicSynthetic">Public synthetic example</option></select>
          <p className="context-meta">Up to eight excerpts and 32,000 UTF-8 bytes. Text is never silently truncated. AI configuration is deferred; both classifications stay local.</p>
          <Button className="primary-button" type="submit" disabled={!enabled}>Preview selected context</Button>
        </form>{preview && <section className="context-preview" aria-label="Disclosure preview"><h4>Review the exact selected content</h4><SourceView source={preview.manifest.selectedContent} /><Disclosure manifest={preview.manifest} /><label className="context-check"><input type="checkbox" checked={previewAccepted} onChange={event => setPreviewAccepted(event.target.checked)} disabled={pending} /> I reviewed these selected fields and recipients for this source version.</label><Button className="primary-button" onClick={saveSource} disabled={!enabled || !previewAccepted}>Save source snapshot</Button></section>}</details>
      </section>
      <section className="context-panel"><h3>Source history</h3><p>Old versions remain inspectable when context changes.</p><div className="context-history">{snapshots.map(item => <Button variant="outline" key={item.id} disabled={pending || loading} aria-pressed={snapshot?.id === item.id} onClick={() => void load(item.id)}>Version {item.number}{workspace?.currentSnapshotId === item.id ? ' · Current' : ''}<small>{date(item.createdAt)}</small></Button>)}</div>{snapshotCursor && <Button variant="outline" disabled={pending || loading} onClick={() => void perform(async () => { const page = await contextApi.snapshots(episodeId, snapshotCursor); setSnapshots([...snapshots, ...page.items]); setSnapshotCursor(page.nextCursor) })}>Load older source versions</Button>}{!snapshots.length && !loading && <p>No source snapshots saved yet.</p>}</section></div>
      <div className="context-column"><section className="context-panel"><header><h3>Interpretation</h3><span className="context-state">{revision && workspace?.acceptedInterpretationId === revision.id && current ? 'User-confirmed exact revision' : 'Separate from source'}</span></header><p>AI configuration is deferred. You can record your own interpretation, missing information and corrections.</p>
        {revision ? <><InterpretationView revision={revision} /><div className="context-actions"><Button variant="outline" disabled={!enabled || !current} onClick={() => updateDraft({ ...draft, text: { ...revision.text, correction: '' }, supersedesId: revision.id })}>Revise this interpretation</Button><Button className="primary-button" disabled={!enabled || !current || workspace?.acceptedInterpretationId === revision.id} onClick={() => { setConfirmAccepted(false); setConfirmOpen(true) }}><Check size={16} /> Review exact confirmation</Button></div></> : <p>No interpretation recorded for this snapshot.</p>}
        {snapshot && !current && <p className="context-unknown">Historical source. Create or confirm interpretations on the current source version.</p>}
        {snapshot && <details className="context-editor" open={!revision || !!draft.supersedesId}><summary>{draft.supersedesId ? 'Write your correction' : 'Record a separate interpretation'}</summary><form onSubmit={saveInterpretation}><p className="context-meta">Saving creates a revision. Confirmation requires a separate action.</p>{(['intent', 'constraints', 'missingInformation', 'correction'] as const).map(field => <div key={field}><label htmlFor={`interpretation-${field}`}>{({ intent: 'Interpreted intent', constraints: 'Interpreted constraints', missingInformation: 'Missing information', correction: 'Correction notes' })[field]}</label><Textarea id={`interpretation-${field}`} rows={3} maxLength={field === 'intent' || field === 'correction' ? 4000 : 8000} required={field === 'intent'} value={draft.text[field]} disabled={!enabled || !current} onChange={event => updateDraft({ ...draft, text: { ...draft.text, [field]: event.target.value } })} /></div>)}<Button className="primary-button" type="submit" disabled={!enabled || !current}>Save interpretation revision</Button>{draft.supersedesId && <Button type="button" variant="ghost" disabled={pending} onClick={() => updateDraft({ ...draft, text: emptyText(), supersedesId: null })}>Discard correction draft</Button>}</form></details>}
      </section><section className="context-panel"><h3>Interpretation history</h3><div className="context-history">{revisions.map(item => <Button key={item.id} variant="outline" aria-pressed={revision?.id === item.id} onClick={() => setSelectedRevisionId(item.id)} disabled={pending}>Revision {item.number} · {item.origin}<small>{item.supersedesId ? 'Correction of an earlier revision' : 'Initial interpretation'}</small></Button>)}</div>{revisionCursor && snapshot && <Button variant="outline" disabled={pending} onClick={() => void perform(async () => { const page = await contextApi.interpretations(episodeId, snapshot.id, revisionCursor); setRevisions([...revisions, ...page.items]); setRevisionCursor(page.nextCursor) })}>Load older interpretations</Button>}<h4>Owner confirmations</h4>{confirmations.map(action => <p className="context-meta" key={action.id}>Revision {revisions.find(x => x.id === action.interpretationId)?.number ?? action.interpretationId} · {date(action.createdAt)} · {current && workspace?.acceptedInterpretationId === action.interpretationId ? 'Current selection' : 'Historical confirmation'}</p>)}{!confirmations.length && <p>No confirmation recorded.</p>}{confirmationCursor && snapshot && <Button variant="outline" disabled={pending} onClick={() => void perform(async () => { const page = await contextApi.confirmations(episodeId, snapshot.id, confirmationCursor); setConfirmations([...confirmations, ...page.items]); setConfirmationCursor(page.nextCursor) })}>Load older confirmations</Button>}</section></div>
    </div>
    <Dialog open={confirmOpen} onOpenChange={setConfirmOpen}><DialogContent className="context-confirm-dialog"><DialogTitle>Confirm this exact interpretation?</DialogTitle><DialogDescription>This records your review of the selected source and interpretation. It does not certify code correctness, repository completeness, or achievement.</DialogDescription>{snapshot && revision && <><p>Source version {snapshot.number} · Interpretation revision {revision.number}</p><code className="context-fingerprint">{snapshot.sourceFingerprint}</code><details><summary>Review supplied source</summary><SourceView source={snapshot.source} /></details><InterpretationView revision={revision} /><label className="context-check"><input type="checkbox" checked={confirmAccepted} disabled={pending} onChange={event => setConfirmAccepted(event.target.checked)} /> I reviewed this source, interpretation and recorded missing information.</label>{error && <p className="form-error" role="alert">{error}</p>}<Button className="primary-button" onClick={confirm} disabled={pending || !confirmAccepted}>Confirm source and interpretation</Button></>}</DialogContent></Dialog>
  </section>
}
