import { Repeat } from 'lucide-react'
import { useLanguage, useT } from '../../i18n/context'
import { formatDate } from '../../lib/format'
import type { RecurringPayment } from '../../types'
import { EvidenceButton } from '../ui/EvidenceButton'
import { MoneyText } from '../ui/MoneyText'

export function RecurringList({ items }: { items: RecurringPayment[] }) {
  const t = useT()
  const { lang } = useLanguage()

  return (
    <section className="card" aria-labelledby="recurring-title">
      <div className="section-title">
        <h2 id="recurring-title">
          {t.home.recurringTitle} ({items.length})
        </h2>
        <Repeat size={18} className="text-subtle" aria-hidden />
      </div>
      <ul role="list">
        {items.map((r) => (
          <li key={r.merchant} className="recurring-row">
            <div className="recurring-row__main">
              <div className="recurring-row__name">{r.merchant}</div>
              <div className="recurring-row__meta">
                {t.home.periods[r.period]} · {formatDate(r.nextDate, lang)}
              </div>
            </div>
            <MoneyText value={r.amount} absolute />
            <EvidenceButton evidence={{ transactionIds: r.transactionIds }} subject={r.merchant} />
          </li>
        ))}
      </ul>
    </section>
  )
}
