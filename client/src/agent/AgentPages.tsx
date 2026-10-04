import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ShieldCheck, ArrowRight, CloudOff, Unplug } from 'lucide-react'
import { ApiError, request } from '../api'
import { useAccount } from '../auth/AccountContext'
import { Button } from '../components/ui/button'
import './agent.css'

type Consent = { clientName: string; scopes: string[]; account: string }
type Connection = { id: string; createdAt: string; expiresAt: string; revoked: boolean }
type Imported = { title: string; context: string; role: string; actions: string; alternatives: string; outcome: string; uncertainty: string; assistance: string; eventDate: string; claimProvenance: string }

export function AgentConsentPage() {
  const account = useAccount()
  const ticket = new URLSearchParams(window.location.search).get('request') ?? ''
  const [consent, setConsent] = useState<Consent | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  useEffect(() => {
    let active = true
    request<Consent>(`/api/v1/auth/cli-consent/${encodeURIComponent(ticket)}`).then(value => { if (active) setConsent(value) }).catch(() => { if (active) setError('This connection request has expired. Start sign-in again from the CLI.') })
    return () => { active = false }
  }, [ticket])
  const decide = async (approve: boolean) => {
    setBusy(true); setError('')
    try {
      const result = await request<{ redirect: string }>(`/api/v1/auth/cli-consent/${encodeURIComponent(ticket)}`, { method: 'POST', body: JSON.stringify({ approve }) }, true)
      if (!result.redirect.startsWith('/connect/authorize?')) throw new Error('Invalid authorization response')
      window.location.assign(result.redirect)
    } catch { setError('Could not complete this connection. Retry sign-in from the CLI.'); setBusy(false) }
  }
  return <main className="agent-shell"><Link className="agent-back" to="/">Sensei++ / Connections</Link><section className="agent-panel">
    <p className="eyebrow">A deliberate connection</p><ShieldCheck size={36} /><h1>Connect your local evidence agent</h1>
    <p>Signed in as <strong>{account.email}</strong>. Connect only the CLI sign-in you just started.</p>
    {consent && <><h2>{consent.clientName}</h2><ul className="agent-permissions">{consent.scopes.some(scope => scope === 'openid' || scope === 'profile') && <li>Identify your account and read your display name.</li>}{consent.scopes.includes('concepts:read') && <li>Read the concept catalog.</li>}{consent.scopes.includes('professional-events:write') && <li>Send contributions you explicitly review and confirm.</li>}{consent.scopes.includes('professional-events:read') && <li>Read receipts for your submissions.</li>}{consent.scopes.includes('offline_access') && <li>Stay connected until you sign out, revoke access or the connection expires.</li>}</ul>
      <div className="agent-boundary"><CloudOff size={20} /><p>Watching stays local. Synchronization sends sanitized contribution fields. This connection cannot approve experience or change learning scores.</p></div>
      <div className="agent-actions"><Button disabled={busy} className="primary-button" onClick={() => void decide(true)}>{busy ? 'Connecting…' : 'Allow this connection'}<ArrowRight size={17} /></Button><Button disabled={busy} variant="ghost" onClick={() => void decide(false)}>Decline</Button></div></>}
    {error && <p role="alert">{error}</p>}
  </section></main>
}

export function ConnectionsPage() {
  const navigate = useNavigate()
  const [connections, setConnections] = useState<Connection[]>([])
  const [selected, setSelected] = useState<string | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  useEffect(() => { let active = true; request<Connection[]>('/api/v1/auth/connections').then(value => { if (active) setConnections(value) }).catch(() => { if (active) setError('Could not load connections. Reconnect and retry.') }); return () => { active = false } }, [])
  const revoke = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault(); const password = String(new FormData(event.currentTarget).get('password'))
    setBusy(true); setError('')
    try {
      await request('/api/v1/auth/reauthenticate', { method: 'POST', body: JSON.stringify({ password }) }, true)
      await request(`/api/v1/auth/connections/${selected}/revoke`, { method: 'POST' }, true)
      setConnections(items => items.map(item => item.id === selected ? { ...item, revoked: true } : item)); setSelected(null)
    } catch { setError('Could not revoke this connection. Check your password and retry.') }
    finally { setBusy(false) }
  }
  return <main className="agent-shell"><Link className="agent-back" to="/">← Workspace</Link><header><p className="eyebrow">Your account, your connections</p><h1>Connected evidence agents</h1><p>Revoke a connection to stop its access. Saved drafts and local evidence stay intact.</p></header>
    {error && <p role="alert">{error}</p>}<section className="agent-connection-list">{connections.map(item => <article className="agent-panel" key={item.id}><header><Unplug size={22} /><h2>Local evidence agent</h2><span className="status-pill">{item.revoked ? 'Revoked' : new Date(item.expiresAt) <= new Date() ? 'Expired' : 'Connected'}</span></header><p>Connected {new Date(item.createdAt).toLocaleDateString()} · Expires {new Date(item.expiresAt).toLocaleDateString()}</p>{!item.revoked && <Button variant="ghost" onClick={() => setSelected(item.id)}>Revoke connection</Button>}</article>)}{connections.length === 0 && !error && <p>No CLI connections yet. Start with <code>auth login --server URL</code> in the agent.</p>}</section>
    {selected && <section className="agent-panel"><h2>Confirm revocation</h2><form className="editor-form" onSubmit={event => void revoke(event)}><label className="field"><span>Confirm your account password</span><input name="password" type="password" autoComplete="current-password" required maxLength={256} disabled={busy} /></label><div className="agent-actions"><Button type="submit" disabled={busy}>{busy ? 'Revoking…' : 'Confirm revocation'}</Button><Button variant="ghost" disabled={busy} onClick={() => setSelected(null)}>Cancel</Button></div></form></section>}
    <Button variant="ghost" onClick={() => navigate('/')}>Back to workspace</Button>
  </main>
}

export function ImportedContribution({ entryId }: { entryId: string }) {
  const [original, setOriginal] = useState<Imported | null>(null)
  const [error, setError] = useState('')
  useEffect(() => {
    let active = true
    request<Imported>(`/api/v1/experience/entries/${entryId}/import`).then(value => { if (active) setOriginal(value) }).catch(err => { if (active && !(err instanceof ApiError && err.status === 404)) setError('Could not verify import provenance. Reconnect to inspect it.') })
    return () => { active = false }
  }, [entryId])
  if (error) return <p role="alert">{error}</p>
  if (!original) return null
  return <details className="imported-contribution"><summary><ShieldCheck size={16} />Client reported · Not assessed<span>{original.eventDate}</span></summary><p>Original imported revision. Journal edits and approval do not establish an assessment.</p><dl>{(['title', 'context', 'role', 'actions', 'alternatives', 'outcome', 'uncertainty', 'assistance'] as const).map(field => <div key={field}><dt>{field[0].toUpperCase() + field.slice(1)}</dt><dd>{original[field] || 'Not supplied'}</dd></div>)}</dl></details>
}
