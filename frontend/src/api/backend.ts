// Response shapes of the ASP.NET backend (docs/API.md): snake_case, money as numbers in złoty.
// Only adapters.ts and client.ts use these; components use the camelCase types in ../types.

import type { Category, FactCheck, GoalStatus, Verdict } from '../types'

export interface BFigure {
  key: string
  label: string
  amount: number
}

export interface BEvidence {
  transaction_ids: string[]
  figures: BFigure[]
  calculation?: string | null
}

export interface BProfile {
  user_id: string
  name: string
  persona: string
  language: 'pl' | 'en'
  has_consent: boolean
  has_data: boolean
  payday: number | null
  safety_buffer: number
  balance: number
  balance_is_estimate: boolean
  as_of: string | null
  available_months: string[]
  income: BIncome
}

export interface BIncome {
  status: 'unknown' | 'confirmed' | 'none'
  day: number | null
  day_rule: 'fixed_day' | 'last_working_day'
  amount: number | null
  source: string | null
}

export interface BIncomeCandidate {
  source: string
  kind: 'salary' | 'stipend' | 'other'
  day: number
  day_rule: 'fixed_day' | 'last_working_day'
  amount: number
  amount_min: number
  amount_max: number
  months_seen: number
  confidence: 'high' | 'medium' | 'low'
  evidence: BEvidence
}

export interface BIncomeDetection {
  guess: BIncomeCandidate | null
  others: BIncomeCandidate[]
  confirmed: BIncome | null
}

export interface BImport {
  imported: number
  skipped_duplicates: number
  categorized: { dictionary: number; fuzzy: number; llm: number; other: number }
  recurring_found: { subscriptions: number; bnpl: number; salary_day: number | null }
  period: { from: string; to: string } | null
  detected_balance: number | null
}

export interface BTransaction {
  id: string
  date: string
  amount: number
  merchant: string
  category: Category
  raw_description: string
  channel: string
  is_recurring: boolean
  is_bnpl: boolean
}

export interface BTransactionList {
  total: number
  sum: number
  items: BTransaction[]
}

export interface BHome {
  month: string
  as_of: string
  income: number
  expenses: number
  saved: number
  expenses_change_pct: number | null
  safe_to_spend: number
  safe_to_spend_evidence: BEvidence
  pay_period: { start: string; end: string; days_left: number }
  status: 'ok' | 'tight' | 'danger'
  by_category: { category: Category; amount: number; change_pct: number | null }[]
  recurring: { merchant: string; type: string; amount: number; period: 'weekly' | 'monthly'; next_date: string | null; transaction_ids: string[] }[]
  narrative: {
    headline: string
    bullets: { text: string; fact_keys: string[]; transaction_ids: string[] }[]
    ai_generated: boolean
    fact_check: FactCheck
  }
}

export interface BOpportunity {
  id: string
  type: string
  title_key: string
  title: string
  rationale: string
  monthly_saving: number
  yearly_saving: number
  difficulty: 'easy' | 'medium' | 'hard'
  evidence: BEvidence
  ai_generated: boolean
}

export interface BOpportunities {
  total_monthly_saving: number
  total_yearly_saving: number
  items: BOpportunity[]
}

export interface BPurchase {
  item: string | null
  amount: number
  verdict: Verdict
  safe_to_spend: number
  left_after: number | null
  shortfall: number | null
  breakdown: BFigure[]
  assumptions: { payday: number | null; safety_buffer: number; balance: number }
  explanation: string
  evidence: BEvidence
}

export interface BWrapped {
  month: string
  total_spent: number
  change_pct: number | null
  transaction_count: number
  top_merchant: { name: string; count: number; amount: number; evidence: BEvidence } | null
  top_category: { category: Category; amount: number; share_pct: number } | null
  biggest_change: { category: Category; from: number; to: number; change_pct: number } | null
  cheapest_weekday: string
  potential_savings: number
  personality: { key: string; emoji: string; title: string; description: string; ai_generated: boolean }
  captions: { total_spent: string; top_merchant: string; biggest_change: string }
}

export interface BGoal {
  id: string
  name: string
  emoji: string | null
  target: number
  saved: number
  deadline: string | null
  required_per_week: number | null
  status: GoalStatus
  short_by: number | null
}

export interface BGoalPreview {
  required_per_week: number
  verdict: Verdict
  plan: { opportunity_id: string; title: string; monthly_saving: number }[]
  text: string
}
