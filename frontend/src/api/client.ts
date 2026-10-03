// The only place that talks to the backend. One typed function per screen need; see docs/API.md (repo root).
// With VITE_USE_MOCKS (default: on) every call is served by ./mocks/handlers instead.
// Real responses go through ./adapters (snake_case + numbers → camelCase + decimal strings).

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
import * as adapt from './adapters'
import type {
  BGoal,
  BGoalPreview,
  BHome,
  BImport,
  BOpportunities,
  BProfile,
  BPurchase,
  BTransactionList,
  BWrapped,
} from './backend'
import { MOCK_TODAY } from './mocks/data'
import * as mock from './mocks/handlers'
import { clearSession, getAsOf, getUserId, setAsOf, setUserId } from './session'

export const USE_MOCKS = import.meta.env.VITE_USE_MOCKS !== 'false'

/** "Today" as the backend sees it: the latest transaction date (demo data is pinned to a fixed date). */
export function todayIso(): string {
  if (USE_MOCKS) return MOCK_TODAY
  return getAsOf() ?? new Date().toISOString().slice(0, 10)
}

const BASE_URL = (import.meta.env.VITE_API_URL ?? 'http://localhost:5080/api').replace(/\/$/, '')

export type DemoPersona = 'student' | 'first_job' | 'bnpl_heavy'

export class ApiError extends Error {
  readonly status: number
  readonly code: string | undefined

  constructor(status: number, message: string, code?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

/** Codes meaning the stored user id is no longer valid (e.g. the database was reset). */
const LOST_USER_CODES = new Set(['missing_user_id', 'invalid_user_id', 'user_not_found'])

function headers(init: RequestInit, accept = 'application/json'): HeadersInit {
  const userId = getUserId()
  const isForm = init.body instanceof FormData
  return {
    Accept: accept,
    ...(init.body && !isForm ? { 'Content-Type': 'application/json' } : {}),
    ...(userId ? { 'X-User-Id': userId } : {}),
    ...init.headers,
  }
}

async function toApiError(res: Response): Promise<ApiError> {
  // Backend errors: { "error": { "code": "...", "message": "..." } }
  let message = res.statusText
  let code: string | undefined
  try {
    const body = await res.json()
    message = body?.error?.message ?? message
    code = body?.error?.code
  } catch {
    // body wasn't JSON
  }
  if (code && LOST_USER_CODES.has(code)) {
    clearSession()
    window.location.assign('/onboarding')
  }
  return new ApiError(res.status, message, code)
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const res = await fetch(`${BASE_URL}${path}`, { ...init, headers: headers(init) })
  if (!res.ok) throw await toApiError(res)
  if (res.status === 204) return undefined as T
  return res.json() as Promise<T>
}

const json = (body: unknown) => JSON.stringify(body)

/** UI language as stored by LanguageProvider (used when creating the backend user). */
function uiLanguage(): Language {
  return document.documentElement.lang === 'en' ? 'en' : 'pl'
}

/** Creates the backend user on first need (onboarding), so settings and imports have someone to belong to. */
async function ensureUser(language: Language = uiLanguage()): Promise<void> {
  if (getUserId()) return
  const profile = await request<BProfile>('/users', { method: 'POST', body: json({ language }) })
  setUserId(profile.user_id)
}

function rememberProfile(profile: BProfile): Settings {
  setAsOf(profile.as_of)
  return adapt.toSettings(profile)
}

// ---------------------------------------------------------------------------
// Settings & data

/** Before onboarding there is no backend user yet: show defaults and send the user to onboarding. */
const NEW_USER_SETTINGS: Settings = {
  language: 'pl',
  payday: 10,
  safetyBuffer: '300.00',
  currentBalance: '0.00',
  onboarded: false,
  availableMonths: [],
}

export async function getSettings(): Promise<Settings> {
  if (USE_MOCKS) return mock.getSettings()
  if (!getUserId()) return { ...NEW_USER_SETTINGS, language: uiLanguage() }
  return rememberProfile(await request<BProfile>('/me'))
}

export async function updateSettings(update: SettingsUpdate): Promise<Settings> {
  if (USE_MOCKS) return mock.updateSettings(update)
  await ensureUser(update.language)
  const profile = await request<BProfile>('/me', {
    method: 'PATCH',
    body: json({
      language: update.language,
      payday: update.payday,
      safety_buffer: update.safetyBuffer === undefined ? undefined : Number(update.safetyBuffer),
      balance: update.currentBalance === undefined ? undefined : Number(update.currentBalance),
    }),
  })
  return rememberProfile(profile)
}

export async function deleteAllData(): Promise<void> {
  if (USE_MOCKS) return mock.deleteAllData()
  if (getUserId()) await request<void>('/me', { method: 'DELETE' })
  clearSession()
}

// ---------------------------------------------------------------------------
// Transactions

export async function importTransactions(file: File): Promise<ImportResult> {
  if (USE_MOCKS) return mock.importTransactions(file)
  await ensureUser()
  const form = new FormData()
  form.append('file', file)
  return adapt.toImportResult(await request<BImport>('/import', { method: 'POST', body: form }), 'mbank')
}

export async function importDemoData(persona: DemoPersona = 'bnpl_heavy'): Promise<ImportResult> {
  if (USE_MOCKS) return mock.importTransactions(null)
  await ensureUser()
  return adapt.toImportResult(await request<BImport>('/import/demo', { method: 'POST', body: json({ persona }) }), 'demo')
}

export async function getTransactionsByIds(ids: string[]): Promise<TransactionList> {
  if (USE_MOCKS) return mock.getTransactions(ids)
  const list = await request<BTransactionList>(`/transactions?limit=500&ids=${encodeURIComponent(ids.join(','))}`)
  return adapt.toTransactionList(list)
}

// ---------------------------------------------------------------------------
// Insights

export async function getSummary(month: MonthKey): Promise<Summary> {
  if (USE_MOCKS) return mock.getSummary(month)
  return adapt.toSummary(await request<BHome>(`/home?month=${month}`))
}

export async function getSavings(month: MonthKey): Promise<SavingsResponse> {
  if (USE_MOCKS) return mock.getSavings()
  void month // opportunities always use the latest 90 days
  return adapt.toSavings(await request<BOpportunities>('/opportunities'))
}

export const dismissSaving = (id: string): Promise<void> =>
  USE_MOCKS ? mock.dismissSaving(id) : request(`/opportunities/${encodeURIComponent(id)}/dismiss`, { method: 'POST' })

export async function checkAffordability(req: AffordabilityRequest): Promise<AffordabilityResponse> {
  if (USE_MOCKS) return mock.checkAffordability(req)
  // With instalments only the first one is paid before payday.
  const amount = req.installments ? req.installments.monthlyAmount : req.price
  const a = req.assumptions
  const result = await request<BPurchase>('/simulate/purchase', {
    method: 'POST',
    body: json({
      amount: Number(amount),
      date: req.date,
      item: req.item,
      assumptions: a ? { payday: a.payday, safety_buffer: Number(a.safetyBuffer), balance: Number(a.currentBalance) } : undefined,
    }),
  })
  return adapt.toAffordability(result, a?.payday ?? 10)
}

// ---------------------------------------------------------------------------
// Wrapped

export async function getWrappedMonths(): Promise<WrappedMonthInfo[]> {
  if (USE_MOCKS) return mock.getWrappedMonths()
  const months = await request<{ month: string; is_new: boolean }[]>('/wrapped/months')
  return months.map((m) => ({ month: m.month, isNew: m.is_new }))
}

export async function getWrapped(month: MonthKey): Promise<Wrapped> {
  if (USE_MOCKS) return mock.getWrapped(month)
  return adapt.toWrapped(await request<BWrapped>(`/wrapped?month=${month}`))
}

// ---------------------------------------------------------------------------
// Goals

const goalBody = (input: Partial<GoalInput>) =>
  json({
    name: input.name,
    emoji: input.emoji,
    target: input.target === undefined ? undefined : Number(input.target),
    saved: input.saved === undefined ? undefined : Number(input.saved),
    deadline: input.deadline,
  })

export async function getGoals(): Promise<Goal[]> {
  if (USE_MOCKS) return mock.getGoals()
  return (await request<BGoal[]>('/goals')).map(adapt.toGoal)
}

export async function createGoal(input: GoalInput): Promise<Goal> {
  if (USE_MOCKS) return mock.createGoal(input)
  return adapt.toGoal(await request<BGoal>('/goals', { method: 'POST', body: goalBody(input) }))
}

export async function updateGoal(id: string, input: Partial<GoalInput> & { tipAccepted?: boolean }): Promise<Goal> {
  if (USE_MOCKS) return mock.updateGoal(id, input)
  return adapt.toGoal(await request<BGoal>(`/goals/${id}`, { method: 'PUT', body: goalBody(input) }))
}

export const deleteGoal = (id: string): Promise<void> =>
  USE_MOCKS ? mock.deleteGoal(id) : request(`/goals/${id}`, { method: 'DELETE' })

export async function previewGoal(req: GoalPreviewRequest): Promise<GoalPreview> {
  if (USE_MOCKS) return mock.previewGoal(req)
  const preview = await request<BGoalPreview>('/goals/preview', {
    method: 'POST',
    body: json({ target: Number(req.target), saved: Number(req.saved), deadline: req.deadline }),
  })
  return adapt.toGoalPreview(preview)
}

// ---------------------------------------------------------------------------
// Chat (SSE). Gemini runs on the backend; answers are fact-checked before they stream.

export async function* streamChat(
  messages: ChatMessage[],
  language: Language,
  signal?: AbortSignal,
  conversationId?: string | null,
): AsyncGenerator<ChatEvent> {
  if (USE_MOCKS) {
    yield* mock.chatStream(messages, signal)
    return
  }
  const init: RequestInit = {
    method: 'POST',
    body: json({
      conversation_id: conversationId ?? undefined,
      messages: messages.map(({ role, content }) => ({ role, content })),
      language,
    }),
    signal,
  }
  const res = await fetch(`${BASE_URL}/chat`, { ...init, headers: headers(init, 'text/event-stream') })
  if (!res.ok) throw await toApiError(res)
  yield* readSse(res)
}
