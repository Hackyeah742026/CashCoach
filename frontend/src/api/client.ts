// The only place that talks to the backend. One typed function per endpoint in docs/API.md.
// With VITE_USE_MOCKS (default: on) every call is served by ./mocks/handlers instead.

import { readSse } from '../lib/sse'
import type {
  AffordabilityRequest,
  AffordabilityResponse,
  ChatEvent,
  ChatMessage,
  Goal,
  GoalInput,
  GoalPreview,
  GoalPreviewRequest,
  ImportResult,
  Language,
  MonthKey,
  SavingsResponse,
  Settings,
  SettingsUpdate,
  Summary,
  TransactionList,
  Wrapped,
  WrappedMonthInfo,
} from '../types'
import { MOCK_TODAY } from './mocks/data'
import * as mock from './mocks/handlers'

export const USE_MOCKS = import.meta.env.VITE_USE_MOCKS !== 'false'

/** "Today" as the backend sees it (demo data is pinned to a fixed date). */
export function todayIso(): string {
  return USE_MOCKS ? MOCK_TODAY : new Date().toISOString().slice(0, 10)
}
const BASE_URL = (import.meta.env.VITE_API_URL ?? 'http://localhost:5080/api').replace(/\/$/, '')

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const isForm = init.body instanceof FormData
  const res = await fetch(`${BASE_URL}${path}`, {
    ...init,
    headers: {
      Accept: 'application/json',
      ...(init.body && !isForm ? { 'Content-Type': 'application/json' } : {}),
      ...init.headers,
    },
  })
  if (!res.ok) {
    // Backend returns RFC 7807 Problem Details
    let message = res.statusText
    try {
      const problem = await res.json()
      message = problem.detail ?? problem.title ?? message
    } catch {
      // body wasn't JSON
    }
    throw new ApiError(res.status, message)
  }
  if (res.status === 204) return undefined as T
  return res.json() as Promise<T>
}

const json = (body: unknown) => JSON.stringify(body)

// ---------------------------------------------------------------------------
// Settings & data

export const getSettings = (): Promise<Settings> => (USE_MOCKS ? mock.getSettings() : request('/settings'))

export const updateSettings = (update: SettingsUpdate): Promise<Settings> =>
  USE_MOCKS ? mock.updateSettings(update) : request('/settings', { method: 'PUT', body: json(update) })

export const deleteAllData = (): Promise<void> =>
  USE_MOCKS ? mock.deleteAllData() : request('/data', { method: 'DELETE' })

// ---------------------------------------------------------------------------
// Transactions

export function importTransactions(file: File): Promise<ImportResult> {
  if (USE_MOCKS) return mock.importTransactions(file)
  const form = new FormData()
  form.append('file', file)
  return request('/transactions/import', { method: 'POST', body: form })
}

export const importDemoData = (): Promise<ImportResult> =>
  USE_MOCKS ? mock.importTransactions(null) : request('/transactions/import/demo', { method: 'POST' })

export const getTransactionsByIds = (ids: string[]): Promise<TransactionList> =>
  USE_MOCKS ? mock.getTransactions(ids) : request(`/transactions?ids=${encodeURIComponent(ids.join(','))}`)

// ---------------------------------------------------------------------------
// Insights

export const getSummary = (month: MonthKey): Promise<Summary> =>
  USE_MOCKS ? mock.getSummary(month) : request(`/insights/summary?month=${month}`)

export const getSavings = (month: MonthKey): Promise<SavingsResponse> =>
  USE_MOCKS ? mock.getSavings() : request(`/insights/savings?month=${month}`)

export const dismissSaving = (id: string): Promise<void> =>
  USE_MOCKS ? mock.dismissSaving(id) : request(`/insights/savings/${id}/dismiss`, { method: 'POST' })

export const checkAffordability = (req: AffordabilityRequest): Promise<AffordabilityResponse> =>
  USE_MOCKS ? mock.checkAffordability(req) : request('/affordability', { method: 'POST', body: json(req) })

// ---------------------------------------------------------------------------
// Wrapped

export const getWrappedMonths = (): Promise<WrappedMonthInfo[]> =>
  USE_MOCKS ? mock.getWrappedMonths() : request('/wrapped/months')

export const getWrapped = (month: MonthKey): Promise<Wrapped> =>
  USE_MOCKS ? mock.getWrapped(month) : request(`/wrapped?month=${month}`)

// ---------------------------------------------------------------------------
// Goals

export const getGoals = (): Promise<Goal[]> => (USE_MOCKS ? mock.getGoals() : request('/goals'))

export const createGoal = (input: GoalInput): Promise<Goal> =>
  USE_MOCKS ? mock.createGoal(input) : request('/goals', { method: 'POST', body: json(input) })

export const updateGoal = (id: string, input: Partial<GoalInput> & { tipAccepted?: boolean }): Promise<Goal> =>
  USE_MOCKS ? mock.updateGoal(id, input) : request(`/goals/${id}`, { method: 'PUT', body: json(input) })

export const deleteGoal = (id: string): Promise<void> =>
  USE_MOCKS ? mock.deleteGoal(id) : request(`/goals/${id}`, { method: 'DELETE' })

export const previewGoal = (req: GoalPreviewRequest): Promise<GoalPreview> =>
  USE_MOCKS ? mock.previewGoal(req) : request('/goals/preview', { method: 'POST', body: json(req) })

// ---------------------------------------------------------------------------
// Chat (SSE)

export async function* streamChat(
  messages: ChatMessage[],
  language: Language,
  signal?: AbortSignal,
): AsyncGenerator<ChatEvent> {
  if (USE_MOCKS) {
    yield* mock.chatStream(messages, signal)
    return
  }
  const res = await fetch(`${BASE_URL}/chat`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream' },
    body: json({ messages: messages.map(({ role, content }) => ({ role, content })), language }),
    signal,
  })
  if (!res.ok) throw new ApiError(res.status, res.statusText)
  yield* readSse(res)
}
