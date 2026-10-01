import { applyTheme } from './palettes'

export type Appearance = 'coastal' | 'coastal-night'
export const appearanceStorageKey = 'sensei:appearance:v1'

export function loadAppearance(): Appearance {
  try {
    return localStorage.getItem(appearanceStorageKey) === 'coastal-night' ? 'coastal-night' : 'coastal'
  } catch { return 'coastal' }
}

export function setAppearance(appearance: Appearance) {
  applyTheme(appearance)
  try { localStorage.setItem(appearanceStorageKey, appearance) } catch { /* Applied for this visit. */ }
}
