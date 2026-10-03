import { useT } from '../../i18n/context'
import { CATEGORY_META, categoryLabel } from '../../lib/categories'
import type { CategoryTotal, Language, Money } from '../../types'
import { MoneyText } from '../ui/MoneyText'

interface SpendingChartProps {
  categories: CategoryTotal[]
  total: Money
  lang: Language
  size?: number
}

/**
 * Donut chart of spending by category. Plain SVG so colors come straight from
 * the CSS tokens (works in light and dark mode). Geometry only — no money math shown.
 */
export function SpendingChart({ categories, total, lang, size = 180 }: SpendingChartProps) {
  const t = useT()
  const stroke = 22
  const r = (size - stroke) / 2
  const c = 2 * Math.PI * r
  const sum = categories.reduce((s, cat) => s + Math.abs(Number(cat.amount)), 0) || 1
  const gap = categories.length > 1 ? 2 : 0

  const segments = categories.reduce<{ key: string; len: number; offset: number; color: string }[]>((acc, cat) => {
    const offset = acc.length ? acc[acc.length - 1].offset + acc[acc.length - 1].len + gap : 0
    const len = Math.max(0, (Math.abs(Number(cat.amount)) / sum) * c - gap)
    acc.push({ key: cat.category, len, offset, color: CATEGORY_META[cat.category].color })
    return acc
  }, [])

  const description = categories.map((cat) => `${categoryLabel(cat.category, lang)}`).join(', ')

  return (
    <div className="donut" style={{ width: size, height: size }}>
      <svg width={size} height={size} role="img" aria-label={`${t.home.whereItWent}: ${description}`}>
        <circle cx={size / 2} cy={size / 2} r={r} fill="none" strokeWidth={stroke} style={{ stroke: 'var(--color-surface-sunken)' }} />
        {segments.map((s) => (
          <circle
            key={s.key}
            className="donut__segment"
            cx={size / 2}
            cy={size / 2}
            r={r}
            fill="none"
            strokeWidth={stroke}
            strokeDasharray={`${s.len} ${c - s.len}`}
            strokeDashoffset={-s.offset}
            style={{ stroke: s.color }}
          />
        ))}
      </svg>
      <div className="donut__center">
        <span className="text-xs text-muted">{t.home.spent}</span>
        <MoneyText value={total} absolute whole />
      </div>
    </div>
  )
}
