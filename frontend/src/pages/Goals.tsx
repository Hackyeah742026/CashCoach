import { Plus, Target } from 'lucide-react'
import { useState, type CSSProperties } from 'react'
import { useSearchParams } from 'react-router'
import { todayIso } from '../api/client'
import { GoalCard } from '../components/goals/GoalCard'
import { GoalSheet, type GoalDraft } from '../components/goals/GoalSheet'
import '../components/goals/goals.css'
import { SkeletonCard } from '../components/ui/Skeleton'
import { EmptyState, ErrorState } from '../components/ui/States'
import { useGoals } from '../hooks/useGoals'
import { useSummary } from '../hooks/useInsights'
import { useSettings } from '../hooks/useSettings'
import { useT } from '../i18n/context'
import { moneyToInput } from '../lib/format'
import type { Goal } from '../types'

function inThreeMonths() {
  const [y, m, d] = todayIso().split('-').map(Number)
  return new Date(Date.UTC(y, m - 1 + 3, d)).toISOString().slice(0, 10)
}

const EMPTY_DRAFT = (): GoalDraft => ({ name: '', emoji: '🎸', target: '', saved: '', deadline: inThreeMonths() })

export function Goals() {
  const t = useT()
  const { data: goals, isLoading, isError, refetch } = useGoals()
  const { data: settings } = useSettings()
  const latest = settings?.availableMonths.at(-1)
  const { data: summary } = useSummary(latest)
  const [params, setParams] = useSearchParams()

  // Sheet state. "?new=1&name=…&target=…" (from "Make it a goal") opens it pre-filled.
  const [sheet, setSheet] = useState<{ draft: GoalDraft; editing?: Goal } | null>(() =>
    params.get('new') === '1'
      ? { draft: { ...EMPTY_DRAFT(), name: params.get('name') ?? '', target: moneyToInput(params.get('target') ?? '') } }
      : null,
  )

  function close() {
    setSheet(null)
    if (params.has('new')) setParams({}, { replace: true })
  }

  const openNew = (draft: Partial<GoalDraft> = {}) => setSheet({ draft: { ...EMPTY_DRAFT(), ...draft } })
  const openEdit = (goal: Goal) =>
    setSheet({
      editing: goal,
      draft: { name: goal.name, emoji: goal.emoji, target: moneyToInput(goal.target), saved: moneyToInput(goal.saved), deadline: goal.deadline ?? '' },
    })

  const hasEmergencyFund = goals?.some((g) => g.emoji === '🛟')

  return (
    <>
      <header className="page-header">
        <h1>{t.goals.title}</h1>
        {/* Without goals the empty state has its own "new goal" button. */}
        {!!goals?.length && (
          <button type="button" className="btn btn--primary" onClick={() => openNew()}>
            <Plus size={18} aria-hidden /> {t.goals.new}
          </button>
        )}
      </header>

      {isError && <ErrorState onRetry={() => refetch()} />}

      {goals?.length === 0 && (
        <EmptyState
          icon={Target}
          title={t.goals.emptyTitle}
          body={t.goals.emptyBody}
          action={
            <button type="button" className="btn btn--primary" onClick={() => openNew()}>
              <Plus size={18} aria-hidden /> {t.goals.new}
            </button>
          }
        />
      )}

      <div className="grid-auto" style={{ '--grid-min': '300px' } as CSSProperties}>
        {isLoading && [0, 1].map((i) => <SkeletonCard key={i} height={240} />)}
        {goals?.map((g) => (
          <GoalCard key={g.id} goal={g} onEdit={openEdit} />
        ))}
        {goals && !hasEmergencyFund && (
          <button
            type="button"
            className="goal-suggest"
            onClick={() =>
              openNew({
                name: t.goals.suggestedTitle,
                emoji: '🛟',
                target: summary ? moneyToInput(summary.expenses) : '',
              })
            }
          >
            <Plus aria-hidden />
            <span>
              <strong>{t.goals.suggestedTitle}</strong>
              <br />
              <span className="text-sm text-muted">{t.goals.suggestedBody}</span>
            </span>
          </button>
        )}
      </div>

      <GoalSheet draft={sheet?.draft ?? null} editing={sheet?.editing} onClose={close} />
    </>
  )
}
