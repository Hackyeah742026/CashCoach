// In-browser stand-in for the backend while VITE_USE_MOCKS is on.
// The arithmetic here plays the role of the backend's deterministic C# services —
// UI components must never do this math themselves.

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
  Money,
  MonthKey,
  SavingsResponse,
  Settings,
  SettingsUpdate,
  Summary,
  TransactionList,
  Verdict,
  Wrapped,
  WrappedMonthInfo,
} from '../../types'
import {
  CHAT_FALLBACK,
  CHAT_SCRIPTS,
  DEFAULT_SETTINGS,
  IDS,
  MOCK_LATEST_MONTH,
  MOCK_MONTHS,
  MOCK_TODAY,
  MOCK_TRANSACTIONS,
  MONTHLY_CATEGORIES,
  MONTHLY_TOTALS,
  NARRATIVES,
  WRAPPED,
  savingsFor,
} from './data'

// ---------------------------------------------------------------------------
// Persistent mock state (localStorage) so a reload keeps onboarding/goals

interface StoredGoal extends GoalInput {
  id: string
  tipAccepted: boolean
}

interface MockState {
  settings: Settings
  goals: StoredGoal[]
  dismissedSavings: string[]
}

const STORAGE_KEY = 'cashcoach.mock'

function initialState(): MockState {
  return {
    settings: { language: 'pl', ...DEFAULT_SETTINGS, onboarded: false, availableMonths: [] },
    goals: [
      { id: 'g1', name: 'Koncert w Gdańsku', emoji: '🎸', target: '1200.00', saved: '540.00', deadline: '2026-09-20', tipAccepted: false },
      { id: 'g2', name: 'Wakacje nad morzem', emoji: '🏖️', target: '1500.00', saved: '180.00', deadline: '2026-11-30', tipAccepted: false },
    ],
    dismissedSavings: [],
  }
}

function load(): MockState {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (raw) return { ...initialState(), ...JSON.parse(raw) }
  } catch {
    // ignore corrupt or unavailable storage
  }
  return initialState()
}

let state = load()

function persist() {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state))
  } catch {
    // storage unavailable — state lives in memory only
  }
}

const lang = (): Language => state.settings.language

/** Simulated network latency so loading states are visible. */
function delay<T>(value: T, ms = 450): Promise<T> {
  return new Promise((resolve) => setTimeout(() => resolve(structuredClone(value)), ms))
}

const money = (n: number): Money => n.toFixed(2)

function daysBetween(fromIso: string, toIso: string) {
  return Math.round((Date.parse(toIso) - Date.parse(fromIso)) / 86_400_000)
}

function nextPayday(payday: number): string {
  const [y, m, d] = MOCK_TODAY.split('-').map(Number)
  const date = d < payday ? new Date(Date.UTC(y, m - 1, payday)) : new Date(Date.UTC(y, m, payday))
  return date.toISOString().slice(0, 10)
}

function previousPayday(payday: number): string {
  const next = nextPayday(payday)
  const [y, m] = next.split('-').map(Number)
  return new Date(Date.UTC(y, m - 2, payday)).toISOString().slice(0, 10)
}

const BILLS_BEFORE_PAYDAY = 360.97

function safeToSpend(balance: Money, buffer: Money) {
  return Number(balance) - BILLS_BEFORE_PAYDAY - Number(buffer)
}

// ---------------------------------------------------------------------------
// Settings & import

export function getSettings(): Promise<Settings> {
  return delay(state.settings, 250)
}

export function updateSettings(update: SettingsUpdate): Promise<Settings> {
  state.settings = { ...state.settings, ...update }
  persist()
  return delay(state.settings, 300)
}

export function importTransactions(file: File | null): Promise<ImportResult> {
  state.settings = { ...state.settings, onboarded: true, availableMonths: MOCK_MONTHS }
  persist()
  return delay<ImportResult>(
    {
      importId: 'imp_demo',
      bank: file ? 'mbank' : 'demo',
      imported: 168,
      duplicatesSkipped: 0,
      categorizedByRules: 147,
      categorizedByAi: 19,
      needsReview: 2,
      from: '2026-06-01',
      to: MOCK_TODAY,
      detectedBalance: DEFAULT_SETTINGS.currentBalance,
    },
    1800,
  )
}

export function deleteAllData(): Promise<void> {
  state = initialState()
  persist()
  return delay(undefined, 300)
}

// ---------------------------------------------------------------------------
// Transactions

export function getTransactions(ids: string[]): Promise<TransactionList> {
  const items = MOCK_TRANSACTIONS.filter((t) => ids.includes(t.id)).sort((a, b) => b.date.localeCompare(a.date))
  const total = items.reduce((sum, t) => sum + Number(t.amount), 0)
  return delay({ items, total: money(total), count: items.length }, 300)
}

// ---------------------------------------------------------------------------
// Insights

export function getSummary(month: MonthKey): Promise<Summary> {
  const m = MONTHLY_CATEGORIES[month] ? month : MOCK_LATEST_MONTH
  const idx = MOCK_MONTHS.indexOf(m)
  const prev = idx > 0 ? MOCK_MONTHS[idx - 1] : null
  const cats = MONTHLY_CATEGORIES[m]
  const prevCats = prev ? MONTHLY_CATEGORIES[prev] : undefined
  const totals = MONTHLY_TOTALS[m]
  const prevTotals = prev ? MONTHLY_TOTALS[prev] : null
  const { payday, currentBalance, safetyBuffer } = state.settings
  const end = nextPayday(payday)
  const narrative = NARRATIVES[m][lang()]

  const byCategory = Object.entries(cats)
    .map(([category, amount]) => {
      const before = prevCats?.[category as keyof typeof prevCats]
      return {
        category: category as Summary['byCategory'][number]['category'],
        amount: amount as Money,
        changePct: before ? Math.round((Number(amount) / Number(before) - 1) * 100) : null,
      }
    })
    .sort((a, b) => Number(a.amount) - Number(b.amount))

  // All demo recurring payments are monthly; next charge is the same day in September
  const recurring = MOCK_TRANSACTIONS.filter((t) => t.isRecurring && Number(t.amount) < 0).map((t) => ({
    merchant: t.merchant,
    amount: t.amount,
    period: 'monthly' as const,
    nextDate: `2026-09-${t.date.slice(8)}`,
    transactionIds: [t.id],
  }))

  const summary: Summary = {
    month: m,
    income: totals.income,
    expenses: totals.expenses,
    saved: money(Number(totals.income) + Number(totals.expenses)),
    safeToSpend: money(safeToSpend(currentBalance, safetyBuffer)),
    safeToSpendEvidence: {
      transactionIds: IDS.billsBeforePayday,
      figures: [
        { label: lang() === 'pl' ? 'Saldo konta' : 'Account balance', amount: currentBalance },
        { label: lang() === 'pl' ? 'Rachunki przed wypłatą' : 'Bills before payday', amount: money(-BILLS_BEFORE_PAYDAY) },
        { label: lang() === 'pl' ? 'Poduszka bezpieczeństwa' : 'Safety buffer', amount: money(-Number(safetyBuffer)) },
      ],
      calculation: `${currentBalance} − ${BILLS_BEFORE_PAYDAY.toFixed(2)} − ${Number(safetyBuffer).toFixed(2)}`,
    },
    payPeriod: { start: previousPayday(payday), end, daysLeft: daysBetween(MOCK_TODAY, end) },
    expensesChangePct: prevTotals ? Math.round((Number(totals.expenses) / Number(prevTotals.expenses) - 1) * 100) : null,
    byCategory,
    recurring: recurring.sort((a, b) => a.nextDate.localeCompare(b.nextDate)),
    narrative: {
      headline: narrative.headline,
      bullets: narrative.bullets.map((b) => ({ text: b.text, transactionIds: b.ids, factKeys: b.keys })),
      aiGenerated: true,
      factCheck: 'passed',
    },
  }
  return delay(summary)
}

export function getSavings(): Promise<SavingsResponse> {
  const suggestions = savingsFor(lang()).filter((s) => !state.dismissedSavings.includes(s.id))
  const total = suggestions.reduce((sum, s) => sum + Number(s.monthlyImpact), 0)
  return delay({ suggestions, totalPotential: money(total) })
}

export function dismissSaving(id: string): Promise<void> {
  state.dismissedSavings = [...new Set([...state.dismissedSavings, id])]
  persist()
  return delay(undefined, 200)
}

// ---------------------------------------------------------------------------
// Affordability

export function checkAffordability(req: AffordabilityRequest): Promise<AffordabilityResponse> {
  const pl = lang() === 'pl'
  const a = req.assumptions ?? {
    payday: state.settings.payday,
    safetyBuffer: state.settings.safetyBuffer,
    currentBalance: state.settings.currentBalance,
  }
  const safe = safeToSpend(a.currentBalance, a.safetyBuffer)
  const cost = req.installments ? Number(req.installments.monthlyAmount) : Number(req.price)
  const verdict: Verdict = cost <= safe ? 'green' : cost <= safe + Number(a.safetyBuffer) ? 'yellow' : 'red'
  const shortfall = cost > safe ? money(cost - safe) : null

  const text: Record<Verdict, string> = {
    green: pl
      ? `Po zakupie zostanie Ci ${money(safe - cost).replace('.', ',')} zł do wypłaty, a poduszka bezpieczeństwa zostaje nietknięta.`
      : `After buying it you'll still have ${money(safe - cost)} zł until payday, and your safety buffer stays untouched.`,
    yellow: pl
      ? `Brakuje ${shortfall?.replace('.', ',')} zł do bezpiecznej kwoty — musiał(a)byś sięgnąć do poduszki bezpieczeństwa.`
      : `You're ${shortfall} zł short of the safe amount — you'd have to dip into your safety buffer.`,
    red: pl
      ? `Ten zakup przekracza bezpieczną kwotę i poduszkę bezpieczeństwa o ${money(cost - safe - Number(a.safetyBuffer)).replace('.', ',')} zł.`
      : `This purchase exceeds both the safe amount and your buffer by ${money(cost - safe - Number(a.safetyBuffer))} zł.`,
  }
  const tips: Record<Verdict, string[]> = {
    green: [pl ? 'Kup śmiało — i odłóż resztę na cel.' : 'Go for it — and put the rest towards a goal.'],
    yellow: pl
      ? [`Kup po wypłacie ${a.payday}. dnia miesiąca — wtedy będzie na zielono.`, 'Pomiń 2 zamówienia w Glovo (ok. 59 zł).']
      : [`Buy after payday on the ${a.payday}th — then it's green.`, 'Skip 2 Glovo orders (about 59 zł).'],
    red: pl
      ? ['Rozłóż zakup na kilka miesięcy jako cel.', 'Sprawdź sugestie oszczędności na ekranie Start.']
      : ['Spread it over a few months as a goal.', 'Check the savings suggestions on Home.'],
  }

  return delay<AffordabilityResponse>({
    verdict,
    safeToSpend: money(safe),
    leftAfter: cost <= safe ? money(safe - cost) : null,
    shortfall,
    breakdown: [
      { label: pl ? 'Saldo konta' : 'Account balance', amount: a.currentBalance },
      { label: pl ? 'Rachunki przed wypłatą (CityFit, Play, Netflix…)' : 'Bills before payday (CityFit, Play, Netflix…)', amount: money(-BILLS_BEFORE_PAYDAY), transactionIds: IDS.billsBeforePayday },
      { label: pl ? 'Poduszka bezpieczeństwa' : 'Safety buffer', amount: money(-Number(a.safetyBuffer)) },
    ],
    assumptions: a,
    explanation: { text: text[verdict], tips: tips[verdict], aiGenerated: true, factCheck: 'passed' },
  })
}

// ---------------------------------------------------------------------------
// Wrapped

export function getWrappedMonths(): Promise<WrappedMonthInfo[]> {
  const months = state.settings.availableMonths.filter((m) => WRAPPED[m]).reverse()
  return delay(months.map((month, i) => ({ month, isNew: i === 0 })), 300)
}

export function getWrapped(month: MonthKey): Promise<Wrapped> {
  const seed = WRAPPED[month] ?? WRAPPED[MOCK_LATEST_MONTH]
  const l = lang()
  const potential = savingsFor(l).reduce((sum, s) => sum + Number(s.monthlyImpact), 0)
  return delay<Wrapped>({
    month: WRAPPED[month] ? month : MOCK_LATEST_MONTH,
    totalSpent: seed.totalSpent,
    changePct: seed.changePct,
    transactionCount: seed.transactionCount,
    topMerchant: {
      name: seed.topMerchant.name,
      count: seed.topMerchant.count,
      amount: seed.topMerchant.amount,
      evidence: { transactionIds: seed.topMerchant.ids },
    },
    topCategory: seed.topCategory,
    biggestChange: seed.biggestChange,
    cheapestWeekday: seed.cheapestWeekday,
    potentialSavings: money(potential),
    personality: {
      emoji: seed.personality.emoji,
      title: seed.personality.title[l],
      description: seed.personality.description[l],
      aiGenerated: true,
    },
    captions: seed.captions[l],
  })
}

// ---------------------------------------------------------------------------
// Goals

const WEEKLY_CAPACITY = 150 // what Ola can realistically put aside per week (backend derives this)

function computeGoal(g: StoredGoal): Goal {
  const pl = lang() === 'pl'
  const remaining = Math.max(0, Number(g.target) - Number(g.saved))
  const weeks = Math.max(1, daysBetween(MOCK_TODAY, g.deadline) / 7)
  const perWeek = remaining / weeks
  const done = remaining === 0
  const behind = !done && perWeek > WEEKLY_CAPACITY
  const shortBy = behind ? money((perWeek - WEEKLY_CAPACITY) * weeks) : null
  return {
    id: g.id,
    name: g.name,
    emoji: g.emoji,
    target: g.target,
    saved: g.saved,
    deadline: g.deadline,
    requiredPerWeek: money(perWeek),
    status: done ? 'done' : behind ? 'behind' : 'on_track',
    shortBy,
    aiTip: behind
      ? {
          text: pl
            ? 'Pomiń 2 zamówienia w Glovo w miesiącu i zrezygnuj z drugiego planu chmury, żeby nadrobić.'
            : 'Skip 2 Glovo orders a month and cancel your second cloud plan to catch up.',
          aiGenerated: true,
          accepted: g.tipAccepted,
        }
      : null,
  }
}

export function getGoals(): Promise<Goal[]> {
  return delay(state.goals.map(computeGoal))
}

export function createGoal(input: GoalInput): Promise<Goal> {
  const goal: StoredGoal = { ...input, id: `g${Date.now()}`, tipAccepted: false }
  state.goals = [...state.goals, goal]
  persist()
  return delay(computeGoal(goal))
}

export function updateGoal(id: string, input: Partial<GoalInput> & { tipAccepted?: boolean }): Promise<Goal> {
  state.goals = state.goals.map((g) => (g.id === id ? { ...g, ...input } : g))
  persist()
  const goal = state.goals.find((g) => g.id === id)
  if (!goal) return Promise.reject(new Error('Goal not found'))
  return delay(computeGoal(goal))
}

export function deleteGoal(id: string): Promise<void> {
  state.goals = state.goals.filter((g) => g.id !== id)
  persist()
  return delay(undefined, 250)
}

export function previewGoal(req: GoalPreviewRequest): Promise<GoalPreview> {
  const pl = lang() === 'pl'
  const remaining = Math.max(0, Number(req.target) - Number(req.saved))
  const weeks = Math.max(1, daysBetween(MOCK_TODAY, req.deadline) / 7)
  const perWeek = remaining / weeks
  const verdict: Verdict = perWeek <= WEEKLY_CAPACITY * 0.6 ? 'green' : perWeek <= WEEKLY_CAPACITY * 1.2 ? 'yellow' : 'red'
  const plan = savingsFor(lang()).map((s) => ({ savingId: s.id, title: s.title, monthlyImpact: s.monthlyImpact }))
  const aiText: Record<Verdict, string> = {
    green: pl ? 'Spokojnie dasz radę z obecnymi nawykami. Ustaw automatyczny przelew w dniu wypłaty.' : "You'll manage comfortably with your current habits. Set up an automatic transfer on payday.",
    yellow: pl ? 'Da się, ale warto ograniczyć dostawy jedzenia i zrezygnować z drugiej chmury.' : "It's doable, but cut back on food delivery and drop the second cloud plan.",
    red: pl ? 'Przy tym terminie to ponad Twoje możliwości. Przesuń termin o kilka tygodni albo zmniejsz kwotę.' : 'That deadline is beyond what you can save. Move it back a few weeks or lower the amount.',
  }
  return delay({ requiredPerWeek: money(perWeek), verdict, plan, aiText: aiText[verdict] }, 350)
}

// ---------------------------------------------------------------------------
// Chat — yields the same events the real SSE stream sends

export async function* chatStream(messages: ChatMessage[], signal?: AbortSignal): AsyncGenerator<ChatEvent> {
  const l = lang()
  const question = [...messages].reverse().find((m) => m.role === 'user')?.content ?? ''
  const script = CHAT_SCRIPTS.find((s) => s.match.test(question))
  const wait = (ms: number) =>
    new Promise<void>((resolve, reject) => {
      const t = setTimeout(resolve, ms)
      signal?.addEventListener('abort', () => {
        clearTimeout(t)
        reject(new DOMException('Aborted', 'AbortError'))
      })
    })

  await wait(500)
  if (script) {
    yield { type: 'tool', name: script.tool }
    await wait(600)
  }

  const words = (script ? script.answer[l] : CHAT_FALLBACK[l]).split(/(\s+)/)
  for (const word of words) {
    await wait(word.trim() ? 35 : 0)
    yield { type: 'delta', text: word }
  }

  if (script) {
    yield {
      type: 'evidence',
      evidence: {
        transactionIds: script.transactionIds,
        figures: script.figures?.map((f) => ({ label: f.label[l], amount: f.amount })),
      },
    }
  }
  yield { type: 'done', factCheck: 'passed' }
}
