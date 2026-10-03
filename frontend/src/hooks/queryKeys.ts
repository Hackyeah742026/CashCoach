import type { GoalPreviewRequest, MonthKey } from '../types'

export const queryKeys = {
  settings: ['settings'] as const,
  summary: (month: MonthKey) => ['summary', month] as const,
  savings: (month: MonthKey) => ['savings', month] as const,
  transactions: (ids: string[]) => ['transactions', ids.join(',')] as const,
  wrappedMonths: ['wrapped', 'months'] as const,
  wrapped: (month: MonthKey) => ['wrapped', month] as const,
  goals: ['goals'] as const,
  goalPreview: (req: GoalPreviewRequest) => ['goals', 'preview', req.target, req.saved, req.deadline] as const,
}
