import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { Github, ShieldCheck } from 'lucide-react'
import { request, resetCsrf, securityToken, setAuthenticatedOwner } from '../api'
import { Button } from '../components/ui/button'
import type { Account } from './AccountContext'
import { notifyAccounts, safeReturnPath, startExternal, type AuthenticationMethods, type Provider } from './external'
import './auth.css'

export function ProviderSignIn({ disabled }: { disabled: boolean }) {
  const [providers, setProviders] = useState<Provider[]>([])
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [load, setLoad] = useState(0)
  useEffect(() => {
    let active = true
    request<Provider[]>('/api/v1/auth/providers').then(value => { if (active) { setProviders(value); setError('') } }).catch(() => { if (active) setError('Other sign-in methods could not load.') })
    return () => { active = false }
  }, [load])
  const signIn = async (provider: string) => {
    setBusy(true); setError('')
    try { await startExternal(provider, 'signin', window.location.pathname + window.location.search) }
    catch (err) { setError(err instanceof Error ? err.message : 'Could not start sign-in.'); setBusy(false) }
  }
  return <>{providers.length > 0 && <div className="auth-provider-group" aria-label="Other sign-in methods">
    {providers.map(provider => <Button key={provider.provider} variant="outline" disabled={disabled || busy} onClick={() => void signIn(provider.provider)}>
      {provider.provider === 'GitHub' ? <Github size={18} aria-hidden="true" /> : <span className="auth-provider-mark" aria-hidden="true">G</span>}
      Continue with {provider.displayName}
    </Button>)}<p className="auth-divider">or use your email</p>
  </div>}{error && <p className="auth-error" role="alert">{error} <Button variant="ghost" onClick={() => setLoad(value => value + 1)}>Retry</Button></p>}</>
}

const errors: Record<string, string> = {
  email_conflict: 'An account already uses this email. Sign in to that account, then connect this provider from Connections.',
  verified_email_required: 'A verified email is required to create your account. For GitHub, verify your primary email and retry. You can also sign in to an existing account and connect the provider there.',
  external_cancelled: 'The provider connection was cancelled. You can retry when you are ready.',
  external_expired: 'This connection request expired or was already used. Start a new connection.',
  account_changed: 'Your account changed during the connection. Return to your workspace and retry.',
  provider_identity_conflict: 'This provider account is already connected to another Sensei++ account.',
  provider_identity_mismatch: 'Choose the provider account already connected to this Sensei++ account.',
  provider_already_linked: 'Another profile from this provider is already connected. Disconnect it before choosing a replacement.',
  provider_not_linked: 'This provider connection changed. Return to Connections and try again.',
  recent_authentication_required: 'Confirm your account again, then retry connecting the provider.',
  registration_disabled: 'New account creation is disabled on this installation. Ask its administrator about access.',
  provider_unavailable: 'This sign-in provider is unavailable on this installation.',
}

export function ExternalAuthenticationResult() {
  const [error, setError] = useState('')
  const started = useRef(false)
  const parameters = new URLSearchParams(window.location.search)
  const code = parameters.get('error')
  useEffect(() => {
    if (started.current) return
    started.current = true
    document.title = 'Account connection · Sensei++'
    resetCsrf()
    notifyAccounts()
    if (code) return
    const finish = async () => {
      try {
        const account = await request<Account>('/api/v1/auth/me')
        setAuthenticatedOwner(account.id)
        await securityToken()
        const path = new URLSearchParams(window.location.search).get('returnPath') ?? '/'
        window.location.replace(safeReturnPath(path) ? path : '/')
      } catch { setError('Could not verify your account. Return to the workspace and retry.') }
    }
    void finish()
  }, [code])
  return <main className="auth-shell"><section className="auth-form-panel"><p className="eyebrow">Your account</p><ShieldCheck size={28} aria-hidden="true" />
    <h1>{code || error ? 'Connection needs attention' : 'Opening your workspace…'}</h1>
    {code || error ? <p role="alert">{error || errors[code ?? ''] || 'Could not connect to the provider. Check your connection and retry.'}</p> : <p role="status">Verifying your session.</p>}
    <div className="agent-actions"><Link className="library-link" to="/">Return to workspace</Link><Link className="library-link" to="/connections">Connections</Link></div>
  </section></main>
}

export function AccountConfirmation({ methods, onConfirmed, onCancel }: { methods: AuthenticationMethods; onConfirmed: () => Promise<void>; onCancel: () => void }) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const confirm = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const password = String(new FormData(event.currentTarget).get('password'))
    setBusy(true); setError('')
    try { await request('/api/v1/auth/reauthenticate', { method: 'POST', body: JSON.stringify({ password }) }, true); await onConfirmed() }
    catch (err) { setError(err instanceof Error ? err.message : 'Could not confirm your account.') }
    finally { setBusy(false) }
  }
  const providerConfirm = async (provider: string) => {
    setBusy(true); setError('')
    try { await startExternal(provider, 'reauthenticate', '/connections') }
    catch (err) { setError(err instanceof Error ? err.message : 'Could not confirm your account.'); setBusy(false) }
  }
  return <section className="agent-panel" aria-labelledby="confirm-account-title"><h2 id="confirm-account-title">Confirm your account</h2>
    <p>Confirm before changing a connection.</p>
    {methods.hasPassword && <form className="editor-form" onSubmit={event => void confirm(event)}><label className="field"><span>Account password</span><input name="password" type="password" autoComplete="current-password" required maxLength={256} disabled={busy} /></label><Button type="submit" disabled={busy}>Confirm with password</Button></form>}
    {methods.profiles.some(profile => profile.enabled) && <><p>After returning from your provider, choose the connection action again.</p><div className="agent-actions">{methods.profiles.filter(profile => profile.enabled).map(profile => <Button key={profile.provider} variant="outline" disabled={busy} onClick={() => void providerConfirm(profile.provider)}>Confirm with {profile.provider}</Button>)}</div></>}
    {!methods.hasPassword && !methods.profiles.some(profile => profile.enabled) && <p>No sign-in provider is currently enabled. Contact the installation administrator.</p>}
    {error && <p className="auth-error" role="alert">{error}</p>}<Button variant="ghost" disabled={busy} onClick={onCancel}>Cancel</Button>
  </section>
}
