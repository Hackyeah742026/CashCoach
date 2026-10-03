import clsx from 'clsx'
import { Pencil } from 'lucide-react'
import { useUpdateGoal } from '../../hooks/useGoals'
import { useLanguage, useT } from '../../i18n/context'
import { formatDate, formatMoney } from '../../lib/format'
import type { Goal } from '../../types'
import { AiText } from '../ui/AiText'
import { MoneyText } from '../ui/MoneyText'
import { ProgressRing } from '../ui/Progress'

const STATUS_BADGE = { on_track: 'badge--positive', behind: 'badge--caution', done: 'badge--positive' } as const
const STATUS_ICON = { on_track: '🟢', behind: '🟡', done: '🎉' } as const

export function GoalCard({ goal, onEdit }: { goal: Goal; onEdit: (goal: Goal) => void }) {
  const t = useT()
  const { lang } = useLanguage()
  const update = useUpdateGoal()
  // Ring geometry only (display), the status itself comes from the backend
  const progress = Number(goal.saved) / Math.max(1, Number(goal.target))
  const statusText =
    goal.status === 'behind'
      ? t.goals.status.behind(formatMoney(goal.shortBy ?? '0', lang, { whole: true }))
      : t.goals.status[goal.status]

  return (
    <article className="card goal-card">
      <div className="goal-card__head">
        <span className="goal-card__emoji" aria-hidden>
          {goal.emoji}
        </span>
        <h2 className="goal-card__name">{goal.name}</h2>
        <button type="button" className="icon-btn icon-btn--sm" onClick={() => onEdit(goal)} aria-label={`${t.common.edit}: ${goal.name}`}>
          <Pencil />
        </button>
      </div>

      <div className="goal-card__body">
        <ProgressRing value={progress} label={t.goals.progressLabel(Math.round(progress * 100))} size={72} />
        <div className="goal-card__numbers">
          <span className="goal-card__amount">
            <MoneyText value={goal.saved} whole /> <span className="text-muted">/</span> <MoneyText value={goal.target} whole />
          </span>
          {goal.deadline && <span className="text-sm text-muted">{t.goals.by(formatDate(goal.deadline, lang, 'long'))}</span>}
          {goal.status !== 'done' && (
            <span className="text-sm text-muted">{t.goals.perWeek(formatMoney(goal.requiredPerWeek, lang, { whole: true }))}</span>
          )}
        </div>
      </div>

      <span className={clsx('badge', STATUS_BADGE[goal.status])} style={{ alignSelf: 'flex-start' }}>
        <span aria-hidden>{STATUS_ICON[goal.status]}</span> {statusText}
      </span>

      {goal.aiTip && (
        <AiText aiGenerated={goal.aiTip.aiGenerated}>
          <p>{goal.aiTip.text}</p>
          <button
            type="button"
            className="btn btn--secondary"
            style={{ alignSelf: 'flex-start', minHeight: 40 }}
            disabled={goal.aiTip.accepted || update.isPending}
            onClick={() => update.mutate({ id: goal.id, input: { tipAccepted: true } })}
          >
            {goal.aiTip.accepted ? t.goals.applied : t.goals.apply}
          </button>
        </AiText>
      )}
    </article>
  )
}
