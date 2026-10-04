import { lazy, StrictMode, Suspense } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import { AgentConsentPage, ConnectionsPage } from './agent/AgentPages'
import { AuthGate, SignOutPage } from './auth/AuthGate'
import '@fontsource/plus-jakarta-sans/latin-400.css'
import '@fontsource/plus-jakarta-sans/latin-500.css'
import '@fontsource/plus-jakarta-sans/latin-600.css'
import '@fontsource/plus-jakarta-sans/latin-700.css'
import '@fontsource/dm-mono/latin-400.css'
import './theme/foundation.css'
import './styles.css'
import { applyTheme } from './theme/palettes'
import { loadAppearance } from './theme/appearance'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'

const DevRoadmapPage = lazy(() => import('./roadmap/DevRoadmapPage'))
const router = createBrowserRouter([
  { path: '/dev-roadmap', element: <Suspense fallback={<p role="status">Opening the build journal…</p>}><DevRoadmapPage /></Suspense> },
  { path: '/agent/authorize', element: <AuthGate><AgentConsentPage /></AuthGate> },
  { path: '/connections', element: <AuthGate><ConnectionsPage /></AuthGate> },
  { path: '/sign-out', element: <AuthGate><SignOutPage /></AuthGate> },
  { path: '*', element: <AuthGate><App /></AuthGate> },
])
applyTheme(loadAppearance())

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <RouterProvider router={router} />
  </StrictMode>,
)
