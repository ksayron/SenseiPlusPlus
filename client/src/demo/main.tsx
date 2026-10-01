import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { Demo } from './Demo'
import '@fontsource/plus-jakarta-sans/latin-400.css'
import '@fontsource/plus-jakarta-sans/latin-500.css'
import '@fontsource/plus-jakarta-sans/latin-600.css'
import '@fontsource/plus-jakarta-sans/latin-700.css'
import '@fontsource/dm-mono/latin-400.css'
import './theme.css'
import './demo.css'
import { applyTheme, loadTheme } from './themes'

applyTheme(loadTheme())

createRoot(document.getElementById('root')!).render(<StrictMode><Demo /></StrictMode>)
