import { PiggyBank } from 'lucide-react'
import { useDismissSaving, useSavings } from '../../hooks/useInsights'
import { useLanguage, useT } from '../../i18n/context'
import { formatMoney } from '../../lib/format'
import type { MonthKey } from '../../types'
import { SkeletonCard } from '../ui/Skeleton'
import { ErrorState } from '../ui/States'
import { SavingsCard } from './SavingsCard'

export function SavingsList({ month }: { month: MonthKey }) {
  const t = useT()
  const { lang } = useLanguage()
  const { data, isLoading, isError, refetch } = useSavings(month)
  const dismiss = useDismissSaving(month)

  return (
    <section aria-labelledby="savings-title">
      <div className="section-title">
        <h2 id="savings-title">{t.home.topSavings}</h2>
        <PiggyBank size={18} className="text-subtle" aria-hidden />
      </div>
      {isLoading && <SkeletonCard height={140} />}
      {isError && <ErrorState onRetry={() => refetch()} />}
      {data && (
        <div className="stack stack--sm">
          {data.suggestions.length === 0 ? (
            <p className="card text-muted">{t.home.noSavings}</p>
          ) : (
            <>
              <p className="text-sm text-muted">{t.home.totalPotential(formatMoney(data.totalPotential, lang, { whole: true }))}</p>
              {data.suggestions.map((s) => (
                <SavingsCard key={s.id} suggestion={s} onDismiss={(id) => dismiss.mutate(id)} />
              ))}
            </>
          )}
        </div>
      )}
    </section>
  )
}
