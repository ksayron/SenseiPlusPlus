import { request } from '../api'

export type Provider = { provider: string; displayName: string }
export type ConnectedProfile = Provider & { username: string | null; profileUrl: string | null; connectedAt: string; refreshedAt: string; enabled: boolean }
export type AuthenticationMethods = { hasPassword: boolean; profiles: ConnectedProfile[] }

export function safeReturnPath(path: string) {
  const pathname = path.split(/[?#]/, 1)[0]
  return path.length > 0 && path.length <= 2048 && path.startsWith('/') && !path.startsWith('//') &&
    !path.includes('\\') && ![...path].some(char => char.charCodeAt(0) < 32 || char.charCodeAt(0) >= 127 && char.charCodeAt(0) <= 159) && !pathname.includes('%') &&
    !pathname.split('/').some(segment => segment === '.' || segment === '..') && !/^\/(api|signin-|auth\/result|sign-out)/i.test(pathname)
}

export async function startExternal(provider: string, purpose: 'signin' | 'link' | 'refresh' | 'reauthenticate', returnPath: string) {
  if (!safeReturnPath(returnPath)) throw new Error('Return to the workspace before connecting your account.')
  const result = await request<{ redirect: string }>(`/api/v1/auth/external/${encodeURIComponent(provider)}/start`, {
    method: 'POST', body: JSON.stringify({ purpose, returnPath, timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone }),
  }, purpose !== 'signin')
  if (!/^\/api\/v1\/auth\/external\/challenge\?flow=[A-F0-9]{64}$/.test(result.redirect)) throw new Error('Could not start this connection. Retry.')
  window.location.assign(result.redirect)
}

export function notifyAccounts() {
  if (typeof BroadcastChannel === 'undefined') return
  const channel = new BroadcastChannel('sensei-account')
  channel.postMessage('changed')
  channel.close()
}
