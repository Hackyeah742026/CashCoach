import clsx from 'clsx'
import { X } from 'lucide-react'
import { useLanguage, useT } from '../../i18n/context'
import { formatMoney } from '../../lib/format'
import type { SavingSuggestion } from '../../types'
import { AiBadge } from '../ui/AiText'
import { EvidenceButton } from '../ui/EvidenceButton'

const DIFFICULTY_BADGE = { easy: 'badge--positive', medium: 'badge--caution', hard: 'badge--negative' } as const

interface SavingsCardProps {
  suggestion: SavingSuggestion
  onDismiss: (id: string) => void
}

export function SavingsCard({ suggestion: s, onDismiss }: SavingsCardProps) {
  const t = useT()
  const { lang } = useLanguage()

  return (
    <article className="card saving-card">
      <div className="saving-card__top">
        <h3 className="saving-card__title">{s.title}</h3>
        <button type="button" className="icon-btn icon-btn--sm" onClick={() => onDismiss(s.id)} aria-label={`${t.home.dismiss}: ${s.title}`}>
          <X />
        </button>
      </div>
      <p className="text-sm text-muted">{s.rationale}</p>
      <div className="cluster">
        <span className="saving-card__impact">
          −{formatMoney(s.monthlyImpact, lang)}
          <span className="text-sm text-muted">{t.common.perMonth}</span>
        </span>
        <span className={clsx('badge', DIFFICULTY_BADGE[s.difficulty])}>{t.home.difficulty[s.difficulty]}</span>
        {s.aiGenerated !== false && <AiBadge />}
        <EvidenceButton evidence={s.evidence} subject={s.title} />
      </div>
    </article>
  )
}
