import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { prefetchVillage } from './api'
import { App } from './App'
import { I18nProvider } from './i18n'
import './index.css'

prefetchVillage(window.location.pathname)

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <I18nProvider>
      <App />
    </I18nProvider>
  </StrictMode>,
)

const uiThemeMedia=window.matchMedia('(prefers-color-scheme: dark)')
const applyUiTheme=()=>{const chosen=localStorage.getItem('desertovia.uiTheme')??'auto';document.documentElement.dataset.uiTheme=chosen==='auto'?(uiThemeMedia.matches?'dark':'light'):chosen}
applyUiTheme()
uiThemeMedia.addEventListener('change',applyUiTheme)
window.addEventListener('storage',applyUiTheme)
