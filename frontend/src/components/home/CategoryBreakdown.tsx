import { useLanguage, useT } from '../../i18n/context'
import { categoryLabel, categoryMeta } from '../../lib/categories'
import { formatPercent } from '../../lib/format'
import type { Summary } from '../../types'
import { MoneyText } from '../ui/MoneyText'
import { SpendingChart } from './SpendingChart'

export function CategoryBreakdown({ summary }: { summary: Summary }) {
  const t = useT()
  const { lang } = useLanguage()

  return (
    <section className="card" aria-labelledby="where-title">
      <div className="section-title">
        <h2 id="where-title">{t.home.whereItWent}</h2>
      </div>
      <div className="breakdown">
        <SpendingChart categories={summary.byCategory} total={summary.expenses} lang={lang} />
        <ul role="list" className="cat-list">
          {summary.byCategory.map((cat) => (
            <li key={cat.category} className="cat-row">
              <span className="cat-row__dot" style={{ background: categoryMeta(cat.category).color }} aria-hidden />
              <span className="cat-row__name">
                {categoryMeta(cat.category).emoji} {categoryLabel(cat.category, lang)}
              </span>
              <span className="text-xs text-subtle">
                {cat.changePct !== null && cat.changePct !== 0 ? formatPercent(cat.changePct, lang) : ''}
              </span>
              <MoneyText value={cat.amount} absolute />
            </li>
          ))}
        </ul>
      </div>
    </section>
  )
}
