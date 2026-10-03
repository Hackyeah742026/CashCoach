// Global styles first: tokens/reset → shared components → (pages import their own CSS via App)
import './index.css'
import './components/ui/ui.css'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'
import App from './App.tsx'
import { EvidenceProvider } from './components/ui/EvidenceProvider'
import { LanguageProvider } from './i18n/LanguageProvider'
import { applyTheme, getThemePreference } from './lib/theme'

applyTheme(getThemePreference())

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 30_000, retry: 1, refetchOnWindowFocus: false },
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <LanguageProvider>
        <BrowserRouter>
          <EvidenceProvider>
            <App />
          </EvidenceProvider>
        </BrowserRouter>
      </LanguageProvider>
    </QueryClientProvider>
  </StrictMode>,
)
