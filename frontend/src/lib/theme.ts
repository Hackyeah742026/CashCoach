export type ThemePreference = 'system' | 'light' | 'dark'

const STORAGE_KEY = 'cashcoach.theme'

export function getThemePreference(): ThemePreference {
  try {
    const v = localStorage.getItem(STORAGE_KEY)
    return v === 'light' || v === 'dark' ? v : 'system'
  } catch {
    return 'system'
  }
}

/** Applies the theme via data-theme on <html>; index.css reads it. */
export function applyTheme(pref: ThemePreference) {
  const root = document.documentElement
  if (pref === 'system') delete root.dataset.theme
  else root.dataset.theme = pref
  try {
    if (pref === 'system') localStorage.removeItem(STORAGE_KEY)
    else localStorage.setItem(STORAGE_KEY, pref)
  } catch {
    // storage unavailable (private mode) — theme still applies for this session
  }
}
