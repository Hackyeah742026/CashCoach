import { useQuery } from '@tanstack/react-query'
import * as api from '../api/client'
import { queryKeys } from './queryKeys'

/** Transactions behind a piece of evidence. */
export function useTransactionsByIds(ids: string[], enabled = true) {
  return useQuery({
    queryKey: queryKeys.transactions(ids),
    queryFn: () => api.getTransactionsByIds(ids),
    enabled: enabled && ids.length > 0,
    staleTime: 5 * 60_000,
  })
}
