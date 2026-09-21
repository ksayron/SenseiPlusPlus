import { type FormEvent, type ReactNode, useCallback, useEffect, useMemo, useState } from 'react'
import {
  Archive,
  ArrowRight,
  BookOpen,
  BrainCircuit,
  BriefcaseBusiness,
  Check,
  ChevronRight,
  CircleAlert,
  CircleCheck,
  Clock3,
  CloudOff,
  FileCheck2,
  Gauge,
  Home,
  Lightbulb,
  Menu,
  MessageSquareText,
  Pencil,
  Plus,
  RefreshCw,
  Search,
  Settings,
  ShieldCheck,
  Sparkles,
  Target,
  TrendingUp,
  UserRound,
  X,
} from 'lucide-react'
import { api, getOwnerId } from './api'
import type {
  Assistance,
  Concept,
  Difficulty,
  EvidenceObservation,
  EvidenceSignal,
  EvidenceStatus,
  ExperienceEntry,
  ImpactState,
  Setting,
  WorkEpisode,
} from './types'

type View = 'today' | 'learn' | 'reflect' | 'evidence' | 'experience'
type ModalState =
  | { kind: 'concept'; item?: Concept }
  | { kind: 'episode'; item?: WorkEpisode }
  | { kind: 'evidence' }
  | { kind: 'experience'; item?: ExperienceEntry }
  | null

const viewMeta: Record<View, { label: string; eyebrow: string; title: string; icon: typeof Home }> = {
  today: { label: 'Today', eyebrow: 'Your workbench', title: 'Make the next idea stick.', icon: Home },
  learn: { label: 'Learn', eyebrow: 'Concept library', title: 'Name what you want to understand.', icon: BookOpen },
  reflect: { label: 'Reflect', eyebrow: 'Work reflection', title: 'Turn shipped work into learning.', icon: MessageSquareText },
  evidence: { label: 'Evidence', eyebrow: 'Knowledge profile', title: 'Keep the signal, not a score.', icon: Gauge },
  experience: { label: 'Experience', eyebrow: 'Experience journal', title: 'Build stories you can stand behind.', icon: BriefcaseBusiness },
}

const navigation: Array<{ view: View; icon: typeof Home }> = [
  { view: 'today', icon: Home },
  { view: 'learn', icon: BookOpen },
  { view: 'reflect', icon: MessageSquareText },
  { view: 'evidence', icon: Gauge },
  { view: 'experience', icon: BriefcaseBusiness },
]

const pretty = (value: string) => value.replace(/([a-z])([A-Z])/g, '$1 $2')
const shortDate = (value: string) => new Intl.DateTimeFormat('en', { month: 'short', day: 'numeric' }).format(new Date(value))
const currentRevision = (entry: ExperienceEntry) => entry.revisions.at(-1)!

function App() {
  const [view, setView] = useState<View>('today')
  const [menuOpen, setMenuOpen] = useState(false)
  const [modal, setModal] = useState<ModalState>(null)
  const [concepts, setConcepts] = useState<Concept[]>([])
  const [episodes, setEpisodes] = useState<WorkEpisode[]>([])
  const [evidence, setEvidence] = useState<EvidenceObservation[]>([])
  const [experience, setExperience] = useState<ExperienceEntry[]>([])
  const [online, setOnline] = useState<boolean | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [toast, setToast] = useState<string | null>(null)

  const reload = useCallback(async () => {
    setLoading(true)
    try {
      await api.health()
      setOnline(true)
      const [conceptData, episodeData, evidenceData, experienceData] = await Promise.all([
        api.concepts.list(),
        api.episodes.list(),
        api.evidence.list(),
        api.experience.list(),
      ])
      setConcepts(conceptData)
      setEpisodes(episodeData)
      setEvidence(evidenceData)
      setExperience(experienceData)
    } catch {
      setOnline(false)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { void reload() }, [reload])
  useEffect(() => {
    if (!toast) return
    const timer = window.setTimeout(() => setToast(null), 3200)
    return () => window.clearTimeout(timer)
  }, [toast])

  const mutate = async (action: () => Promise<unknown>, message: string) => {
    setSaving(true)
    try {
      await action()
      setModal(null)
      setToast(message)
      await reload()
    } catch (error) {
      setToast(error instanceof Error ? error.message : 'Something went wrong.')
    } finally {
      setSaving(false)
    }
  }

  const activeConcepts = concepts.filter((item) => item.isActive)
  const activeEpisodes = episodes.filter((item) => !item.isArchived)
  const activeEvidence = evidence.filter((item) => item.status === 'Active')
  const activeExperience = experience.filter((item) => !item.isArchived)
  const title = viewMeta[view]

  return (
    <div className="app-shell">
      <aside className={`sidebar ${menuOpen ? 'sidebar--open' : ''}`}>
        <button className="sidebar-close icon-button" onClick={() => setMenuOpen(false)} aria-label="Close menu"><X size={20} /></button>
        <button className="brand" onClick={() => setView('today')} aria-label="Sensei++ home">
          <span className="brand-mark">S<span>++</span></span>
          <span className="brand-name">Sensei<span>++</span></span>
        </button>
        <nav className="main-nav" aria-label="Main navigation">
          <p className="nav-caption">Workspace</p>
          {navigation.map(({ view: itemView, icon: Icon }) => (
            <button
              key={itemView}
              className={`nav-item ${view === itemView ? 'nav-item--active' : ''}`}
              onClick={() => { setView(itemView); setMenuOpen(false) }}
            >
              <Icon size={19} strokeWidth={1.8} />
              <span>{viewMeta[itemView].label}</span>
              {itemView === 'reflect' && activeEpisodes.length > 0 && <span className="nav-count">{activeEpisodes.length}</span>}
            </button>
          ))}
        </nav>
        <div className="sidebar-card">
          <div className="sidebar-card-icon"><Sparkles size={18} /></div>
          <p>Small sessions.<br />Useful understanding.</p>
          <button onClick={() => setModal({ kind: 'concept' })}>Add a concept <ArrowRight size={15} /></button>
        </div>
        <div className="sidebar-footer">
          <div className={`status-dot ${online ? 'status-dot--online' : ''}`} />
          <div>
            <strong>{online ? 'API connected' : online === false ? 'API offline' : 'Checking API'}</strong>
            <span>Development workspace</span>
          </div>
          <Settings size={17} aria-label="Settings" />
        </div>
      </aside>

      <main className="main-area">
        <header className="topbar">
          <button className="menu-button icon-button" onClick={() => setMenuOpen(true)} aria-label="Open menu"><Menu size={21} /></button>
          <div className="breadcrumbs"><span>Sensei++</span><ChevronRight size={14} /><strong>{title.label}</strong></div>
          <div className="topbar-actions">
            <label className="search-box">
              <Search size={17} />
              <input aria-label="Search workspace" placeholder="Search your workspace" />
              <kbd>⌘ K</kbd>
            </label>
            <button className="avatar" title={`Development owner: ${getOwnerId()}`} aria-label="Development user">
              <UserRound size={18} />
            </button>
          </div>
        </header>

        <div className="content-frame">
          <section className="page-heading reveal">
            <div>
              <p className="eyebrow">{title.eyebrow}</p>
              <h1>{title.title}</h1>
            </div>
            {view !== 'today' && (
              <button className="primary-button" onClick={() => setModal({ kind: view === 'learn' ? 'concept' : view === 'reflect' ? 'episode' : view === 'evidence' ? 'evidence' : 'experience' })}>
                <Plus size={18} /> {view === 'reflect' ? 'Capture work' : view === 'experience' ? 'New entry' : view === 'evidence' ? 'Add signal' : 'New concept'}
              </button>
            )}
          </section>

          {online === false && <OfflineNotice onRetry={() => void reload()} />}

          {view === 'today' && (
            <Dashboard
              loading={loading}
              concepts={activeConcepts}
              episodes={activeEpisodes}
              evidence={activeEvidence}
              experience={activeExperience}
              onNavigate={setView}
              onCreate={setModal}
            />
          )}
          {view === 'learn' && <LearnPage concepts={concepts} loading={loading} onEdit={(item) => setModal({ kind: 'concept', item })} onDeactivate={(item) => void mutate(() => api.concepts.deactivate(item), 'Concept moved out of active study.')} />}
          {view === 'reflect' && <ReflectPage episodes={episodes} loading={loading} onEdit={(item) => setModal({ kind: 'episode', item })} onArchive={(item) => void mutate(() => api.episodes.archive(item), 'Reflection archived.')} />}
          {view === 'evidence' && <EvidencePage evidence={evidence} concepts={concepts} loading={loading} onStatus={(item, status) => void mutate(() => api.evidence.status(item, status), `Signal marked ${pretty(status).toLowerCase()}.`)} onWithdraw={(item) => void mutate(() => api.evidence.withdraw(item), 'Evidence signal withdrawn.')} />}
          {view === 'experience' && <ExperiencePage entries={experience} concepts={concepts} loading={loading} onEdit={(item) => setModal({ kind: 'experience', item })} onApprove={(item) => void mutate(() => api.experience.approve(item, currentRevision(item).number), 'Revision approved and preserved.')} onArchive={(item) => void mutate(() => api.experience.archive(item), 'Experience entry archived.')} />}
        </div>
      </main>

      {modal && (
        <Modal title={modal.kind === 'concept' ? modal.item ? 'Edit concept' : 'Add a concept' : modal.kind === 'episode' ? modal.item ? 'Edit reflection' : 'Capture meaningful work' : modal.kind === 'evidence' ? 'Record an evidence signal' : modal.item ? 'Create a new revision' : 'Create an experience entry'} onClose={() => setModal(null)}>
          {modal.kind === 'concept' && <ConceptForm item={modal.item} saving={saving} onSubmit={(body) => void mutate(() => modal.item ? api.concepts.update(modal.item, body) : api.concepts.create(body), modal.item ? 'Concept updated.' : 'Concept added to your library.')} />}
          {modal.kind === 'episode' && <EpisodeForm item={modal.item} saving={saving} onSubmit={(body) => void mutate(() => modal.item ? api.episodes.update(modal.item, body) : api.episodes.create(body), modal.item ? 'Reflection updated.' : 'Work episode captured.')} />}
          {modal.kind === 'evidence' && <EvidenceForm concepts={activeConcepts} saving={saving} onSubmit={(body) => void mutate(() => api.evidence.create(body), 'Evidence signal recorded.')} />}
          {modal.kind === 'experience' && <ExperienceForm item={modal.item} concepts={activeConcepts} saving={saving} onSubmit={(body) => void mutate(() => modal.item ? api.experience.revise(modal.item, body) : api.experience.create(body), modal.item ? 'New revision created.' : 'Experience entry created.')} />}
        </Modal>
      )}

      {toast && <div className="toast" role="status"><CircleCheck size={18} /><span>{toast}</span></div>}
    </div>
  )
}

function OfflineNotice({ onRetry }: { onRetry: () => void }) {
  return (
    <div className="offline-notice reveal">
      <CloudOff size={19} />
      <div><strong>The learning API is offline.</strong><span>Start the .NET host on port 5062, then reconnect. Your interface is still available.</span></div>
      <button onClick={onRetry}><RefreshCw size={16} /> Retry</button>
    </div>
  )
}

function Dashboard({ loading, concepts, episodes, evidence, experience, onNavigate, onCreate }: {
  loading: boolean
  concepts: Concept[]
  episodes: WorkEpisode[]
  evidence: EvidenceObservation[]
  experience: ExperienceEntry[]
  onNavigate: (view: View) => void
  onCreate: (modal: ModalState) => void
}) {
  const recentEpisode = [...episodes].sort((a, b) => b.eventDate.localeCompare(a.eventDate))[0]
  const focusConcept = concepts[0]
  const approvedCount = experience.filter((entry) => currentRevision(entry).approvalState === 'Approved').length

  return (
    <div className="dashboard-grid">
      <section className="focus-card reveal reveal--1">
        <div className="focus-copy">
          <div className="focus-label"><span>01</span> Suggested next step</div>
          <h2>{focusConcept ? `Explain ${focusConcept.name} without notes.` : 'Choose one idea worth understanding deeply.'}</h2>
          <p>{focusConcept ? focusConcept.description : 'Start a concept library that connects what you study with the work you actually do.'}</p>
          <button className="ink-button" onClick={() => focusConcept ? onNavigate('learn') : onCreate({ kind: 'concept' })}>
            {focusConcept ? 'Open concept library' : 'Add your first concept'} <ArrowRight size={17} />
          </button>
        </div>
        <div className="focus-visual" aria-hidden="true">
          <div className="orbit orbit--one" />
          <div className="orbit orbit--two" />
          <BrainCircuit size={60} strokeWidth={1.2} />
          <span>learn</span><span>reflect</span><span>prove</span>
        </div>
      </section>

      <section className="metrics-row reveal reveal--2">
        <Metric icon={<BookOpen size={19} />} value={loading ? '—' : concepts.length} label="active concepts" tone="lime" />
        <Metric icon={<ShieldCheck size={19} />} value={loading ? '—' : evidence.length} label="evidence signals" tone="blue" />
        <Metric icon={<FileCheck2 size={19} />} value={loading ? '—' : approvedCount} label="approved stories" tone="orange" />
      </section>

      <section className="panel reflection-panel reveal reveal--3">
        <PanelHeader number="02" label="Reflect while it is fresh" action="All reflections" onAction={() => onNavigate('reflect')} />
        {recentEpisode ? (
          <article className="reflection-item">
            <div className="reflection-date"><strong>{new Date(recentEpisode.eventDate).getDate()}</strong><span>{new Intl.DateTimeFormat('en', { month: 'short' }).format(new Date(recentEpisode.eventDate))}</span></div>
            <div><span className="tiny-label">{pretty(recentEpisode.setting)}</span><h3>{recentEpisode.title}</h3><p>{recentEpisode.summary}</p></div>
            <ChevronRight size={20} />
          </article>
        ) : (
          <EmptyState icon={<MessageSquareText />} title="No reflection queued" copy="Capture a decision, incident, or change before the useful context fades." action="Capture work" onAction={() => onCreate({ kind: 'episode' })} />
        )}
      </section>

      <section className="panel profile-panel reveal reveal--4">
        <PanelHeader number="03" label="Knowledge signals" action="View profile" onAction={() => onNavigate('evidence')} />
        {concepts.length > 0 ? (
          <div className="signal-list">
            {concepts.slice(0, 3).map((concept, index) => {
              const count = evidence.filter((item) => item.conceptId === concept.id).length
              return <div className="signal-row" key={concept.id}><span className={`signal-rank signal-rank--${index + 1}`}>{index + 1}</span><div><strong>{concept.name}</strong><span>{count ? `${count} concrete ${count === 1 ? 'signal' : 'signals'}` : 'Not observed yet'}</span></div><div className="signal-dots">{[0, 1, 2, 3].map((dot) => <i key={dot} className={dot < Math.min(count, 4) ? 'filled' : ''} />)}</div></div>
            })}
          </div>
        ) : <EmptyState icon={<Target />} title="Profile starts with evidence" copy="Concepts and observations will appear here without collapsing you into a score." action="Add concept" onAction={() => onCreate({ kind: 'concept' })} />}
      </section>

      <section className="prompt-card reveal reveal--5">
        <Lightbulb size={22} />
        <div><span>Reflection prompt</span><p>What assumption in your latest change would fail first under load?</p></div>
        <button onClick={() => onCreate({ kind: 'episode' })} aria-label="Answer reflection prompt"><ArrowRight size={18} /></button>
      </section>

      <section className="panel journal-preview reveal reveal--5">
        <PanelHeader number="04" label="Experience in progress" action="Open journal" onAction={() => onNavigate('experience')} />
        <div className="journal-count"><strong>{experience.length.toString().padStart(2, '0')}</strong><span>credible stories<br />in your journal</span><TrendingUp size={22} /></div>
      </section>
    </div>
  )
}

function Metric({ icon, value, label, tone }: { icon: ReactNode; value: string | number; label: string; tone: string }) {
  return <div className={`metric metric--${tone}`}><span>{icon}</span><strong>{value}</strong><p>{label}</p></div>
}

function PanelHeader({ number, label, action, onAction }: { number: string; label: string; action: string; onAction: () => void }) {
  return <header className="panel-header"><div><span>{number}</span><h2>{label}</h2></div><button onClick={onAction}>{action} <ArrowRight size={15} /></button></header>
}

function LearnPage({ concepts, loading, onEdit, onDeactivate }: { concepts: Concept[]; loading: boolean; onEdit: (item: Concept) => void; onDeactivate: (item: Concept) => void }) {
  return (
    <section className="collection reveal reveal--1">
      <div className="collection-toolbar"><p>{concepts.filter((item) => item.isActive).length} active ideas in your library</p><span><Clock3 size={15} /> Short, deliberate sessions</span></div>
      {loading ? <SkeletonGrid /> : concepts.length === 0 ? <BigEmpty title="Your concept library is a blank page." copy="Add a technology, mechanism, or idea you want to explain and apply—not merely recognize." /> : (
        <div className="card-grid">
          {concepts.map((concept, index) => (
            <article className={`concept-card ${!concept.isActive ? 'is-muted' : ''}`} key={concept.id}>
              <div className="card-index">{String(index + 1).padStart(2, '0')}</div>
              <div className="concept-top"><span className={`difficulty difficulty--${concept.difficulty.toLowerCase()}`}>{concept.difficulty}</span><span>v{concept.version}</span></div>
              <h2>{concept.name}</h2><p>{concept.description}</p>
              <div className="concept-footer"><span>{concept.locale.toUpperCase()} · {concept.isActive ? 'Active' : 'Inactive'}</span><div><button className="icon-button" title="Edit concept" onClick={() => onEdit(concept)}><Pencil size={16} /></button>{concept.isActive && <button className="icon-button" title="Deactivate concept" onClick={() => onDeactivate(concept)}><Archive size={16} /></button>}</div></div>
            </article>
          ))}
        </div>
      )}
    </section>
  )
}

function ReflectPage({ episodes, loading, onEdit, onArchive }: { episodes: WorkEpisode[]; loading: boolean; onEdit: (item: WorkEpisode) => void; onArchive: (item: WorkEpisode) => void }) {
  return (
    <section className="timeline-wrap reveal reveal--1">
      <div className="collection-toolbar"><p>Meaningful episodes, not activity metrics</p><span><ShieldCheck size={15} /> Private by default</span></div>
      {loading ? <SkeletonGrid /> : episodes.length === 0 ? <BigEmpty title="What happened, and why did it matter?" copy="Capture a bug investigation, feature, design decision, review, or learning moment while its trade-offs are still clear." /> : (
        <div className="timeline">
          {[...episodes].sort((a, b) => b.eventDate.localeCompare(a.eventDate)).map((episode) => (
            <article className={`timeline-item ${episode.isArchived ? 'is-muted' : ''}`} key={episode.id}>
              <div className="timeline-marker" /><time>{shortDate(episode.eventDate)}</time>
              <div className="timeline-card"><div className="timeline-meta"><span>{pretty(episode.setting)}</span><span>{episode.role}</span><span>v{episode.version}</span></div><h2>{episode.title}</h2><p>{episode.summary}</p><footer><span>{episode.isArchived ? 'Archived' : 'Ready to explore'}</span><div><button className="text-button" onClick={() => onEdit(episode)}><Pencil size={15} /> Edit</button>{!episode.isArchived && <button className="text-button" onClick={() => onArchive(episode)}><Archive size={15} /> Archive</button>}</div></footer></div>
            </article>
          ))}
        </div>
      )}
    </section>
  )
}

function EvidencePage({ evidence, concepts, loading, onStatus, onWithdraw }: { evidence: EvidenceObservation[]; concepts: Concept[]; loading: boolean; onStatus: (item: EvidenceObservation, status: EvidenceStatus) => void; onWithdraw: (item: EvidenceObservation) => void }) {
  const conceptMap = useMemo(() => new Map(concepts.map((item) => [item.id, item.name])), [concepts])
  return (
    <section className="evidence-layout reveal reveal--1">
      <div className="evidence-principle"><CircleAlert size={20} /><div><strong>No universal mastery score.</strong><p>Each signal keeps its context, source, assistance, and status so you can judge what it actually shows.</p></div></div>
      {loading ? <SkeletonGrid /> : evidence.length === 0 ? <BigEmpty title="Evidence is more honest than a percentage." copy="Record a concrete explanation, recall, scenario, or exposure and keep the conditions attached." /> : (
        <div className="evidence-table-wrap"><table className="evidence-table"><thead><tr><th>Concept / aspect</th><th>Signal</th><th>Assistance</th><th>Observed</th><th>Status</th><th><span className="sr-only">Actions</span></th></tr></thead><tbody>{evidence.map((item) => <tr key={item.id} className={item.status === 'Withdrawn' ? 'is-muted' : ''}><td><strong>{conceptMap.get(item.conceptId) ?? 'Unknown concept'}</strong><span>{item.aspect}</span></td><td>{pretty(item.signalKind)}<small>{pretty(item.sourceKind)}</small></td><td>{pretty(item.assistance)}</td><td>{shortDate(item.observedAt)}</td><td><span className={`status-pill status-pill--${item.status.toLowerCase()}`}>{item.status}</span></td><td><div className="table-actions">{item.status === 'Active' && <button title="Mark disputed" onClick={() => onStatus(item, 'Disputed')}><CircleAlert size={16} /></button>}{!['Withdrawn', 'Superseded'].includes(item.status) && <button title="Withdraw signal" onClick={() => onWithdraw(item)}><Archive size={16} /></button>}</div></td></tr>)}</tbody></table></div>
      )}
    </section>
  )
}

function ExperiencePage({ entries, concepts, loading, onEdit, onApprove, onArchive }: { entries: ExperienceEntry[]; concepts: Concept[]; loading: boolean; onEdit: (item: ExperienceEntry) => void; onApprove: (item: ExperienceEntry) => void; onArchive: (item: ExperienceEntry) => void }) {
  const conceptMap = useMemo(() => new Map(concepts.map((item) => [item.id, item.name])), [concepts])
  return (
    <section className="collection reveal reveal--1">
      <div className="collection-toolbar"><p>Every approved claim stays tied to an exact revision</p><span><FileCheck2 size={15} /> Factual approval</span></div>
      {loading ? <SkeletonGrid /> : entries.length === 0 ? <BigEmpty title="Your work deserves better than a forgotten bullet point." copy="Capture context, your role, decisions, alternatives, and outcomes. Approve only the revision you can stand behind." /> : (
        <div className="experience-grid">{entries.map((entry) => { const revision = currentRevision(entry); return <article className={`experience-card ${entry.isArchived ? 'is-muted' : ''}`} key={entry.id}><header><span className={`status-pill status-pill--${revision.approvalState.toLowerCase()}`}>{revision.approvalState}</span><span>Revision {revision.number} · v{entry.version}</span></header><h2>{revision.title}</h2><p>{revision.context}</p><div className="tag-row">{revision.conceptIds.map((id) => <span key={id}>{conceptMap.get(id) ?? 'Concept'}</span>)}</div><div className="experience-facts"><div><span>My role</span><strong>{revision.role}</strong></div><div><span>Impact</span><strong>{revision.impactState}</strong></div></div><footer><button className="text-button" onClick={() => onEdit(entry)}><Pencil size={15} /> Revise</button>{revision.approvalState === 'Draft' && !entry.isArchived && <button className="approve-button" onClick={() => onApprove(entry)}><Check size={16} /> Approve exact revision</button>}{!entry.isArchived && <button className="icon-button" title="Archive entry" onClick={() => onArchive(entry)}><Archive size={16} /></button>}</footer></article> })}</div>
      )}
    </section>
  )
}

function EmptyState({ icon, title, copy, action, onAction }: { icon: ReactNode; title: string; copy: string; action: string; onAction: () => void }) {
  return <div className="empty-state"><span>{icon}</span><div><h3>{title}</h3><p>{copy}</p></div><button onClick={onAction}>{action} <ArrowRight size={15} /></button></div>
}

function BigEmpty({ title, copy }: { title: string; copy: string }) {
  return <div className="big-empty"><div className="big-empty-mark"><Plus size={28} /></div><p className="eyebrow">Begin here</p><h2>{title}</h2><p>{copy}</p></div>
}

function SkeletonGrid() {
  return <div className="skeleton-grid" aria-label="Loading"><i /><i /><i /></div>
}

function Modal({ title, onClose, children }: { title: string; onClose: () => void; children: ReactNode }) {
  useEffect(() => {
    const handler = (event: KeyboardEvent) => { if (event.key === 'Escape') onClose() }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [onClose])
  return <div className="modal-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose() }}><section className="modal" role="dialog" aria-modal="true" aria-label={title}><header><div><span>Sensei++ workbench</span><h2>{title}</h2></div><button className="icon-button" onClick={onClose} aria-label="Close dialog"><X size={20} /></button></header>{children}</section></div>
}

function ConceptForm({ item, saving, onSubmit }: { item?: Concept; saving: boolean; onSubmit: (body: Pick<Concept, 'key' | 'name' | 'description' | 'locale' | 'difficulty'>) => void }) {
  const submit = (event: FormEvent<HTMLFormElement>) => { event.preventDefault(); const data = new FormData(event.currentTarget); onSubmit({ key: item?.key ?? String(data.get('key')), name: String(data.get('name')), description: String(data.get('description')), locale: String(data.get('locale')), difficulty: String(data.get('difficulty')) as Difficulty }) }
  return <form className="editor-form" onSubmit={submit}><div className="form-row"><Field label="Concept key" hint="Stable, URL-friendly identifier"><input name="key" defaultValue={item?.key} disabled={Boolean(item)} required placeholder="distributed-locks" /></Field><Field label="Difficulty"><select name="difficulty" defaultValue={item?.difficulty ?? 'Intermediate'}><option>Beginner</option><option>Intermediate</option><option>Advanced</option></select></Field></div><Field label="Name"><input name="name" defaultValue={item?.name} required placeholder="Distributed locks" /></Field><Field label="What should become explainable?"><textarea name="description" defaultValue={item?.description} required rows={4} placeholder="Explain ownership, leases, failure modes, and when a lock is the wrong tool." /></Field><Field label="Content locale"><input name="locale" defaultValue={item?.locale ?? 'en'} required /></Field><FormFooter saving={saving} label={item ? 'Save changes' : 'Add concept'} /></form>
}

function EpisodeForm({ item, saving, onSubmit }: { item?: WorkEpisode; saving: boolean; onSubmit: (body: Pick<WorkEpisode, 'title' | 'setting' | 'eventDate' | 'role' | 'summary'>) => void }) {
  const submit = (event: FormEvent<HTMLFormElement>) => { event.preventDefault(); const data = new FormData(event.currentTarget); onSubmit({ title: String(data.get('title')), setting: String(data.get('setting')) as Setting, eventDate: String(data.get('eventDate')), role: String(data.get('role')), summary: String(data.get('summary')) }) }
  return <form className="editor-form" onSubmit={submit}><Field label="Episode title"><input name="title" defaultValue={item?.title} required placeholder="Stabilized duplicate webhook delivery" /></Field><div className="form-row"><Field label="Setting"><select name="setting" defaultValue={item?.setting ?? 'PersonalProject'}><option value="Employment">Employment</option><option value="Coursework">Coursework</option><option value="PersonalProject">Personal project</option><option value="Other">Other</option></select></Field><Field label="When did it happen?"><input name="eventDate" type="date" defaultValue={item?.eventDate ?? new Date().toISOString().slice(0, 10)} required /></Field></div><Field label="Your role"><input name="role" defaultValue={item?.role} required placeholder="Investigated and designed the retry boundary" /></Field><Field label="What happened?"><textarea name="summary" defaultValue={item?.summary} required rows={5} placeholder="Describe the problem, decision, uncertainty, and what changed. Keep employer-sensitive details out." /></Field><FormFooter saving={saving} label={item ? 'Save reflection' : 'Capture episode'} /></form>
}

function EvidenceForm({ concepts, saving, onSubmit }: { concepts: Concept[]; saving: boolean; onSubmit: (body: Omit<EvidenceObservation, 'id' | 'ownerId' | 'status' | 'observedAt' | 'version'>) => void }) {
  const submit = (event: FormEvent<HTMLFormElement>) => { event.preventDefault(); const data = new FormData(event.currentTarget); onSubmit({ conceptId: String(data.get('conceptId')), aspect: String(data.get('aspect')), signalKind: String(data.get('signalKind')) as EvidenceSignal, sourceKind: 'Declaration', sourceId: crypto.randomUUID(), assistance: String(data.get('assistance')) as Assistance, conditions: String(data.get('conditions')) }) }
  return <form className="editor-form" onSubmit={submit}>{concepts.length === 0 && <div className="form-warning"><CircleAlert size={17} /> Add an active concept before recording evidence.</div>}<Field label="Concept"><select name="conceptId" required disabled={concepts.length === 0}><option value="">Choose a concept</option>{concepts.map((concept) => <option key={concept.id} value={concept.id}>{concept.name}</option>)}</select></Field><Field label="Observed aspect"><input name="aspect" required placeholder="Explained at-least-once delivery failure modes" /></Field><div className="form-row"><Field label="Signal kind"><select name="signalKind" defaultValue="Explanation"><option>SelfDeclaration</option><option>ProfessionalExposure</option><option>Explanation</option><option>Scenario</option><option>Recall</option><option>ClientReportedAssessment</option></select></Field><Field label="Assistance"><select name="assistance" defaultValue="None"><option>None</option><option>HintUsed</option><option>ReferenceReviewed</option><option>SelfReviewed</option></select></Field></div><Field label="Conditions and limits"><textarea name="conditions" rows={4} required placeholder="Explained from memory after implementing a retry policy; did not cover broker failover." /></Field><FormFooter saving={saving} disabled={concepts.length === 0} label="Record signal" /></form>
}

function ExperienceForm({ item, concepts, saving, onSubmit }: { item?: ExperienceEntry; concepts: Concept[]; saving: boolean; onSubmit: (body: Record<string, unknown>) => void }) {
  const revision = item ? currentRevision(item) : undefined
  const submit = (event: FormEvent<HTMLFormElement>) => { event.preventDefault(); const data = new FormData(event.currentTarget); onSubmit({ title: String(data.get('title')), setting: String(data.get('setting')) as Setting, context: String(data.get('context')), role: String(data.get('role')), actions: String(data.get('actions')), alternatives: String(data.get('alternatives')), outcome: String(data.get('outcome')), impactState: String(data.get('impactState')) as ImpactState, conceptIds: data.getAll('conceptIds').map(String) }) }
  return <form className="editor-form" onSubmit={submit}><Field label="Story title"><input name="title" defaultValue={revision?.title} required placeholder="Made webhook retries safe and observable" /></Field><div className="form-row"><Field label="Setting"><select name="setting" defaultValue={revision?.setting ?? 'PersonalProject'}><option>Employment</option><option>Coursework</option><option value="PersonalProject">Personal project</option><option>Other</option></select></Field><Field label="Impact evidence"><select name="impactState" defaultValue={revision?.impactState ?? 'Unknown'}><option>Unknown</option><option>Qualitative</option><option>Measured</option></select></Field></div><Field label="Context and constraints"><textarea name="context" defaultValue={revision?.context} rows={3} required placeholder="What problem existed, for whom, and under which constraints?" /></Field><Field label="Your role"><input name="role" defaultValue={revision?.role} required placeholder="What you personally owned or contributed" /></Field><Field label="Actions and reasoning"><textarea name="actions" defaultValue={revision?.actions} rows={4} required placeholder="What you investigated, decided, and changed" /></Field><Field label="Alternatives considered"><textarea name="alternatives" defaultValue={revision?.alternatives} rows={2} placeholder="What else could have worked, and why did you choose differently?" /></Field><Field label="Outcome"><textarea name="outcome" defaultValue={revision?.outcome} rows={3} placeholder="Measured, qualitative, or not yet known—keep it factual." /></Field><Field label="Related concepts" hint="Select all that genuinely apply"><div className="check-grid">{concepts.map((concept) => <label key={concept.id}><input type="checkbox" name="conceptIds" value={concept.id} defaultChecked={revision?.conceptIds.includes(concept.id)} /><span><Check size={13} />{concept.name}</span></label>)}</div></Field><FormFooter saving={saving} label={item ? 'Create revision' : 'Save draft'} /></form>
}

function Field({ label, hint, children }: { label: string; hint?: string; children: ReactNode }) {
  return <label className="field"><span>{label}{hint && <small>{hint}</small>}</span>{children}</label>
}

function FormFooter({ saving, label, disabled = false }: { saving: boolean; label: string; disabled?: boolean }) {
  return <footer className="form-footer"><span><ShieldCheck size={15} /> You approve your own claims.</span><button className="primary-button" type="submit" disabled={saving || disabled}>{saving ? <RefreshCw className="spin" size={17} /> : <Check size={17} />}{saving ? 'Saving…' : label}</button></footer>
}

export default App
