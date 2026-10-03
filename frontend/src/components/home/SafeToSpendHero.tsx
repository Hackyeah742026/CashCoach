import { useSettings } from '../../hooks/useSettings'
import { useLanguage, useT } from '../../i18n/context'
import { formatDate } from '../../lib/format'
import type { Summary } from '../../types'
import { EvidenceButton } from '../ui/EvidenceButton'
import { MoneyText } from '../ui/MoneyText'
import { ProgressBar } from '../ui/Progress'

const DAY = 86_400_000

/** Big "safe to spend until payday" number — deterministic, never AI. */
export function SafeToSpendHero({ summary }: { summary: Summary }) {
  const t = useT()
  const { lang } = useLanguage()
  const { start, end, daysLeft } = summary.payPeriod
  const periodDays = Math.max(1, Math.round((Date.parse(end) - Date.parse(start)) / DAY))
  const elapsed = 1 - daysLeft / periodDays
  const label = t.home.safeToSpend(formatDate(end, lang))
  // Without a regular income the period ends with the month, not on a payday.
  const noIncome = useSettings().data?.income.status === 'none'
  const daysText = noIncome ? t.home.daysLeftMonth(daysLeft) : t.home.daysLeft(daysLeft)

  return (
    <section className="hero-card" aria-label={label}>
      <span className="hero-card__label">
        {label}
        <EvidenceButton evidence={summary.safeToSpendEvidence} subject={label} />
      </span>
      <MoneyText value={summary.safeToSpend} variant="hero" countUp />
      <div className="hero-card__footer">
        <ProgressBar value={elapsed} label={daysText} color="#34c58f" />
        <span>{daysText}</span>
      </div>
    </section>
  )
}
