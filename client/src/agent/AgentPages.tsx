import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ShieldCheck, ArrowRight, CloudOff, Unplug } from 'lucide-react'
import { ApiError, request } from '../api'
import { useAccount } from '../auth/AccountContext'
import { AccountConfirmation } from '../auth/ExternalAuthentication'
import { startExternal, type AuthenticationMethods, type Provider } from '../auth/external'
import { safeExternalUrl } from '../lib/safeExternalUrl'
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
  const [methods, setMethods] = useState<AuthenticationMethods | null>(null)
  const [providers, setProviders] = useState<Provider[]>([])
  const [selected, setSelected] = useState<{ kind: 'revoke' | 'disconnect' | 'link'; value: string } | null>(null)
  const [confirm, setConfirm] = useState(false)
  const [reload, setReload] = useState(0)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  useEffect(() => {
    let active = true
    Promise.all([request<Connection[]>('/api/v1/auth/connections', undefined, true), request<AuthenticationMethods>('/api/v1/auth/methods', undefined, true), request<Provider[]>('/api/v1/auth/providers')])
      .then(([agents, accountMethods, available]) => { if (active) { setConnections(agents); setMethods(accountMethods); setProviders(available); setError('') } })
      .catch(() => { if (active) setError('Could not load connections. Reconnect and retry.') })
    return () => { active = false }
  }, [reload])
  const perform = async () => {
    if (!selected) return
    setBusy(true); setError('')
    try {
      if (selected.kind === 'link') { await startExternal(selected.value, 'link', '/connections'); return }
      const path = selected.kind === 'revoke' ? `/api/v1/auth/connections/${selected.value}/revoke` : `/api/v1/auth/external/${selected.value}/disconnect`
      await request(path, { method: 'POST' }, true)
      setSelected(null); setConfirm(false); setReload(value => value + 1)
    } catch (err) { setError(err instanceof Error ? err.message : 'Could not change this connection. Retry.') }
    finally { setBusy(false) }
  }
  const continueAction = async () => {
    setBusy(true); setError('')
    try { await request('/api/v1/auth/reauthentication', undefined, true); await perform() }
    catch (err) { if (err instanceof ApiError && err.status === 403) setConfirm(true); else setError(err instanceof Error ? err.message : 'Could not confirm your account.') }
    finally { setBusy(false) }
  }
  const refreshProfile = async (provider: string) => {
    setBusy(true); setError('')
    try { await startExternal(provider, 'refresh', '/connections') }
    catch (err) { setError(err instanceof Error ? err.message : 'Could not refresh this profile.'); setBusy(false) }
  }
  const choose = (action: NonNullable<typeof selected>) => { setSelected(action); setConfirm(false); setError('') }
  return <main className="agent-shell"><Link className="agent-back" to="/">← Workspace</Link><header><p className="eyebrow">Your account, your connections</p><h1>Connections</h1><p>Manage how you sign in and which evidence agents can access your account.</p></header>
    {error && <p className="auth-error" role="alert">{error} <Button variant="ghost" onClick={() => setReload(value => value + 1)}>Reload connections</Button></p>}
    {methods && <section aria-labelledby="signin-profiles-title"><h2 id="signin-profiles-title">Sign-in profiles</h2><p>{methods.hasPassword ? 'Email and password are available for this account.' : 'You sign in through a connected provider.'}</p>
      <div className="agent-connection-list">{methods.profiles.map(profile => <article className="agent-panel" key={profile.provider}><header><ShieldCheck size={22} aria-hidden="true" /><h2>{profile.provider}</h2><span className="status-pill">{profile.enabled ? 'Connected profile' : 'Provider disabled'}</span></header>
        <p><strong>{profile.displayName}</strong>{profile.username && <> · @{profile.username}</>}</p>
        {safeExternalUrl(profile.profileUrl ?? undefined) && <a className="library-link" href={safeExternalUrl(profile.profileUrl ?? undefined)} target="_blank" rel="noopener noreferrer">View GitHub profile</a>}
        <p className="connection-date">Connected {new Date(profile.connectedAt).toLocaleDateString()} · Refreshed {new Date(profile.refreshedAt).toLocaleDateString()}</p>
        {profile.provider === 'GitHub' && <p className="connection-boundary">Profile connection only. Work import and assessment are not enabled.</p>}
        <div className="agent-actions"><Button variant="outline" disabled={busy || !profile.enabled} onClick={() => void refreshProfile(profile.provider)}>Refresh profile</Button><Button variant="ghost" disabled={busy} onClick={() => choose({ kind: 'disconnect', value: profile.provider })}>Disconnect {profile.provider}</Button></div>
      </article>)}</div>
      <div className="agent-actions">{providers.filter(provider => !methods.profiles.some(profile => profile.provider === provider.provider)).map(provider => <Button key={provider.provider} variant="outline" disabled={busy} onClick={() => choose({ kind: 'link', value: provider.provider })}>Connect {provider.displayName}</Button>)}</div>
      {!providers.length && <p>External sign-in providers are not enabled on this installation.</p>}
    </section>}
    {selected && !confirm && <section className="agent-panel"><h2>{selected.kind === 'revoke' ? 'Revoke evidence agent' : selected.kind === 'link' ? `Connect ${selected.value}` : `Disconnect ${selected.value}`}</h2><p>{selected.kind === 'link' ? 'Choose the provider account you want to connect. Your existing workspace stays attached to this account.' : 'Your saved work stays in this workspace.'}</p><div className="agent-actions"><Button disabled={busy} onClick={() => void continueAction()}>{busy ? 'Checking…' : selected.kind === 'link' ? 'Continue to provider' : 'Confirm change'}</Button><Button variant="ghost" disabled={busy} onClick={() => setSelected(null)}>Cancel</Button></div></section>}
    {confirm && methods && <AccountConfirmation methods={methods} onConfirmed={perform} onCancel={() => { setSelected(null); setConfirm(false) }} />}
    <section aria-labelledby="evidence-agents-title"><h2 id="evidence-agents-title">Evidence agents</h2><div className="agent-connection-list">{connections.map(item => <article className="agent-panel" key={item.id}><header><Unplug size={22} /><h2>Local evidence agent</h2><span className="status-pill">{item.revoked ? 'Revoked' : new Date(item.expiresAt) <= new Date() ? 'Expired' : 'Connected'}</span></header><p>Connected {new Date(item.createdAt).toLocaleDateString()} · Expires {new Date(item.expiresAt).toLocaleDateString()}</p>{!item.revoked && <Button variant="ghost" disabled={busy} onClick={() => choose({ kind: 'revoke', value: item.id })}>Revoke connection</Button>}</article>)}{connections.length === 0 && methods && <p>No CLI connections yet. Start with <code>auth login --server URL</code> in the agent.</p>}</div></section>
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
