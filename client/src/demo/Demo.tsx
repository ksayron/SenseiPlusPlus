import { useEffect, useRef, useState } from 'react'
import { ArrowLeft, ArrowRight, BookOpen, Check, ChevronRight, Code2, FileText, FlaskConical, Layers3, Lightbulb, LoaderCircle, LockKeyhole, MessageSquareText, RotateCcw, ShieldCheck, Sparkles } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Textarea } from '@/components/ui/textarea'
import { Input } from '@/components/ui/input'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import { Dialog, DialogTrigger, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter, DialogClose } from '@/components/ui/dialog'
import { Accordion, AccordionItem, AccordionTrigger, AccordionContent } from '@/components/ui/accordion'
import { Switch } from '@/components/ui/switch'
import { Progress } from '@/components/ui/progress'
import { Alert, AlertTitle, AlertDescription } from '@/components/ui/alert'
import { episode, loadDraft, requestFixtureFeedback, saveDraft, type Draft } from './fixture'
import { ThemePicker } from './ThemePicker'
import { applyTheme, loadTheme, persistTheme, resolveTheme, themes, type ThemeId } from './themes'

function GuidanceDialog({ onHint }: { onHint: () => void }) {
  return <Dialog><DialogTrigger render={<Button variant="outline" />}><Lightbulb size={16} /> Get a hint</DialogTrigger>
    <DialogContent><DialogHeader><p className="demo-eyebrow">A little direction</p><DialogTitle>Follow the failure boundary.</DialogTitle><DialogDescription>This is curated sample guidance. Opening it marks your next response as assisted.</DialogDescription></DialogHeader>
      <div className="demo-hint"><Lightbulb /><p>Imagine the invoice exists, but the completion marker does not. Walk through the same handler a second time. Which operation needs its own guarantee?</p></div>
      <p>Consider a unique order identifier at the side-effect boundary, or a transaction where both operations can share one.</p>
      <DialogFooter><DialogClose render={<Button onClick={onHint} />}>Use this hint <ArrowRight size={16} /></DialogClose></DialogFooter>
    </DialogContent></Dialog>
}

function SourceContext() {
  return <section className="demo-context" aria-labelledby="context-heading">
    <div className="demo-section-label"><Code2 size={17} /><h2 id="context-heading">The context</h2><Badge variant="secondary">Source v3</Badge></div>
    <p className="demo-context-summary">{episode.summary}</p>
    <div className="demo-source"><header><span><FileText size={14} /> {episode.file}</span><span>Selected excerpt</span></header>
      <pre tabIndex={0} aria-label="C sharp source excerpt">{episode.code.map((line, index) => <span className={`demo-code-line ${index === 5 || index === 6 ? 'demo-code-emphasis' : ''}`} key={index}><span aria-hidden="true">{index + 1}</span><code>{line}</code></span>)}</pre>
      <footer><span className="demo-code-dot" /> The gap between these two writes matters.</footer>
    </div>
    <Accordion><AccordionItem value="assumptions"><AccordionTrigger>Constraints & assumptions</AccordionTrigger><AccordionContent><ul className="demo-assumptions"><li>Messages can be delivered more than once.</li><li>The worker may stop at any point.</li><li>Invoice creation and completion use separate writes.</li><li>No shared transaction is shown in this excerpt.</li></ul></AccordionContent></AccordionItem></Accordion>
    <div className="demo-context-note"><LockKeyhole size={15} /><p>Sanitized sample context.<br /><span>No repository connection needed.</span></p></div>
  </section>
}

function Reflection() {
  const [draft, setDraft] = useState<Draft>(loadDraft)
  const [saved, setSaved] = useState('Local draft ready')
  const [feedback, setFeedback] = useState<string | null>(null)
  const [feedbackAnswer, setFeedbackAnswer] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [invalid, setInvalid] = useState(false)
  const [simulateFailure, setSimulateFailure] = useState(false)
  const [helpOpened, setHelpOpened] = useState(false)
  const answerRef = useRef<HTMLTextAreaElement>(null)
  const latestDraft = useRef(draft)
  latestDraft.current = draft

  useEffect(() => {
    const timer = window.setTimeout(() => {
      try { saveDraft(draft); setSaved('Saved on this device') }
      catch { setSaved('Could not save on this device. Keep this tab open.') }
    }, 400)
    return () => window.clearTimeout(timer)
  }, [draft])

  const update = (change: Partial<Draft>) => {
    setDraft((current) => ({ ...current, ...change, acknowledged: false }))
    setSaved('Saving locally…')
  }

  const requestFeedback = async (forceSuccess = false) => {
    if (!draft.answer.trim()) { setInvalid(true); answerRef.current?.focus(); return }
    setInvalid(false)
    const submitted = draft.answer
    const submittedDraft = { ...draft, firstAttempt: draft.firstAttempt ?? submitted }
    setDraft(submittedDraft)
    try { saveDraft(submittedDraft); setSaved('Saved on this device') }
    catch { setSaved('Could not save on this device. Keep this tab open.') }
    setPending(true); setError(null); setFeedback(null)
    try { setFeedback(await requestFixtureFeedback(simulateFailure && !forceSuccess)); setFeedbackAnswer(submitted) }
    catch (cause) { setError(cause instanceof Error ? cause.message : 'Feedback unavailable.') }
    finally { setPending(false) }
  }

  useEffect(() => {
    const persist = () => { try { saveDraft(latestDraft.current) } catch { /* Save error is surfaced by autosave. */ } }
    window.addEventListener('pagehide', persist)
    return () => { persist(); window.removeEventListener('pagehide', persist) }
  }, [])

  return <>
    <div className="demo-heading"><div><p className="demo-eyebrow">Work reflection / Personal project</p><h1>{episode.title}</h1><p>A small pause to understand the work behind the change.</p></div><Badge variant="outline" className="demo-private"><LockKeyhole size={13} /> Private draft</Badge></div>
    <div className="demo-session-strip"><div><span className="demo-step-number">01</span><strong>Explore the failure boundary</strong><span className="demo-session-secondary">One focused question</span></div><span><span className="demo-live-dot" /> In progress</span></div>
    <div className="demo-workspace"><div className="demo-desktop-context"><SourceContext /></div><Accordion className="demo-mobile-context"><AccordionItem value="source"><AccordionTrigger><span><Code2 size={17} /> View source context <Badge variant="secondary">v3</Badge></span></AccordionTrigger><AccordionContent><SourceContext /></AccordionContent></AccordionItem></Accordion>
      <section className="demo-response" aria-labelledby="question-heading"><div className="demo-response-label"><span className="demo-eyebrow">Your reasoning</span><Badge variant="secondary" className={draft.assisted ? 'demo-status-attention' : ''}>{draft.assisted ? 'Assisted revision' : 'First attempt'}</Badge></div>
        <h2 id="question-heading">{episode.question}</h2><p className="demo-question-note">Trace what a retry would do. Assumptions and unanswered questions are welcome.</p>
        <label className="demo-input-label" htmlFor="reflection-answer">Your explanation</label><Textarea id="reflection-answer" ref={answerRef} value={draft.answer} onChange={(event) => { update({ answer: event.target.value }); setInvalid(false) }} aria-invalid={invalid} aria-describedby={invalid ? 'answer-error' : 'answer-note'} placeholder="I’d start by looking at what has already been persisted…" className="demo-answer" />
        {invalid && <p id="answer-error" className="demo-field-error">Write an explanation before requesting feedback.</p>}
        <div className="demo-answer-footer"><span id="answer-note">Your words. No perfect answer required.</span><span role="status"><Check size={13} /> {saved}</span></div>
        <div className="demo-help-row"><Dialog onOpenChange={(open) => { if (open) { setHelpOpened(true); update({ assisted: true }) } }}><DialogTrigger render={<Button variant="outline" />}><Lightbulb size={16} /> Get a hint</DialogTrigger><DialogContent><DialogHeader><p className="demo-eyebrow">A little direction</p><DialogTitle>Follow the failure boundary.</DialogTitle><DialogDescription>Curated sample guidance. Your response is now labeled as assisted.</DialogDescription></DialogHeader><div className="demo-hint"><Lightbulb size={22} /><p>Imagine the invoice exists, but the completion marker does not. Walk through the handler again. Which side effect needs its own guarantee?</p></div><p>Consider how a stable order identifier could prevent creating a second invoice across worker restarts.</p><DialogFooter><DialogClose render={<Button />}>Back to my answer <ArrowRight size={16} /></DialogClose></DialogFooter></DialogContent></Dialog><span>{helpOpened || draft.assisted ? 'Help use is recorded with your draft.' : 'A hint is available when you need it.'}</span></div>
        <Accordion className="demo-unresolved"><AccordionItem value="uncertainty"><AccordionTrigger><span><MessageSquareText size={16} /> Keep an open question {draft.unresolved && <Badge variant="secondary" className="demo-status-attention">Unresolved</Badge>}</span></AccordionTrigger><AccordionContent><label className="demo-input-label" htmlFor="open-question">What would you investigate next?</label><Textarea id="open-question" value={draft.unresolved} onChange={(event) => update({ unresolved: event.target.value })} placeholder="For example: can these writes share a transaction?" rows={3} /><p>Uncertainty stays visible. It is useful context for a follow-up.</p></AccordionContent></AccordionItem></Accordion>
        {error && <Alert variant="destructive" className="demo-feedback-error"><RotateCcw size={18} /><AlertTitle>Feedback unavailable</AlertTitle><AlertDescription><p>{error}</p><Button variant="outline" size="sm" onClick={() => { setSimulateFailure(false); void requestFeedback(true) }} disabled={pending}>Retry feedback</Button></AlertDescription></Alert>}
        {feedback && <div className="demo-feedback" role="status"><div><Sparkles size={17} /><strong>Sample feedback</strong><Badge variant="outline">Fixture</Badge></div><p>{feedback}</p><small>{draft.answer !== feedbackAnswer ? 'Your answer has changed since this feedback was requested.' : 'A fixed demonstration prompt, not an assessment of your answer.'}</small></div>}
        <div className="demo-response-actions"><span><ShieldCheck size={16} /> Your answer stays yours.</span><Button onClick={() => void requestFeedback()} disabled={pending} focusableWhenDisabled>{pending ? <><LoaderCircle className="animate-spin" size={16} /> Getting sample feedback</> : <>Get sample feedback <ArrowRight size={16} /></>}</Button></div>
      </section>
    </div>
    <div className="demo-lower-grid"><section className="demo-guidance"><div className="demo-guidance-icon"><BookOpen size={25} /></div><div><p className="demo-eyebrow">Make room for the unknown</p><h3>You don’t need to resolve everything today.</h3><p>Capture the boundary you understand and the question you want to carry forward.</p></div></section><section className="demo-simulation"><div><FlaskConical size={17} /><label htmlFor="failure-mode">Try a feedback timeout</label><Switch id="failure-mode" checked={simulateFailure} onCheckedChange={setSimulateFailure} /></div><p>A demo control to explore recovery. Your answer stays saved.</p></section></div>
    <section className="demo-acknowledgement"><div><p className="demo-eyebrow">Finish on your terms</p><h3>Record that you reflected on this context.</h3><p>{episode.sourceVersion} · {draft.unresolved ? 'Includes your unresolved question' : 'Any uncertainty can stay open'} · Does not approve code safety.</p></div><Dialog><DialogTrigger render={<Button variant="outline" disabled={!draft.answer.trim() || pending || draft.acknowledged} />}>{draft.acknowledged ? <><Check size={16} /> Reflection acknowledged</> : 'Review & acknowledge'}</DialogTrigger><DialogContent><DialogHeader><DialogTitle>Acknowledge this reflection?</DialogTitle><DialogDescription>Your acknowledgement refers only to {episode.sourceVersion}. It records reflection on the context and does not certify the code.</DialogDescription></DialogHeader><div className="demo-review"><strong>Your explanation</strong><p>{draft.answer}</p>{draft.unresolved && <><Badge variant="secondary" className="demo-status-attention">Unresolved question</Badge><p>{draft.unresolved}</p></>}<p>Attempt: {draft.assisted ? 'assisted' : 'without in-app help'}. Feedback: {feedback ? 'sample prompt received' : 'not received'}.</p></div><DialogFooter><DialogClose render={<Button variant="outline" />}>Keep reflecting</DialogClose><DialogClose render={<Button onClick={() => setDraft((current) => ({ ...current, acknowledged: true }))} />}>Acknowledge reflection</DialogClose></DialogFooter></DialogContent></Dialog></section>
  </>
}

function Gallery({ themeId }: { themeId: ThemeId }) {
  const [selected, setSelected] = useState(false)
  const palette = (themes.find((theme) => theme.id === themeId) ?? themes[0]).tokens
  return <div className="demo-gallery"><div className="demo-heading"><div><p className="demo-eyebrow">The shared foundations</p><h1>A little kit. A coherent campus.</h1><p>Working components, with room for real content and honest states.</p></div><Badge variant="outline">shadcn/ui + Base UI</Badge></div>
    <div className="demo-gallery-grid"><section><p className="demo-eyebrow">01 / Actions</p><h2>One clear next step.</h2><div className="demo-gallery-row"><Button onClick={() => setSelected(!selected)}>{selected ? <><Check /> Selected</> : <>Continue <ArrowRight /></>}</Button><Button variant="outline">Secondary</Button><Button variant="ghost">Quiet action</Button><Button disabled>Unavailable</Button></div><p>The palette’s primary accent holds the next action. Keyboard focus remains visible.</p></section>
    <section><p className="demo-eyebrow">02 / Meaningful states</p><h2>Labels carry the meaning.</h2><div className="demo-gallery-row"><Badge variant="secondary">Untested</Badge><Badge variant="secondary" className="demo-status-success">Saved</Badge><Badge variant="secondary" className="demo-status-attention">Assisted</Badge><Badge variant="outline">Draft</Badge><Badge variant="secondary" className="demo-status-error">Provider error</Badge></div><p>Unknown is neutral. Saved input and reviewed content are separate.</p></section>
    <section><p className="demo-eyebrow">03 / Writing</p><h2>Space to think.</h2><label htmlFor="gallery-title" className="demo-input-label">Episode title</label><Input id="gallery-title" placeholder="A decision worth revisiting" /><label htmlFor="gallery-note" className="demo-input-label">Reflection</label><Textarea id="gallery-note" placeholder="Explain the trade-off in your own words…" rows={4} /></section>
    <section><p className="demo-eyebrow">04 / Interaction</p><h2>Guidance on demand.</h2><GuidanceDialog onHint={() => setSelected(true)} /><Accordion><AccordionItem value="detail"><AccordionTrigger>What makes a useful explanation?</AccordionTrigger><AccordionContent>Connect the constraints, the decision, and its consequences. State what you have not verified yet.</AccordionContent></AccordionItem></Accordion><label className="demo-gallery-switch"><Switch checked={selected} onCheckedChange={setSelected} /> Include contextual guidance</label></section>
    <section><p className="demo-eyebrow">05 / Activity progress</p><h2>A position, not a competence score.</h2><div className="demo-progress-label"><span>Question 1 of 3</span><span>Reflection session</span></div><Progress value={1} max={3} aria-label="Reflection session progress" /><p>Progress describes this activity only.</p></section>
    <section><p className="demo-eyebrow">06 / Recovery</p><h2>Keep the work. Explain the next step.</h2><Alert><Lightbulb size={16} /><AlertTitle>Feedback is taking longer</AlertTitle><AlertDescription>Your input is saved locally. You can return to the reflection and try the timeout recovery flow.</AlertDescription></Alert></section></div>
    <section className="demo-token-swatches" aria-label="Sensei color roles">{[['Canvas', palette.background], ['Reading', palette.card], ['Anchor', palette.sidebar], ['Action', palette.primary], ['Guidance', palette.guidance]].map(([name, color]) => <div key={name}><span style={{ background: color }} /><strong>{name}</strong><code>{color}</code></div>)}</section>
  </div>
}

export function Demo() {
  const [storedThemeId, setThemeId] = useState<ThemeId>(loadTheme)
  const themeId = resolveTheme(storedThemeId)
  const changeTheme = (id: ThemeId) => {
    applyTheme(id)
    persistTheme(id)
    setThemeId(id)
  }
  return <Tabs defaultValue="reflection" className="demo-shell">
    <a className="demo-skip" href="#demo-main">Skip to content</a>
    <aside className="demo-sidebar">
      <a className="demo-brand" href="/ui-demo.html"><span className="demo-brand-mark">S<span>++</span></span><span>Sensei<span>++</span><small>Room to grow.</small></span></a>
      <p className="demo-nav-caption">Your workspace</p>
      <TabsList className="demo-navigation" aria-label="Demo sections"><TabsTrigger value="reflection"><MessageSquareText size={18} /><span>Reflection studio</span><ChevronRight size={14} /></TabsTrigger><TabsTrigger value="gallery"><Layers3 size={18} /><span>Component gallery</span><ChevronRight size={14} /></TabsTrigger></TabsList>
      <div className="demo-sidebar-bottom"><div className="demo-campus-mark" aria-hidden="true"><span /><span /><span /></div><h3>Good work starts<br />with a good question.</h3><p>A little curiosity goes a long way.</p><a href="/"><ArrowLeft size={15} /> Open the application</a></div>
      <div className="demo-sidebar-foot"><span className="demo-live-dot" /> UI demo <span>Local fixtures</span></div>
    </aside>
    <div className="demo-main">
      <header className="demo-topbar"><span>Workspace <ChevronRight size={13} /> <strong>Design preview</strong></span><div><ThemePicker themeId={themeId} onChange={changeTheme} /><Badge variant="secondary" className="demo-fixture-badge"><FlaskConical size={13} /> Interactive demo</Badge><span className="demo-avatar" aria-label="Sample user">JD</span></div></header>
      <main id="demo-main" tabIndex={-1}><TabsContent value="reflection"><Reflection /></TabsContent><TabsContent value="gallery"><Gallery themeId={themeId} /></TabsContent><footer className="demo-footer"><span>Sensei++ · Living Campus</span><span>Sample data · Stored on this device · No AI calls</span></footer></main>
    </div>
  </Tabs>
}
