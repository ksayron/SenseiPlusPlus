import { useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ArrowDown, ArrowLeft, ArrowRight, ArrowUpRight, BookOpen, Check, ChevronDown, Circle, Compass, FileText, GitBranch, Search, X } from 'lucide-react'
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '../components/ui/dialog'
import { devLogs, futureTracks, milestones, sources, type LogCategory, type SourceId } from './roadmap-data'
import './roadmap.css'

const categories: Array<'All entries' | LogCategory> = ['All entries', 'Build', 'Design', 'Decisions', 'Verification']
const statusLabel = { delivered: 'Delivered locally', next: 'Up next', later: 'Later' }
const formatDate = (date: string) => new Intl.DateTimeFormat('en', { day: '2-digit', month: 'short', timeZone: 'UTC' }).format(new Date(`${date}T12:00:00Z`))

export default function DevRoadmapPage() {
  const [params, setParams] = useSearchParams()
  const selected = milestones.find(milestone => milestone.id === params.get('milestone')) ?? milestones[2]
  const [category, setCategory] = useState<typeof categories[number]>('All entries')
  const [query, setQuery] = useState('')
  const [source, setSource] = useState<SourceId | null>(null)
  const [sourceText, setSourceText] = useState('')
  const [sourceError, setSourceError] = useState(false)
  const sourceRequest = useRef(0)
  const detailRef = useRef<HTMLElement>(null)
  const headingRef = useRef<HTMLHeadingElement>(null)
  const visibleLogs = devLogs.filter(log => (category === 'All entries' || category === log.category) &&
    `${log.title} ${log.summary} ${log.details.join(' ')} ${log.tags.join(' ')}`.toLowerCase().includes(query.trim().toLowerCase()))
  const delivered = milestones.filter(milestone => milestone.status === 'delivered').length

  useEffect(() => {
    document.title = 'Development roadmap · Sensei++'
    headingRef.current?.focus({ preventScroll: true })
  }, [])

  function selectMilestone(id: string, scroll = false) {
    const next = new URLSearchParams(params)
    next.set('milestone', id)
    setParams(next, { preventScrollReset: true })
    if (scroll) {
      detailRef.current?.scrollIntoView({ block: 'center', behavior: 'instant' })
      detailRef.current?.focus({ preventScroll: true })
    }
  }

  async function openSource(id: SourceId) {
    const request = ++sourceRequest.current
    setSource(id)
    setSourceText('')
    setSourceError(false)
    try {
      const content = await sources[id].load()
      if (request === sourceRequest.current) setSourceText(content)
    } catch {
      if (request === sourceRequest.current) setSourceError(true)
    }
  }

  return (
    <div className="rd-page">
      <a className="rd-skip" href="#rd-main">Skip to roadmap</a>
      <header className="rd-topbar">
        <Link to="/" className="rd-brand" aria-label="Sensei++ home">Sensei<span>++</span><span className="rd-brand-divider" /><small>THE BUILD JOURNAL</small></Link>
        <nav aria-label="Roadmap navigation"><a href="#journey">Roadmap</a><a href="#journal">Dev log</a><Link to="/" className="rd-return">Open app <ArrowUpRight size={16} /></Link></nav>
      </header>
      <main id="rd-main">
        <section className="rd-hero">
          <div className="rd-hero-copy">
            <p className="rd-kicker"><span className="rd-live-dot" /> A WORK IN PROGRESS, OUT IN THE OPEN</p>
            <h1 ref={headingRef} tabIndex={-1}>The making of<br /><em>Sensei++.</em><span className="rd-asterisk" aria-hidden="true">✳</span></h1>
            <p className="rd-intro">From a useful idea to a learning companion.<br className="rd-desktop-break" /> Follow the work, the decisions, and what comes next.</p>
            <a className="rd-primary" href="#journey">Explore the journey <ArrowDown size={18} /></a>
            <div className="rd-hero-foot"><span>EST. SEPTEMBER 2026</span><span>LOCAL FIRST. BUILT WITH INTENT.</span></div>
          </div>
          <div className="rd-cover">
            <div className="rd-cover-top"><span>FIELD NOTES / 001</span><Compass size={23} strokeWidth={1.3} /></div>
            <div className="rd-map" aria-hidden="true">
              <svg viewBox="0 0 360 300" fill="none">
                <circle cx="180" cy="150" r="125" /><circle cx="180" cy="150" r="90" /><circle cx="180" cy="150" r="53" />
                <path d="M20 150H340M180 10V290M65 35L295 265M65 265L295 35" className="rd-map-grid" />
                <path d="M58 223C100 225 90 142 138 139S201 212 225 166 240 70 300 72" className="rd-map-trail" />
                <circle cx="58" cy="223" r="7" className="rd-map-stop" /><circle cx="138" cy="139" r="7" className="rd-map-stop" /><circle cx="225" cy="166" r="7" className="rd-map-stop" /><circle cx="300" cy="72" r="10" className="rd-map-destination" />
                <text x="36" y="249">IDEA</text><text x="113" y="117">BUILD</text><text x="211" y="193">LEARN</text><text x="272" y="49">NEXT</text>
              </svg>
              <span className="rd-map-stamp">small steps.<br /><em>real progress.</em></span>
            </div>
            <p>Learn something.<br />Use it. Keep the evidence.</p>
            <div className="rd-cover-bottom"><span>THE CORE LOOP</span><ArrowUpRight size={22} /></div>
          </div>
        </section>
        <div className="rd-snapshot" aria-label="Project snapshot">
          <div><span className="rd-stat-number">{String(delivered).padStart(2, '0')}<small> / {String(milestones.length).padStart(2, '0')}</small></span><span>tracked checkpoints delivered</span></div>
          <div><span className="rd-stat-number">06</span><span>exercise formats in Phase 1</span></div>
          <div><span className="rd-stat-number">{String(devLogs.length).padStart(2, '0')}</span><span>notes from the build</span></div>
          <div className="rd-snapshot-note"><GitBranch size={19} /><span>Repository snapshot<br /><strong>02 October 2026</strong></span></div>
        </div>
        <section className="rd-journey" id="journey">
          <div className="rd-section-heading"><div><p className="rd-kicker">01 / THE JOURNEY</p><h2>A little further, every build.</h2></div><p>Pick a checkpoint to see what changed.<br />Dates record delivery, not promises.</p></div>
          <div className="rd-trail" role="group" aria-label="Development milestones">
            {milestones.map(milestone => (
              <button key={milestone.id} className={`rd-stop ${selected.id === milestone.id ? 'rd-stop-selected' : ''} rd-stop-${milestone.status}`} aria-pressed={selected.id === milestone.id} aria-controls="rd-milestone-detail" onClick={() => selectMilestone(milestone.id)}>
                <span className="rd-stop-top"><span>{milestone.number}</span><span className="rd-stop-marker">{milestone.status === 'delivered' ? <Check size={17} /> : <ArrowRight size={17} />}</span></span>
                <span className="rd-stop-date">{milestone.period}</span><strong>{milestone.title}</strong><span className="rd-stop-subtitle">{milestone.subtitle}</span><span className="rd-stop-status">{statusLabel[milestone.status]}<ArrowUpRight size={14} /></span>
              </button>
            ))}
          </div>
          <article className="rd-detail" id="rd-milestone-detail" ref={detailRef} tabIndex={-1} aria-label={`${selected.title} details`}>
            <div className="rd-detail-copy"><span className={`rd-badge rd-badge-${selected.status}`}>{selected.status === 'delivered' ? <Check size={13} /> : <Circle size={12} />}{statusLabel[selected.status]}</span><h3>{selected.title}</h3><p>{selected.description}</p><strong className="rd-outcome"><ArrowRight size={17} />{selected.outcome}</strong></div>
            <div className="rd-detail-list"><p className="rd-kicker">{selected.status === 'delivered' ? 'WHAT LANDED' : 'WHAT IT WILL TAKE'}</p><ul>{selected.items.map(item => <li key={item}>{selected.status === 'delivered' ? <Check size={15} /> : <Circle size={12} />}<span>{item}</span></li>)}</ul><details className="rd-evidence"><summary>Evidence & delivery notes <ChevronDown size={15} /></summary><p>{selected.evidence}</p><button className="rd-text-button" onClick={() => void openSource(selected.source)}><FileText size={15} />Read the source record <ArrowUpRight size={14} /></button></details></div>
          </article>
          <details className="rd-horizon"><summary><span><Compass size={20} />Beyond the next checkpoint</span><span>Learning phases 2–6 <ChevronDown size={17} /></span></summary><p className="rd-horizon-intro">Planned directions from the learning design. Sequencing depends on scope and capacity; no delivery dates are assigned.</p><div className="rd-future-grid">{futureTracks.map(track => <article key={track.phase}><span>PHASE {track.phase}</span><h3>{track.title}</h3><p>{track.detail}</p></article>)}</div><button className="rd-text-button" onClick={() => void openSource('delivery')}>Read the delivery plan <ArrowUpRight size={14} /></button></details>
        </section>
        <section className="rd-journal" id="journal">
          <aside className="rd-journal-aside"><p className="rd-kicker">02 / THE FIELD NOTES</p><h2>Behind<br /><em>the build.</em></h2><p>The things we shipped, the choices we made, and the checks that kept us honest.</p><div className="rd-journal-note"><BookOpen size={22} /><p>A curated development log, sourced from repository history and project records.</p><span>Latest entry <strong>02 OCT 2026</strong></span></div><a href="#journey" className="rd-text-link">Back to the roadmap <ArrowUpRight size={15} /></a></aside>
          <div className="rd-log-main">
            <div className="rd-log-tools"><label className="rd-search"><Search size={18} /><span className="sr-only">Search development log</span><input type="search" value={query} onChange={event => setQuery(event.target.value)} placeholder="Search notes, decisions, tools…" /></label><div className="rd-filters" role="group" aria-label="Filter development log">{categories.map(item => <button key={item} aria-pressed={category === item} onClick={() => setCategory(item)}>{item}</button>)}</div><p className="rd-results" role="status">{visibleLogs.length} {visibleLogs.length === 1 ? 'entry' : 'entries'}{query.trim() ? ` matching “${query.trim()}”` : ' in the notebook'}</p></div>
            <div className="rd-log-entries">{visibleLogs.map(log => <article className="rd-log-entry" key={log.id}><div className="rd-log-meta"><time dateTime={log.date}>{formatDate(log.date)} <span>2026</span></time><span className={`rd-category rd-category-${log.category.toLowerCase()}`}>{log.category}</span></div><h3>{log.title}</h3><p>{log.summary}</p><div className="rd-tags">{log.tags.map(tag => <span key={tag}>{tag}</span>)}</div><details className="rd-log-details"><summary>Read field note <ChevronDown size={15} /></summary><ul>{log.details.map(detail => <li key={detail}>{detail}</li>)}</ul><div className="rd-log-links">{log.links?.map(link => <a className="rd-text-link" key={link.url} href={link.url} target="_blank" rel="noreferrer">{link.label}<ArrowUpRight size={14} /></a>)}{log.source && <button className="rd-text-button" onClick={() => void openSource(log.source!)}><FileText size={15} />Source record <ArrowUpRight size={14} /></button>}{log.milestone && <button className="rd-text-button" onClick={() => selectMilestone(log.milestone!, true)}>View checkpoint <ArrowUpRight size={14} /></button>}</div></details></article>)}</div>
            {visibleLogs.length === 0 && <div className="rd-empty"><Search size={30} /><h3>No notes on that page.</h3><p>Try a different phrase or look across all categories.</p><button className="rd-primary" onClick={() => { setQuery(''); setCategory('All entries') }}>Reset filters <X size={16} /></button></div>}
          </div>
        </section>
        <section className="rd-closing"><span aria-hidden="true">✳</span><div><p className="rd-kicker">STILL WRITING THE NEXT CHAPTER.</p><h2>Progress is a practice, too.</h2></div><Link to="/learn">Go learn something <ArrowUpRight size={19} /></Link></section>
      </main>
      <footer className="rd-footer"><Link to="/" aria-label="Return to Sensei++"><ArrowLeft size={14} />Sensei++</Link><p>Curated snapshot · local delivery ≠ hosted release · no live CI connection</p><a href="#rd-main">Back to top ↑</a></footer>
      <Dialog open={source !== null} onOpenChange={open => { if (!open) { ++sourceRequest.current; setSource(null) } }}>
        <DialogContent className="rd-source-dialog"><DialogTitle>{source ? sources[source].title : 'Source record'}</DialogTitle><DialogDescription>{source ? sources[source].path : ''} · Repository document bundled with this build.</DialogDescription>{sourceError ? <div role="alert"><p>The record could not be loaded.</p><button className="rd-text-button" onClick={() => source && void openSource(source)}>Try again</button></div> : sourceText ? <pre className="rd-source-text" tabIndex={0} aria-label="Source document">{sourceText}</pre> : <p role="status">Opening the source record…</p>}</DialogContent>
      </Dialog>
    </div>
  )
}
