import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import * as api from '../api/client'
import type { AffordabilityRequest, MonthKey, SavingsResponse } from '../types'
import { queryKeys } from './queryKeys'

export function useSummary(month: MonthKey | undefined) {
  return useQuery({
    queryKey: queryKeys.summary(month ?? ''),
    queryFn: () => api.getSummary(month!),
    enabled: Boolean(month),
  })
}

export function useSavings(month: MonthKey | undefined) {
  return useQuery({
    queryKey: queryKeys.savings(month ?? ''),
    queryFn: () => api.getSavings(month!),
    enabled: Boolean(month),
  })
}

/** Optimistically hides the suggestion, restores it if the request fails. */
export function useDismissSaving(month: MonthKey) {
  const qc = useQueryClient()
  const key = queryKeys.savings(month)
  return useMutation({
    mutationFn: (id: string) => api.dismissSaving(id),
    onMutate: async (id) => {
      await qc.cancelQueries({ queryKey: key })
      const previous = qc.getQueryData<SavingsResponse>(key)
      if (previous) {
        qc.setQueryData<SavingsResponse>(key, {
          ...previous,
          suggestions: previous.suggestions.filter((s) => s.id !== id),
        })
      }
      return { previous }
    },
    onError: (_err, _id, ctx) => {
      if (ctx?.previous) qc.setQueryData(key, ctx.previous)
    },
    onSettled: () => qc.invalidateQueries({ queryKey: ['savings'] }),
  })
}

export function useAffordability() {
  return useMutation({ mutationFn: (req: AffordabilityRequest) => api.checkAffordability(req) })
}
