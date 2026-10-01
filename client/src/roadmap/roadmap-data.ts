export type MilestoneStatus = 'delivered' | 'next' | 'later'
export type LogCategory = 'Build' | 'Design' | 'Decisions' | 'Verification'
export type SourceId = 'learning' | 'delivery' | 'interface' | 'architecture' | 'model'

export const sources: Record<SourceId, { title: string; path: string; load: () => Promise<string> }> = {
  learning: { title: 'Phase 1 implementation record', path: 'docs/13-learning-implementation.md', load: async () => (await import('../../../docs/13-learning-implementation.md?raw')).default },
  delivery: { title: 'Learning delivery plan', path: 'docs/11-learning-delivery-plan.md', load: async () => (await import('../../../docs/11-learning-delivery-plan.md?raw')).default },
  interface: { title: 'SPA implementation record', path: 'docs/ui/Sensei-SPA-Design-Implementation.md', load: async () => (await import('../../../docs/ui/Sensei-SPA-Design-Implementation.md?raw')).default },
  architecture: { title: 'Architecture and alternatives', path: 'docs/01-architecture.md', load: async () => (await import('../../../docs/01-architecture.md?raw')).default },
  model: { title: 'Functional model & requirements', path: 'docs/14-functional-model-idef0.md', load: async () => (await import('../../../docs/14-functional-model-idef0.md?raw')).default },
}

export interface Milestone {
  id: string
  number: string
  title: string
  subtitle: string
  status: MilestoneStatus
  period: string
  description: string
  outcome: string
  items: string[]
  source: SourceId
  evidence: string
}

export const milestones: Milestone[] = [
  {
    id: 'foundations', number: '01', title: 'Lay the groundwork', subtitle: 'Architecture & persistence', status: 'delivered', period: '21 SEP 2026',
    description: 'Give learning, reflection, evidence and experience their own boundaries, with one local application and database.',
    outcome: 'A modular .NET backend that can keep real history.',
    items: ['Modular .NET 9 host and domain boundaries', 'PostgreSQL / EF Core persistence and explicit migrations', 'Concept, reflection, evidence and experience lifecycles', 'Automated unit, architecture and integration foundations'],
    source: 'architecture', evidence: 'Git history: 55dcd55, ff78795 (#42), a62e482 (#43). These are committed foundations; later milestones also include local working-tree delivery.',
  },
  {
    id: 'contracts', number: '02', title: 'Make the contract dependable', subtitle: 'Versioned HTTP API', status: 'delivered', period: '24 SEP 2026',
    description: 'Make concurrent changes and failed requests explainable. A stale update should never silently overwrite accepted work.',
    outcome: 'Predictable requests, explicit conflicts, safer updates.',
    items: ['Strong ETags and opaque version tokens', 'If-Match preconditions for mutations', 'Owner-scoped cursor pagination', 'Problem Details and reviewed OpenAPI contracts'],
    source: 'architecture', evidence: 'Git history: 762cc4d (#44), standardize versioned HTTP API contract. Missing, malformed and stale preconditions have separate outcomes.',
  },
  {
    id: 'learning', number: '03', title: 'Close the learning loop', subtitle: 'Phase 1 · L1–L7', status: 'delivered', period: '29 SEP 2026',
    description: 'Go from a topic to practice, saved feedback and inspectable evidence. Subject knowledge lives in content rather than in the engine.',
    outcome: 'A complete local learning journey, independent of AI.',
    items: ['Topic workspace, safe material and six exercise formats', '3 / 5 / 10-item practice with honest content shortages', 'Pinned sessions, autosave, receipts and conflict recovery', 'Trusted evidence, self-review cooldown and shared coverage', 'Goals, deterministic recommendations and test roadmaps', 'L1–L7 acceptance checks and implementation record'],
    source: 'learning', evidence: 'Recorded local verification: 18 unit, 3 architecture, 25 PostgreSQL integration, 7 client tests and 34 browser journeys. These are historical results from the implementation record, not live CI.',
  },
  {
    id: 'workbench', number: '04', title: 'Give the work a home', subtitle: 'Responsive web workbench', status: 'delivered', period: '01 OCT 2026',
    description: 'Bring Today, Learn, Reflect, Evidence and Experience together in a responsive interface that preserves learning and editing state.',
    outcome: 'One coherent workspace, from desktop to narrow screens.',
    items: ['Coastal / Coastal Night with local fonts and shared tokens', 'Responsive shell and all five workspace views', 'Accessible dialogs, keyboard topic tabs and focus recovery', 'Learning flows retain answers and acknowledged save state', 'Desktop, tablet and 320px layout checks'],
    source: 'interface', evidence: 'SPA record dated 1 October: build/lint and 7 client tests passed; 36 learning, 18 UI and 21 demo checks recorded. Physical-device and full screen-reader validation remain open.',
  },
  {
    id: 'release', number: '05', title: 'Prepare the next release', subtitle: 'Local delivery → release readiness', status: 'next', period: 'NEXT · NO FIXED DATE',
    description: 'Turn the tested local prototype into a release with an explicit audience, trustworthy access and useful content. This is a proposed next checkpoint, not an approved hosting schedule.',
    outcome: 'A release whose content, access and recovery are ready for its audience.',
    items: ['Confirm the first release audience and scope', 'Replace development owner selection before a non-local release', 'Validate educational content beyond demonstration fixtures', 'Plan deployment, backup and recovery for the chosen environment'],
    source: 'delivery', evidence: 'The current implementation explicitly leaves authentication, deployment and production content outside Phase 1. Hosting and exact dates need their own decisions.',
  },
]

export const futureTracks = [
  { phase: '02', title: 'Learning that adapts', detail: 'Spaced review, decay, dimensions and explainable recommendations.' },
  { phase: '03', title: 'Go deeper', detail: 'Intensive practice and exam formats with deferred feedback.' },
  { phase: '04', title: 'Take it with you', detail: 'Offline packs, durable local activity and multi-device synchronization.' },
  { phase: '05', title: 'Connect real work', detail: 'Agent, commit and IDE signals with explicit provenance.' },
  { phase: '06', title: 'Optional AI assistance', detail: 'Ordinary learning objects, free-only policy and a usable core without a provider.' },
]

export interface DevLog {
  id: string
  date: string
  category: LogCategory
  title: string
  summary: string
  details: string[]
  tags: string[]
  source?: SourceId
  milestone?: string
  links?: Array<{ label: string; url: string }>
}

// Curated project history. Dates describe development records, not deployment dates.
export const devLogs: DevLog[] = [
  { id: 'github-audit', date: '2026-10-02', category: 'Build', title: 'Give the delivery a paper trail', summary: 'Design, Phase 1 learning and the build journal now have focused GitHub tracking issues and separate delivery commits.', details: ['The older learning backlog includes broader AI, scheduling, publishing and export requirements. A focused L1–L7 audit records the delivered local phase without closing unfinished expansion work.', 'Each delivery issue records acceptance checks and links to its commits and pull request.', 'The public journal links the audit records so source, verification and delivery history stay connected.'], tags: ['GitHub', 'audit', 'delivery'], links: [{ label: 'Design audit #45', url: 'https://github.com/ksayron/SenseiPlusPlus/issues/45' }, { label: 'Learning audit #46', url: 'https://github.com/ksayron/SenseiPlusPlus/issues/46' }, { label: 'Roadmap audit #47', url: 'https://github.com/ksayron/SenseiPlusPlus/issues/47' }] },
  { id: 'field-journal', date: '2026-10-01', category: 'Build', title: 'Open the project notebook', summary: 'A public development roadmap now connects delivered milestones, future work and the decisions behind them.', details: ['Explore milestones, filter and search the journal, and open the source records in place.', 'This is a curated repository snapshot. It does not poll CI, expose learner data or require the learning API.', 'Design, Phase 1 learning and this journal have separate GitHub audit issues. Their delivery comments link commits and verification evidence.', 'Keep this file updated alongside future development records.'], tags: ['roadmap', 'public page', 'developer experience'], links: [{ label: 'Design audit #45', url: 'https://github.com/ksayron/SenseiPlusPlus/issues/45' }, { label: 'Learning audit #46', url: 'https://github.com/ksayron/SenseiPlusPlus/issues/46' }, { label: 'Roadmap audit #47', url: 'https://github.com/ksayron/SenseiPlusPlus/issues/47' }] },
  { id: 'workbench', date: '2026-10-01', category: 'Design', title: 'A home for the whole learning loop', summary: 'The Coastal web workbench brings five views and the learning player onto shared visual foundations.', details: ['Local typography, light/night appearances, responsive layouts and shared controls now span the application.', 'Keyboard topic tabs, focus containment and input preservation are part of the implementation.', 'The implementation record reports desktop/tablet/phone checks; real-device and full screen-reader checks remain open.'], tags: ['SPA', 'Coastal', 'accessibility'], source: 'interface', milestone: 'workbench' },
  { id: 'functional-model', date: '2026-10-01', category: 'Decisions', title: 'Map the system, not just its screens', summary: 'An editable IDEF0 model describes the system context, its decomposition and the movement of evidence.', details: ['The four-page diagrams.net artifact includes context, decomposition, requirements coverage and flow checks.', 'The functional model is a design artifact; it does not imply that every modeled capability is implemented.'], tags: ['IDEF0', 'requirements', 'documentation'], source: 'model' },
  { id: 'visual-baseline', date: '2026-09-30', category: 'Design', title: 'Settle on Coastal', summary: 'The explored UI direction becomes an implementation guide, with light and night palettes and semantic roles.', details: ['The design guide defines typography, layout, component states and responsive behavior for the web workbench.', 'A documented visual baseline is separate from verifying the application that adopts it.'], tags: ['design system', 'tokens', 'Coastal'], source: 'interface', milestone: 'workbench' },
  { id: 'phase-one-checks', date: '2026-09-29', category: 'Verification', title: 'Try to break the learning journey', summary: 'Phase 1 verification covers concurrent starts, retries, rollback, owner isolation and browser recovery.', details: ['The implementation record reports 18 unit, 3 architecture and 25 PostgreSQL integration tests.', 'Seven client tests and 34 desktop/narrow browser journeys were recorded for that delivery.', 'These counts are dated evidence. The later SPA record has a separate, expanded browser run.'], tags: ['PostgreSQL', 'Playwright', 'recovery'], source: 'learning', milestone: 'learning' },
  { id: 'phase-one', date: '2026-09-29', category: 'Build', title: 'From topic to trustworthy evidence', summary: 'The complete Phase 1 runtime delivers six exercise formats, durable sessions, goals and shared roadmap coverage.', details: ['Versions are pinned to sessions. Operation receipts handle retries without duplicate accepted results.', 'Automatic evidence keeps objective results, assistance, self-review and manual observations distinct.', 'The engine also renders a synthetic orbital subject: C# is demonstration data rather than a built-in curriculum.'], tags: ['L1–L7', 'learning', 'evidence'], source: 'learning', milestone: 'learning' },
  { id: 'learning-scope', date: '2026-09-24', category: 'Decisions', title: 'Make the first phase small enough to finish', summary: 'Choose web-first, AI-independent practice with immediate feedback and 3 / 5 / 10-item sessions.', details: ['Keep subject knowledge in versioned declarative content and use existing Concept identities.', 'Scheduling, decay, exams, offline execution and optional AI belong to later learning phases.', 'A self-review cooldown controls evidence gain; it is not a spaced-repetition schedule.'], tags: ['scope', 'web first', 'AI independent'], source: 'delivery', milestone: 'learning' },
  { id: 'http-contracts', date: '2026-09-24', category: 'Build', title: 'Give every update a version', summary: 'The versioned API adopts ETags, If-Match, cursor envelopes and explicit Problem Details.', details: ['Missing, malformed and stale preconditions produce distinct 428, 400 and 412 responses.', 'Domain conflicts retain 409. Pagination cursors stay bound to their owner and filters.', 'Committed as 762cc4d, pull request #44.'], tags: ['API', 'concurrency', 'contracts'], source: 'architecture', milestone: 'contracts' },
  { id: 'test-foundations', date: '2026-09-21', category: 'Verification', title: 'Give the architecture a safety net', summary: 'Automated verification foundations arrive alongside the persisted modular backend.', details: ['Unit, architecture and integration checks establish a base for later learning work.', 'Committed as a62e482, pull request #43.'], tags: ['testing', '.NET', 'architecture'], source: 'architecture', milestone: 'foundations' },
  { id: 'persistence', date: '2026-09-21', category: 'Build', title: 'Keep the history after a restart', summary: 'The modular .NET baseline gets PostgreSQL persistence and explicit EF Core migrations.', details: ['Identity, Learning, WorkReflection, Evidence and Experience have clear module boundaries.', 'Committed foundations: 55dcd55 and ff78795, with persistence in pull request #42.'], tags: ['PostgreSQL', 'EF Core', 'backend'], source: 'architecture', milestone: 'foundations' },
]
