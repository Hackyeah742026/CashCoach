import { createContext, useContext } from 'react'
import type { Language } from '../types'
import { dictionaries, type Dictionary } from './strings'

export interface LanguageContextValue {
  lang: Language
  setLang: (lang: Language) => void
}

export const LanguageContext = createContext<LanguageContextValue | null>(null)

export function useLanguage(): LanguageContextValue {
  const ctx = useContext(LanguageContext)
  if (!ctx) throw new Error('useLanguage must be used inside <LanguageProvider>')
  return ctx
}

/** Current dictionary: `const t = useT(); t.home.income` */
export function useT(): Dictionary {
  return dictionaries[useLanguage().lang]
}
