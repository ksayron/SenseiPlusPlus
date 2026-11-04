import { describe, expect, it } from 'vitest'
import { safeReturnPath } from './external'

describe('external authentication return paths', () => {
  it.each(['/', '/connections', '/agent/authorize?request=example-ticket', '/learn?search=%D1%82%D0%B5%D1%81%D1%82', '/learn?search=100%25'])('preserves local navigation: %s', path => {
    expect(safeReturnPath(path)).toBe(true)
  })

  it.each(['https://evil.example', '//evil.example', '/\\evil.example', '/%2fevil.example', '/learn/../sign-out', '/learn/../api/v1/auth/logout', '/./signin-google', '/auth/result', '/sign-out', '/learn\u0085'])('rejects unsafe navigation: %s', path => {
    expect(safeReturnPath(path)).toBe(false)
  })
})
