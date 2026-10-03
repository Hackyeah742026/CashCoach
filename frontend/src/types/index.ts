// DTO types — mirror docs/API.md (repo root). Keep both in sync.

/** Decimal string in PLN, e.g. "-412.30". Expenses < 0, income > 0. Never do math on it in the UI. */
export type Money = string

/** ISO date "YYYY-MM-DD" */
export type IsoDate = string

/** "YYYY-MM" */
export type MonthKey = string

export type Language = 'pl' | 'en'

/** Union of the backend's categories (docs/API.md) and older mock-only ones. */
export type Category =
  | 'restaurants'
  | 'rent'
  | 'utilities'
  | 'health'
  | 'transfers'
  | 'salary'
  | 'groceries'
  | 'food_delivery'
  | 'restaurants_cafes'
  | 'transport'
  | 'rent_bills'
  | 'subscriptions'
  | 'shopping'
  | 'health_beauty'
  | 'entertainment'
  | 'education'
  | 'travel'
  | 'transfers_people'
  | 'bnpl'
  | 'cash'
  | 'income_salary'
  | 'income_other'
  | 'savings'
  | 'other'
  | 'uncategorized'

export type CategorySource = 'rule' | 'ai' | 'user'

export type FactCheck = 'passed' | 'failed' | 'fallback'

export type Verdict = 'green' | 'yellow' | 'red'

// ---------------------------------------------------------------------------
// Settings & import

export type IncomeStatus = 'unknown' | 'confirmed' | 'none'

export type PaydayRule = 'fixed_day' | 'last_working_day'

/** The user's regular income. With status "unknown" the day is only a guess from the imported data. */
export interface Income {
  status: IncomeStatus
  /** 1–31 (31 with "last_working_day"); null without regular income */
  day: number | null
  dayRule: PaydayRule
  /** Confirmed monthly amount */
  amount: Money | null
  source: string | null
}

export interface Settings {
  language: Language
  /** Day of month income usually arrives (1–31) */
  payday: number
  safetyBuffer: Money
  currentBalance: Money
  /** True until the balance comes from a CSV balance column or the user confirms it */
  balanceIsEstimate: boolean
  income: Income
  onboarded: boolean
  /** Months that have imported data, oldest → newest */
  availableMonths: MonthKey[]
}

export type SettingsUpdate = Partial<Omit<Settings, 'onboarded' | 'availableMonths' | 'balanceIsEstimate' | 'income'>>

export type IncomeKind = 'salary' | 'stipend' | 'other'

/** A regular income the backend found in the imported transactions. */
export interface IncomeCandidate {
  source: string
  kind: IncomeKind
  day: number
  dayRule: PaydayRule
  /** Median monthly amount; amountMin–amountMax is the range seen */
  amount: Money
  amountMin: Money
  amountMax: Money
  monthsSeen: number
  confidence: 'high' | 'medium' | 'low'
  evidence: Evidence
}

export interface IncomeDetection {
  guess: IncomeCandidate | null
  others: IncomeCandidate[]
  confirmed: Income | null
}

export type IncomeAnswer =
  | { hasIncome: false }
  | { hasIncome: true; day: number; dayRule: PaydayRule; amount: Money; source?: string | null }

export interface ImportResult {
  importId: string
  bank: 'mbank' | 'pko' | 'demo'
  imported: number
  duplicatesSkipped: number
  categorizedByRules: number
  categorizedByAi: number
  needsReview: number
  from: IsoDate
  to: IsoDate
  detectedBalance: Money | null
}

// ---------------------------------------------------------------------------
// Transactions & evidence

export interface Transaction {
  id: string
  date: IsoDate
  amount: Money
  merchant: string
  description: string
  category: Category
  categorySource: CategorySource
  confidence: number
  isRecurring: boolean
}

export interface TransactionList {
  items: Transaction[]
  total: Money
  count: number
}

export interface EvidenceFigure {
  label: string
  amount: Money
}

/** Attached to every AI-produced item so the user can verify it. */
export interface Evidence {
  transactionIds: string[]
  factKeys?: string[]
  figures?: EvidenceFigure[]
  calculation?: string
}

// ---------------------------------------------------------------------------
// Insights (Home)

export interface NarrativeBullet {
  text: string
  factKeys: string[]
  transactionIds: string[]
}

export interface Narrative {
  headline: string
  bullets: NarrativeBullet[]
  aiGenerated: boolean
  factCheck: FactCheck
}

export interface CategoryTotal {
  category: Category
  amount: Money
  /** Month-over-month change, null when there's no previous month */
  changePct: number | null
}

export interface RecurringPayment {
  merchant: string
  amount: Money
  period: 'weekly' | 'monthly' | 'yearly'
  nextDate: IsoDate
  transactionIds: string[]
}

export interface PayPeriod {
  start: IsoDate
  /** Next payday */
  end: IsoDate
  daysLeft: number
}

export interface Summary {
  month: MonthKey
  income: Money
  expenses: Money
  saved: Money
  /** Deterministic: balance − bills before payday − buffer */
  safeToSpend: Money
  safeToSpendEvidence: Evidence
  payPeriod: PayPeriod
  /** Change of total expenses vs previous month, % */
  expensesChangePct: number | null
  byCategory: CategoryTotal[]
  recurring: RecurringPayment[]
  narrative: Narrative
}

export type Difficulty = 'easy' | 'medium' | 'hard'

export interface SavingSuggestion {
  id: string
  title: string
  rationale: string
  monthlyImpact: Money
  difficulty: Difficulty
  evidence: Evidence
  /** false when the rationale is a template (no AI); undefined in mocks */
  aiGenerated?: boolean
}

export interface SavingsResponse {
  suggestions: SavingSuggestion[]
  totalPotential: Money
}

// ---------------------------------------------------------------------------
// Affordability

export interface AffordabilityAssumptions {
  payday: number
  safetyBuffer: Money
  currentBalance: Money
}

export interface Installments {
  count: number
  monthlyAmount: Money
}

export interface AffordabilityRequest {
  item: string
  price: Money
  date?: IsoDate
  installments?: Installments | null
  assumptions?: AffordabilityAssumptions
}

export interface BreakdownLine {
  label: string
  amount: Money
  transactionIds?: string[]
}

export interface AiExplanation {
  text: string
  tips: string[]
  aiGenerated: boolean
  factCheck: FactCheck
}

export interface AffordabilityResponse {
  verdict: Verdict
  safeToSpend: Money
  /** Safe-to-spend left after the purchase; null when short */
  leftAfter: Money | null
  shortfall: Money | null
  breakdown: BreakdownLine[]
  assumptions: AffordabilityAssumptions
  explanation: AiExplanation
}

// ---------------------------------------------------------------------------
// Wrapped

export interface WrappedMonthInfo {
  month: MonthKey
  isNew: boolean
}

export type Weekday = 'monday' | 'tuesday' | 'wednesday' | 'thursday' | 'friday' | 'saturday' | 'sunday'

export interface Wrapped {
  month: MonthKey
  totalSpent: Money
  changePct: number | null
  transactionCount: number
  topMerchant: { name: string; count: number; amount: Money; evidence: Evidence }
  topCategory: { category: Category; amount: Money; sharePct: number }
  biggestChange: { category: Category; from: Money; to: Money; changePct: number } | null
  cheapestWeekday: Weekday
  potentialSavings: Money
  personality: { emoji: string; title: string; description: string; aiGenerated: boolean }
  /** AI captions, fact-checked */
  captions: { totalSpent: string; topMerchant: string; biggestChange: string }
}

// ---------------------------------------------------------------------------
// Goals

export type GoalStatus = 'on_track' | 'behind' | 'done'

export interface Goal {
  id: string
  name: string
  emoji: string
  target: Money
  saved: Money
  /** null for goals without a deadline (e.g. created in chat) */
  deadline: IsoDate | null
  requiredPerWeek: Money
  status: GoalStatus
  /** Present when status is "behind" */
  shortBy: Money | null
  aiTip: { text: string; aiGenerated: boolean; accepted: boolean } | null
}

export interface GoalInput {
  name: string
  emoji: string
  target: Money
  saved: Money
  deadline: IsoDate
}

export interface GoalPreviewRequest {
  target: Money
  saved: Money
  deadline: IsoDate
}

export interface GoalPreview {
  requiredPerWeek: Money
  verdict: Verdict
  plan: { savingId: string; title: string; monthlyImpact: Money }[]
  aiText: string
  /** false when aiText is a template; undefined in mocks */
  aiGenerated?: boolean
}

// ---------------------------------------------------------------------------
// Chat

export type ChatRole = 'user' | 'assistant'

export interface ChatMessage {
  id: string
  role: ChatRole
  content: string
  toolsUsed?: string[]
  evidence?: Evidence
  status?: 'streaming' | 'done' | 'error'
  /** Template answer: the model was unavailable or failed the fact check */
  fallback?: boolean
}

/** Server-sent events from POST /chat */
export type ChatEvent =
  | { type: 'delta'; text: string }
  | { type: 'tool'; name: string }
  | { type: 'evidence'; evidence: Evidence }
  | { type: 'done'; factCheck: FactCheck; conversationId?: string; fallback?: boolean }
  | { type: 'error'; message: string }
