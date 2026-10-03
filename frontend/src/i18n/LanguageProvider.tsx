import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import type { Language } from '../types'
import { LanguageContext } from './context'

const STORAGE_KEY = 'cashcoach.lang'

function readStoredLanguage(): Language {
  try {
    const v = localStorage.getItem(STORAGE_KEY)
    if (v === 'pl' || v === 'en') return v
  } catch {
    // storage unavailable
  }
  return navigator.language.toLowerCase().startsWith('pl') ? 'pl' : 'en'
}

/**
 * Language lives client-side so the UI switches instantly; Settings sync it to the
 * backend (which uses it for AI answers).
 */
export function LanguageProvider({ children }: { children: ReactNode }) {
  const [lang, setLangState] = useState<Language>(readStoredLanguage)

  // Keep <html lang> in sync for screen readers and hyphenation
  useEffect(() => {
    document.documentElement.lang = lang
  }, [lang])

  const setLang = useCallback((next: Language) => {
    setLangState(next)
    try {
      localStorage.setItem(STORAGE_KEY, next)
    } catch {
      // storage unavailable
    }
  }, [])

  const value = useMemo(() => ({ lang, setLang }), [lang, setLang])
  return <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>
}
