import { type FormEvent, type ReactNode, useCallback, useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ArrowRight, BookOpen, Moon, Sun } from 'lucide-react'
import { ApiError, request, resetCsrf, setAuthenticatedOwner } from '../api'
import { Button } from '../components/ui/button'
import { loadAppearance, setAppearance } from '../theme/appearance'
import './auth.css'
import { AccountContext, useAccount, type Account } from './AccountContext'
import { notifyAccounts } from './external'
import { ProviderSignIn } from './ExternalAuthentication'

export function AuthGate({ children }: { children: ReactNode }) {
  const [account, setAccount] = useState<Account | null>(null)
  const [pending, setPending] = useState(true)
  const [error, setError] = useState('')
  const generation = useRef(0)
  const accept = useCallback((next: Account | null) => {
    generation.current++
    setAuthenticatedOwner(next?.id ?? '')
    setAccount(next)
    setPending(false)
    setError('')
  }, [])
  const refresh = useCallback(async () => {
    const version = ++generation.current
    try {
      const next = await request<Account>('/api/v1/auth/me')
      if (version === generation.current) accept(next)
    } catch (err) {
      if (version !== generation.current) return
      if (err instanceof ApiError && err.status === 401) accept(null)
      else { setError('Could not verify your session. Reconnect and retry.'); setPending(false) }
    }
  }, [accept])
  useEffect(() => {
    void refresh()
    const channel = typeof BroadcastChannel === 'undefined' ? null : new BroadcastChannel('sensei-account')
    const changed = () => { accept(null); void refresh() }
    const expired = () => { accept(null) }
    const focused = () => { void refresh() }
    if (channel) channel.onmessage = changed
    window.addEventListener('focus', focused)
    window.addEventListener('sensei-session-expired', expired)
    return () => { generation.current++; channel?.close(); window.removeEventListener('focus', focused); window.removeEventListener('sensei-session-expired', expired) }
  }, [accept, refresh])
  if (pending) return <main className="auth-shell"><p role="status">Opening your workspace…</p></main>
  if (error && !account) return <main className="auth-shell"><section className="auth-form-panel"><h1>Reconnect to your workspace</h1><p role="alert">{error}</p><Button onClick={() => void refresh()}>Retry connection</Button><Link className="library-link" to="/dev-roadmap">Build journal</Link></section></main>
  if (!account) return <SignInForm onSignedIn={next => { resetCsrf(); accept(next); notifyAccounts() }} />
  return <AccountContext.Provider key={account.id} value={account}><div>{error && <div className="auth-connection-notice" role="alert">{error}<Button variant="ghost" onClick={() => void refresh()}>Retry connection</Button></div>}{children}</div></AccountContext.Provider>
}

function SignInForm({ onSignedIn }: { onSignedIn: (account: Account) => void }) {
  const [register, setRegister] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [appearance, updateAppearance] = useState(loadAppearance)
  useEffect(() => { document.title = `${register ? 'Create account' : 'Sign in'} · Sensei++` }, [register])
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    setBusy(true); setError('')
    try {
      const body = { email: String(data.get('email')), password: String(data.get('password')), ...(register ? { displayName: String(data.get('displayName')), timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone } : {}) }
      const account = await request<Account>(`/api/v1/auth/${register ? 'register' : 'login'}`, { method: 'POST', body: JSON.stringify(body) })
      onSignedIn(account)
    } catch (err) { setError(err instanceof Error ? err.message : 'Could not sign in. Please retry.') }
    finally { setBusy(false) }
  }
  return <main className="auth-shell">
    <header className="auth-header"><Link className="brand" to="/"><span className="brand-mark">S<span>++</span></span><span className="brand-name">Sensei<span>++</span></span></Link><Button variant="ghost" aria-label="Night appearance" aria-pressed={appearance === 'coastal-night'} onClick={() => { const next = appearance === 'coastal' ? 'coastal-night' : 'coastal'; setAppearance(next); updateAppearance(next) }}>{appearance === 'coastal' ? <Moon size={18} /> : <Sun size={18} />} {appearance === 'coastal' ? 'Night' : 'Day'}</Button></header>
    <div className="auth-layout"><section className="auth-intro"><p className="eyebrow">Your learning workspace</p><h1>Keep what you learn.<br />Build on what you know.</h1><p>Practice an idea, save your reasoning, and return with a clearer understanding.</p><div className="auth-note"><BookOpen size={24} /><p>Small sessions.<br /><strong>A history that stays yours.</strong></p></div><Link className="library-link" to="/dev-roadmap">Explore the build journal <ArrowRight size={16} /></Link></section>
    <section className="auth-form-panel" aria-labelledby="auth-title"><p className="eyebrow">{register ? 'A fresh start' : 'Welcome back'}</p><h2 id="auth-title">{register ? 'Create your account' : 'Sign in to Sensei++'}</h2><p>{register ? 'Set up an account on this installation.' : 'Continue your practice and saved reflections.'}</p>
      <ProviderSignIn disabled={busy} />
      <form className="editor-form" onSubmit={submit}>
        {register && <label className="field"><span>Display name</span><input name="displayName" autoComplete="name" maxLength={200} required disabled={busy} /></label>}
        <label className="field"><span>Email</span><input name="email" type="email" autoComplete="username" maxLength={256} required disabled={busy} /></label>
        <label className="field"><span>Password</span><input name="password" type="password" aria-label="Password" aria-describedby={register ? 'password-guidance' : undefined} autoComplete={register ? 'new-password' : 'current-password'} minLength={register ? 12 : undefined} maxLength={256} required disabled={busy} />{register && <small id="password-guidance">At least 12 characters, including uppercase, lowercase, a number, and a symbol.</small>}</label>
        {error && <p className="auth-error" role="alert">{error}</p>}
        <Button className="primary-button" type="submit" disabled={busy}>{busy ? 'Connecting…' : register ? 'Create account' : 'Sign in'}<ArrowRight size={18} /></Button>
      </form>
      <div className="auth-switch"><span>{register ? 'Already have an account?' : 'New to this installation?'}</span><Button variant="ghost" disabled={busy} onClick={() => { setRegister(!register); setError('') }}>{register ? 'Sign in' : 'Create account'}</Button></div>
      <p className="auth-footnote">Your password stays with this installation. Email verification and email recovery are not available yet.</p>
    </section></div>
  </main>
}

export function SignOutPage() {
  const account = useAccount()
  const navigate = useNavigate()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const signOut = async () => {
    setBusy(true); setError('')
    try {
      await request('/api/v1/auth/logout', { method: 'POST' }, true)
      setAuthenticatedOwner(''); resetCsrf(); notifyAccounts()
      window.dispatchEvent(new Event('sensei-session-expired'))
      navigate('/', { replace: true })
    } catch (err) { setError(err instanceof Error ? err.message : 'Could not sign out. Please retry.') }
    finally { setBusy(false) }
  }
  return <main className="auth-shell"><section className="auth-form-panel"><h1>Sign out?</h1><p>Signed in as {account.email}. Your saved work will be here when you return.</p>{error && <p role="alert">{error}</p>}<Button className="primary-button" disabled={busy} onClick={() => void signOut()}>{busy ? 'Signing out…' : 'Confirm sign out'}</Button><Button variant="ghost" disabled={busy} onClick={() => navigate('/')}>Back to workspace</Button></section></main>
}
