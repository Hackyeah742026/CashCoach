import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import * as api from '../api/client'
import type { GoalInput, GoalPreviewRequest } from '../types'
import { queryKeys } from './queryKeys'

export function useGoals() {
  return useQuery({ queryKey: queryKeys.goals, queryFn: api.getGoals })
}

function useInvalidateGoals() {
  const qc = useQueryClient()
  return () => qc.invalidateQueries({ queryKey: queryKeys.goals })
}

export function useCreateGoal() {
  const onSuccess = useInvalidateGoals()
  return useMutation({ mutationFn: (input: GoalInput) => api.createGoal(input), onSuccess })
}

export function useUpdateGoal() {
  const onSuccess = useInvalidateGoals()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: Partial<GoalInput> & { tipAccepted?: boolean } }) =>
      api.updateGoal(id, input),
    onSuccess,
  })
}

export function useDeleteGoal() {
  const onSuccess = useInvalidateGoals()
  return useMutation({ mutationFn: (id: string) => api.deleteGoal(id), onSuccess })
}

/** Live calculation while the user types in the goal form. Pass null to skip (invalid input). */
export function useGoalPreview(req: GoalPreviewRequest | null) {
  return useQuery({
    queryKey: req ? queryKeys.goalPreview(req) : ['goals', 'preview', 'none'],
    queryFn: () => api.previewGoal(req!),
    enabled: req !== null,
    placeholderData: keepPreviousData,
  })
}
