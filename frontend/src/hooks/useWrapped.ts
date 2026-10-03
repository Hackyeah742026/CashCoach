import { useQuery } from '@tanstack/react-query'
import * as api from '../api/client'
import type { MonthKey } from '../types'
import { queryKeys } from './queryKeys'

export function useWrappedMonths() {
  return useQuery({ queryKey: queryKeys.wrappedMonths, queryFn: api.getWrappedMonths })
}

export function useWrapped(month: MonthKey) {
  return useQuery({ queryKey: queryKeys.wrapped(month), queryFn: () => api.getWrapped(month) })
}
