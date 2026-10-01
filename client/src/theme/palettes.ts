const coastal = {
  background: '#eef3fa', foreground: '#253b59', card: '#ffffff', primary: '#315fc4',
  'primary-foreground': '#ffffff', secondary: '#e3eaf5', 'secondary-foreground': '#355072',
  muted: '#f0f4fa', 'muted-foreground': '#52657f', accent: '#dce7f6', destructive: '#a73229',
  border: '#d9e2ef', input: '#9cacbf', ring: '#416da7',
  sidebar: '#253b59', 'sidebar-foreground': '#f1f5fc', 'sidebar-muted': '#bccde1', 'sidebar-accent': '#97b8ff',
  guidance: '#fff3de', 'guidance-foreground': '#725a33',
  'attention-surface': '#fbefd8', 'attention-foreground': '#71521d',
  'success-surface': '#e1eaf8', 'success-foreground': '#345682',
  'error-surface': '#ffe7e0', 'error-foreground': '#a73229',
}

const coastalNight = {
  ...coastal, background: '#121c2d', foreground: '#e3ebf8', card: '#1c2940', primary: '#97b8ff',
  'primary-foreground': '#162846', secondary: '#283954', 'secondary-foreground': '#d6e3f6',
  muted: '#23324a', 'muted-foreground': '#b0c0d9', accent: '#304565', destructive: '#ffb2a7',
  border: '#3c506d', input: '#7087a8', ring: '#8bb5f0',
  sidebar: '#0d1524', 'sidebar-foreground': '#edf3ff', 'sidebar-muted': '#b7c8e3', 'sidebar-accent': '#97b8ff',
  guidance: '#383229', 'guidance-foreground': '#ebd5b0',
  'attention-surface': '#423929', 'attention-foreground': '#f0d39b',
  'success-surface': '#263b58', 'success-foreground': '#bfd5f5',
  'error-surface': '#482e2d', 'error-foreground': '#ffc4b9',
}

export const themes = [
  { id: 'coastal', name: 'Coastal', description: 'Airy blue, midnight ink, and cobalt.', dark: false, primaryHover: '#274da4', tokens: coastal },
  { id: 'daybreak', name: 'Daybreak', description: 'Warm ivory, navy, and soft coral.', dark: false, tokens: {
    ...coastal, background: '#f7f3ee', foreground: '#303c51', primary: '#ad493a',
    secondary: '#eee7df', 'secondary-foreground': '#4b5567', muted: '#f8f5f1', 'muted-foreground': '#666477',
    accent: '#f1ded6', border: '#e2dbd3', input: '#aaa0a0', ring: '#925446',
    sidebar: '#303c51', 'sidebar-muted': '#d0cccf', 'sidebar-accent': '#f1b4a1',
    guidance: '#fff0df', 'guidance-foreground': '#7a5c3e',
  } },
  { id: 'porcelain', name: 'Porcelain', description: 'Cool white, slate, and clear blue.', dark: false, tokens: {
    ...coastal, background: '#f3f5f8', foreground: '#303c50', primary: '#355faf',
    secondary: '#e7ecf5', 'secondary-foreground': '#405375', muted: '#f6f8fc', 'muted-foreground': '#57657c',
    accent: '#e0e9fa', border: '#dce2eb', input: '#9eaec6', ring: '#416fbf',
    sidebar: '#303c50', 'sidebar-muted': '#c2cfe3', 'sidebar-accent': '#a6c3fa',
    guidance: '#edf1fa', 'guidance-foreground': '#536486',
  } },
  { id: 'coastal-night', name: 'Coastal Night', description: 'Midnight blue with soft cobalt.', dark: true, primaryHover: '#aac5ff', tokens: coastalNight },
  { id: 'ember-night', name: 'Ember Night', description: 'Warm charcoal with peach and coral.', dark: true, tokens: {
    ...coastalNight, background: '#242128', foreground: '#f2e8e5', card: '#302d36', primary: '#f0ac96',
    'primary-foreground': '#35251f', secondary: '#403b46', 'secondary-foreground': '#eee1dd',
    muted: '#39353f', 'muted-foreground': '#c6b9be', accent: '#514149', border: '#5b505b', input: '#998390', ring: '#e9b09e',
    sidebar: '#1b1920', 'sidebar-foreground': '#f9efed', 'sidebar-muted': '#cdbfc5', 'sidebar-accent': '#f0ac96',
    guidance: '#443830', 'guidance-foreground': '#edd0b4',
  } },
  { id: 'slate-night', name: 'Slate Night', description: 'Graphite, silver, and soft blue.', dark: true, tokens: {
    ...coastalNight, background: '#1a202a', foreground: '#e6edf7', card: '#252e3d', primary: '#a6c5fb',
    'primary-foreground': '#203047', secondary: '#333f53', 'secondary-foreground': '#dce7f8',
    muted: '#2d384a', 'muted-foreground': '#b8c5dc', accent: '#3c506e', border: '#485973', input: '#8295b2', ring: '#a6c5fb',
    sidebar: '#131923', 'sidebar-foreground': '#edf3ff', 'sidebar-muted': '#bac8de', 'sidebar-accent': '#a6c5fb',
    guidance: '#303e56', 'guidance-foreground': '#c3d5f3',
  } },
] as const

export type ThemeId = typeof themes[number]['id']
const storageKey = 'sensei-ui-demo:color-theme:v1'

export function resolveTheme(stored: string | null): ThemeId {
  if (stored === 'dusk') return 'coastal-night'
  return themes.find((theme) => theme.id === stored)?.id ?? 'coastal'
}

export function loadTheme(): ThemeId {
  try {
    // Preserve the old dark preference; retired light palettes use the new default.
    return resolveTheme(localStorage.getItem(storageKey))
  } catch { return 'coastal' }
}

export function applyTheme(id: ThemeId) {
  const theme = themes.find((entry) => entry.id === id)!
  const root = document.documentElement
  root.dataset.theme = id
  root.classList.toggle('dark', theme.dark)
  root.style.colorScheme = theme.dark ? 'dark' : 'light'
  for (const [role, color] of Object.entries(theme.tokens)) root.style.setProperty(`--${role}`, color)
  // Opaque Coastal hover colors preserve label contrast; other palettes keep the registry treatment.
  root.style.setProperty('--primary-hover', 'primaryHover' in theme ? theme.primaryHover : `color-mix(in oklab, ${theme.tokens.primary} 80%, transparent)`)
  for (const role of ['card-foreground', 'popover-foreground', 'accent-foreground']) root.style.setProperty(`--${role}`, theme.tokens.foreground)
  root.style.setProperty('--popover', theme.tokens.card)
  root.style.setProperty('--control-border', theme.dark ? theme.tokens.input : theme.tokens.ring)
  root.style.setProperty('--code-surface', theme.tokens.sidebar)
  root.style.setProperty('--code-foreground', theme.tokens['sidebar-foreground'])
  root.style.setProperty('--overlay', `color-mix(in srgb, ${theme.tokens.sidebar} 65%, transparent)`)
  root.style.setProperty('--shadow-overlay', `0 24px 80px color-mix(in srgb, ${theme.tokens.sidebar} 30%, transparent)`)
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content', theme.tokens.sidebar)
}

export function persistTheme(id: ThemeId) {
  try { localStorage.setItem(storageKey, id) } catch { /* Theme still works for this visit. */ }
}
