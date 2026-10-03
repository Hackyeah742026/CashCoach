// Backend DTOs (snake_case, money as numbers) → frontend types (camelCase, money as decimal strings).
// Pure mapping: no arithmetic on money happens here.

import type {
  AffordabilityResponse,
  Evidence,
  Goal,
  GoalPreview,
  ImportResult,
  Income,
  IncomeCandidate,
  IncomeDetection,
  Money,
  SavingsResponse,
  Settings,
  Summary,
  Transaction,
  TransactionList,
  Weekday,
  Wrapped,
} from '../types'
import type {
  BEvidence,
  BGoal,
  BGoalPreview,
  BHome,
  BImport,
  BIncome,
  BIncomeCandidate,
  BIncomeDetection,
  BOpportunities,
  BProfile,
  BPurchase,
  BTransaction,
  BTransactionList,
  BWrapped,
} from './backend'

/** Złoty number → decimal string ("412.30"). */
export const money = (value: number): Money => value.toFixed(2)

const moneyOrNull = (value: number | null | undefined): Money | null => (value === null || value === undefined ? null : money(value))

export function toEvidence(e: BEvidence): Evidence {
  return {
    transactionIds: e.transaction_ids,
    figures: e.figures.map((f) => ({ label: f.label, amount: money(f.amount) })),
    ...(e.calculation ? { calculation: e.calculation } : {}),
  }
}

/** Default payday shown before the backend has detected one. */
const DEFAULT_PAYDAY = 10

export function toSettings(p: BProfile): Settings {
  return {
    language: p.language,
    payday: p.payday ?? DEFAULT_PAYDAY,
    safetyBuffer: money(p.safety_buffer),
    currentBalance: money(p.balance),
    balanceIsEstimate: p.balance_is_estimate,
    income: toIncome(p.income),
    onboarded: p.has_data,
    availableMonths: p.available_months,
  }
}

export function toIncome(i: BIncome): Income {
  return { status: i.status, day: i.day, dayRule: i.day_rule, amount: moneyOrNull(i.amount), source: i.source }
}

function toIncomeCandidate(c: BIncomeCandidate): IncomeCandidate {
  return {
    source: c.source,
    kind: c.kind,
    day: c.day,
    dayRule: c.day_rule,
    amount: money(c.amount),
    amountMin: money(c.amount_min),
    amountMax: money(c.amount_max),
    monthsSeen: c.months_seen,
    confidence: c.confidence,
    evidence: toEvidence(c.evidence),
  }
}

export function toIncomeDetection(d: BIncomeDetection): IncomeDetection {
  return {
    guess: d.guess ? toIncomeCandidate(d.guess) : null,
    others: d.others.map(toIncomeCandidate),
    confirmed: d.confirmed ? toIncome(d.confirmed) : null,
  }
}

export function toImportResult(r: BImport, bank: ImportResult['bank']): ImportResult {
  return {
    importId: crypto.randomUUID(),
    bank,
    imported: r.imported,
    duplicatesSkipped: r.skipped_duplicates,
    categorizedByRules: r.categorized.dictionary + r.categorized.fuzzy,
    categorizedByAi: r.categorized.llm,
    needsReview: r.categorized.other,
    from: r.period?.from ?? '',
    to: r.period?.to ?? '',
    detectedBalance: moneyOrNull(r.detected_balance),
  }
}

export function toTransaction(t: BTransaction): Transaction {
  return {
    id: t.id,
    date: t.date,
    amount: money(t.amount),
    merchant: t.merchant,
    description: t.raw_description,
    category: t.category,
    categorySource: 'rule',
    confidence: 1,
    isRecurring: t.is_recurring,
  }
}

export function toTransactionList(l: BTransactionList): TransactionList {
  return { items: l.items.map(toTransaction), total: money(l.sum), count: l.total }
}

export function toSummary(h: BHome): Summary {
  return {
    month: h.month,
    income: money(h.income),
    expenses: money(h.expenses),
    saved: money(h.saved),
    safeToSpend: money(h.safe_to_spend),
    safeToSpendEvidence: toEvidence(h.safe_to_spend_evidence),
    payPeriod: { start: h.pay_period.start, end: h.pay_period.end, daysLeft: h.pay_period.days_left },
    expensesChangePct: h.expenses_change_pct,
    byCategory: h.by_category.map((c) => ({ category: c.category, amount: money(c.amount), changePct: c.change_pct })),
    recurring: h.recurring.map((r) => ({
      merchant: r.merchant,
      amount: money(r.amount),
      period: r.period,
      nextDate: r.next_date ?? '',
      transactionIds: r.transaction_ids,
    })),
    narrative: {
      headline: h.narrative.headline,
      bullets: h.narrative.bullets.map((b) => ({ text: b.text, factKeys: b.fact_keys, transactionIds: b.transaction_ids })),
      aiGenerated: h.narrative.ai_generated,
      factCheck: h.narrative.fact_check,
    },
  }
}

export function toSavings(o: BOpportunities): SavingsResponse {
  return {
    totalPotential: money(o.total_monthly_saving),
    suggestions: o.items.map((s) => ({
      id: s.id,
      title: s.title,
      rationale: s.rationale,
      monthlyImpact: money(s.monthly_saving),
      difficulty: s.difficulty,
      evidence: toEvidence(s.evidence),
      aiGenerated: s.ai_generated,
    })),
  }
}

export function toAffordability(p: BPurchase, fallbackPayday: number): AffordabilityResponse {
  return {
    verdict: p.verdict,
    safeToSpend: money(p.safe_to_spend),
    leftAfter: moneyOrNull(p.left_after),
    shortfall: moneyOrNull(p.shortfall),
    breakdown: p.breakdown.map((f) => ({ label: f.label, amount: money(f.amount), transactionIds: p.evidence.transaction_ids })),
    assumptions: {
      payday: p.assumptions.payday ?? fallbackPayday,
      safetyBuffer: money(p.assumptions.safety_buffer),
      currentBalance: money(p.assumptions.balance),
    },
    explanation: { text: p.explanation, tips: [], aiGenerated: false, factCheck: 'passed' },
  }
}

export function toWrapped(w: BWrapped): Wrapped {
  return {
    month: w.month,
    totalSpent: money(w.total_spent),
    changePct: w.change_pct,
    transactionCount: w.transaction_count,
    topMerchant: w.top_merchant
      ? { name: w.top_merchant.name, count: w.top_merchant.count, amount: money(w.top_merchant.amount), evidence: toEvidence(w.top_merchant.evidence) }
      : { name: '—', count: 0, amount: '0.00', evidence: { transactionIds: [] } },
    topCategory: w.top_category
      ? { category: w.top_category.category, amount: money(w.top_category.amount), sharePct: w.top_category.share_pct }
      : { category: 'other', amount: '0.00', sharePct: 0 },
    biggestChange: w.biggest_change
      ? { category: w.biggest_change.category, from: money(w.biggest_change.from), to: money(w.biggest_change.to), changePct: w.biggest_change.change_pct }
      : null,
    cheapestWeekday: w.cheapest_weekday as Weekday,
    potentialSavings: money(w.potential_savings),
    personality: {
      emoji: w.personality.emoji,
      title: w.personality.title,
      description: w.personality.description,
      aiGenerated: w.personality.ai_generated,
    },
    captions: { totalSpent: w.captions.total_spent, topMerchant: w.captions.top_merchant, biggestChange: w.captions.biggest_change },
  }
}

export function toGoal(g: BGoal): Goal {
  return {
    id: g.id,
    name: g.name,
    emoji: g.emoji ?? '🎯',
    target: money(g.target),
    saved: money(g.saved),
    deadline: g.deadline,
    requiredPerWeek: money(g.required_per_week ?? 0),
    status: g.status,
    shortBy: moneyOrNull(g.short_by),
    aiTip: null,
  }
}

export function toGoalPreview(p: BGoalPreview): GoalPreview {
  return {
    requiredPerWeek: money(p.required_per_week),
    verdict: p.verdict,
    plan: p.plan.map((s) => ({ savingId: s.opportunity_id, title: s.title, monthlyImpact: money(s.monthly_saving) })),
    aiText: p.text,
    aiGenerated: false,
  }
}
