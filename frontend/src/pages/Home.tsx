import { ChevronLeft, ChevronRight, PiggyBank, TrendingDown, TrendingUp, Wallet } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useLocation, useSearchParams } from 'react-router'
import { AffordSheet } from '../components/home/AffordSheet'
import { CategoryBreakdown } from '../components/home/CategoryBreakdown'
import '../components/home/home.css'
import { InsightCard } from '../components/home/InsightCard'
import { QuickAfford, type AffordQuestion } from '../components/home/QuickAfford'
import { RecurringList } from '../components/home/RecurringList'
import { SafeToSpendHero } from '../components/home/SafeToSpendHero'
import { SavingsList } from '../components/home/SavingsList'
import { SkeletonCard } from '../components/ui/Skeleton'
import { StatTile } from '../components/ui/StatTile'
import { ErrorState } from '../components/ui/States'
import { useSavings, useSummary } from '../hooks/useInsights'
import { useSettings } from '../hooks/useSettings'
import { useLanguage, useT } from '../i18n/context'
import { formatMonth } from '../lib/format'
import type { MonthKey } from '../types'

function MonthSwitcher({ months, month, onChange }: { months: MonthKey[]; month: MonthKey; onChange: (m: MonthKey) => void }) {
  const t = useT()
  const { lang } = useLanguage()
  const i = months.indexOf(month)
  return (
    <div className="month-switcher">
      <button type="button" className="icon-btn icon-btn--sm" disabled={i <= 0} onClick={() => onChange(months[i - 1])} aria-label={t.home.prevMonth}>
        <ChevronLeft />
      </button>
      <span className="month-switcher__label" aria-live="polite">
        {formatMonth(month, lang)}
      </span>
      <button
        type="button"
        className="icon-btn icon-btn--sm"
        disabled={i === -1 || i >= months.length - 1}
        onClick={() => onChange(months[i + 1])}
        aria-label={t.home.nextMonth}
      >
        <ChevronRight />
      </button>
    </div>
  )
}

export function Home() {
  const t = useT()
  const { data: settings } = useSettings()
  const [params, setParams] = useSearchParams()
  const months = settings?.availableMonths ?? []
  const requested = params.get('month')
  const month = requested && months.includes(requested) ? requested : months[months.length - 1]

  const summary = useSummary(month)
  const savings = useSavings(month)
  const [question, setQuestion] = useState<AffordQuestion | null>(null)

  const setMonth = (m: MonthKey) => setParams({ month: m }, { replace: true })

  // Support links like "/#savings-title" (from Wrapped → "See how")
  const { hash } = useLocation()
  const loaded = Boolean(summary.data)
  useEffect(() => {
    if (loaded && hash) document.getElementById(hash.slice(1))?.scrollIntoView({ block: 'start' })
  }, [loaded, hash])

  return (
    <>
      <header className="page-header">
        <h1>{t.home.greeting}</h1>
        {month && <MonthSwitcher months={months} month={month} onChange={setMonth} />}
      </header>

      {summary.isError && <ErrorState onRetry={() => summary.refetch()} />}

      {summary.isLoading && (
        <div className="stack">
          <div className="home-top">
            <SkeletonCard height={200} />
            <div className="grid-stats">
              {[0, 1, 2, 3].map((i) => (
                <SkeletonCard key={i} height={96} />
              ))}
            </div>
          </div>
          <SkeletonCard height={180} />
        </div>
      )}

      {summary.data && (
        <div className="stack" style={{ gap: 'var(--section-gap)' }}>
          <div className="home-top">
            <SafeToSpendHero summary={summary.data} />
            <div className="grid-stats">
              <StatTile label={t.home.income} value={summary.data.income} icon={TrendingUp} tone="positive" />
              <StatTile
                label={t.home.spent}
                value={summary.data.expenses}
                icon={TrendingDown}
                change={summary.data.expensesChangePct}
                increaseIsBad
                hint={t.home.vsLastMonth}
              />
              <StatTile label={t.home.saved} value={summary.data.saved} icon={Wallet} absolute={false} />
              <StatTile
                label={t.home.savingsFound}
                value={savings.data?.totalPotential ?? '0'}
                icon={PiggyBank}
                tone="positive"
                hint={t.common.perMonth}
              />
            </div>
          </div>

          <div className="split">
            <div className="stack" style={{ gap: 'var(--grid-gap)' }}>
              <InsightCard narrative={summary.data.narrative} />
              <CategoryBreakdown summary={summary.data} />
            </div>
            <div className="stack" style={{ gap: 'var(--grid-gap)' }}>
              <QuickAfford onCheck={setQuestion} />
              <RecurringList items={summary.data.recurring} />
              {month && <SavingsList month={month} />}
            </div>
          </div>
        </div>
      )}

      <AffordSheet question={question} onClose={() => setQuestion(null)} />
    </>
  )
}
