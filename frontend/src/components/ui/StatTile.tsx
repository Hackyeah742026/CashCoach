import clsx from 'clsx'
import type { LucideIcon } from 'lucide-react'
import { useLanguage } from '../../i18n/context'
import { formatPercent } from '../../lib/format'
import type { Money } from '../../types'
import { MoneyText } from './MoneyText'

interface StatTileProps {
  label: string
  value: Money
  icon?: LucideIcon
  /** Month-over-month change in % */
  change?: number | null
  /** For expenses, going up is bad */
  increaseIsBad?: boolean
  tone?: 'neutral' | 'positive'
  hint?: string
}

export function StatTile({ label, value, icon: Icon, change, increaseIsBad = false, tone = 'neutral', hint }: StatTileProps) {
  const { lang } = useLanguage()
  const hasChange = change !== undefined && change !== null && change !== 0
  const bad = hasChange && (increaseIsBad ? change > 0 : change < 0)

  return (
    <div className="card stat-tile">
      <span className="stat-tile__label">
        {Icon && <Icon aria-hidden />}
        {label}
      </span>
      <MoneyText value={value} absolute whole className="stat-tile__value" tone={tone} />
      {hasChange && (
        <span className={clsx('badge', bad ? 'badge--caution' : 'badge--positive')} style={{ alignSelf: 'flex-start' }}>
          {formatPercent(change, lang)} {hint}
        </span>
      )}
    </div>
  )
}
