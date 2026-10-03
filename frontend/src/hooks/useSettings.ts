import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import * as api from '../api/client'
import { useLanguage } from '../i18n/context'
import type { IncomeAnswer, Language, Settings, SettingsUpdate } from '../types'
import { queryKeys } from './queryKeys'

export function useSettings() {
  return useQuery({ queryKey: queryKeys.settings, queryFn: api.getSettings, staleTime: 60_000 })
}

/** Saves settings; everything computed from them (summary, goals, …) is refetched. */
export function useUpdateSettings() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (update: SettingsUpdate) => api.updateSettings(update),
    onSuccess: (settings) => {
      qc.setQueryData<Settings>(queryKeys.settings, settings)
      qc.invalidateQueries({ predicate: (q) => q.queryKey[0] !== 'settings' })
    },
  })
}

/** Switches UI language instantly and tells the backend, so AI texts come back in that language. */
export function useChangeLanguage() {
  const { setLang } = useLanguage()
  const update = useUpdateSettings()
  return (lang: Language) => {
    setLang(lang)
    update.mutate({ language: lang })
  }
}

/** What the backend found as the user's regular income (after an import). */
export function useIncomeDetection(enabled = true) {
  return useQuery({ queryKey: ['income', 'detection'], queryFn: api.getIncomeDetection, enabled })
}

/** Saves the user's income answer; the payday changes every forecast, so everything is refetched. */
export function useConfirmIncome() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (answer: IncomeAnswer) => api.confirmIncome(answer),
    onSuccess: (settings) => {
      qc.setQueryData<Settings>(queryKeys.settings, settings)
      qc.invalidateQueries({ predicate: (q) => q.queryKey[0] !== 'settings' })
    },
  })
}

export function useImportTransactions() {
  const qc = useQueryClient()
  return useMutation({
    /** A CSV file, or a demo persona to load synthetic data for. */
    mutationFn: (source: File | api.DemoPersona) => (source instanceof File ? api.importTransactions(source) : api.importDemoData(source)),
    // Refetch inactive queries too (e.g. settings.onboarded) before the result screen shows,
    // otherwise the route guard would see stale settings and bounce back to onboarding.
    onSuccess: () => qc.invalidateQueries({ refetchType: 'all' }),
  })
}

export function useDeleteAllData() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: api.deleteAllData,
    onSuccess: () => qc.resetQueries(),
  })
}
