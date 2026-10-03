import { Trash } from 'lucide-react'
import { useMemo, useState, type SyntheticEvent } from 'react'
import { todayIso } from '../../api/client'
import { useDebouncedValue } from '../../hooks/useDebouncedValue'
import { useCreateGoal, useDeleteGoal, useGoalPreview, useUpdateGoal } from '../../hooks/useGoals'
import { useLanguage, useT } from '../../i18n/context'
import { formatMoney, inputToMoney } from '../../lib/format'
import type { Goal, GoalInput, GoalPreviewRequest } from '../../types'
import { AiText } from '../ui/AiText'
import { Field } from '../ui/Field'
import { Sheet } from '../ui/Sheet'
import { VerdictCard } from '../ui/VerdictCard'

const EMOJIS = ['🎸', '🏖️', '💻', '🎓', '🚗', '📱', '✈️', '🛟', '🎁', '🏠']

export interface GoalDraft {
  name: string
  emoji: string
  target: string
  saved: string
  deadline: string
}

interface GoalSheetProps {
  /** null = closed */
  draft: GoalDraft | null
  /** Goal being edited; undefined when creating */
  editing?: Goal
  onClose: () => void
}

export function GoalSheet({ draft, editing, onClose }: GoalSheetProps) {
  const t = useT()
  return (
    <Sheet open={draft !== null} onClose={onClose} title={editing ? t.goals.editTitle : t.goals.newTitle}>
      {draft && <GoalForm key={editing?.id ?? 'new'} initial={draft} editing={editing} onDone={onClose} />}
    </Sheet>
  )
}

function GoalForm({ initial, editing, onDone }: { initial: GoalDraft; editing?: Goal; onDone: () => void }) {
  const t = useT()
  const { lang } = useLanguage()
  const [form, setForm] = useState(initial)
  const [showErrors, setShowErrors] = useState(false)
  const create = useCreateGoal()
  const update = useUpdateGoal()
  const remove = useDeleteGoal()

  const set = <K extends keyof GoalDraft>(key: K, value: GoalDraft[K]) => setForm((f) => ({ ...f, [key]: value }))

  // Valid input → payload; null while something is missing
  const input = useMemo<GoalInput | null>(() => {
    const target = inputToMoney(form.target)
    const saved = inputToMoney(form.saved || '0')
    if (!form.name.trim() || !target || Number(target) <= 0 || !saved || !form.deadline || form.deadline <= todayIso()) return null
    return { name: form.name.trim(), emoji: form.emoji, target, saved, deadline: form.deadline }
  }, [form])

  const previewReq = useDebouncedValue<GoalPreviewRequest | null>(
    input ? { target: input.target, saved: input.saved, deadline: input.deadline } : null,
    350,
  )
  const preview = useGoalPreview(previewReq)

  const saving = create.isPending || update.isPending
  const failed = create.isError || update.isError || remove.isError

  function submit(e: SyntheticEvent) {
    e.preventDefault()
    if (!input) {
      setShowErrors(true)
      return
    }
    if (editing) update.mutate({ id: editing.id, input }, { onSuccess: onDone })
    else create.mutate(input, { onSuccess: onDone })
  }

  function onDelete() {
    if (editing && window.confirm(t.goals.deleteConfirm)) remove.mutate(editing.id, { onSuccess: onDone })
  }

  return (
    <form className="stack" onSubmit={submit} noValidate>
      <Field
        label={t.goals.name}
        placeholder={t.goals.namePlaceholder}
        value={form.name}
        onChange={(e) => set('name', e.target.value)}
        autoComplete="off"
      />

      <div className="field">
        <span id="emoji-label" style={{ fontSize: 'var(--text-sm)', fontWeight: 'var(--weight-medium)' }}>
          {t.goals.emoji}
        </span>
        <div className="emoji-picker" role="group" aria-labelledby="emoji-label">
          {EMOJIS.map((e) => (
            <button key={e} type="button" aria-pressed={form.emoji === e} onClick={() => set('emoji', e)}>
              {e}
            </button>
          ))}
        </div>
      </div>

      <div className="field-row">
        <Field label={t.goals.target} inputMode="decimal" placeholder="1200" value={form.target} onChange={(e) => set('target', e.target.value)} />
        <Field label={t.goals.savedNow} inputMode="decimal" placeholder="0" value={form.saved} onChange={(e) => set('saved', e.target.value)} />
      </div>
      <Field label={t.goals.deadline} type="date" min={todayIso()} value={form.deadline} onChange={(e) => set('deadline', e.target.value)} />

      {showErrors && !input && (
        <p className="field__error" role="alert">
          {t.goals.validation}
        </p>
      )}

      {preview.data && input && (
        <div className="goal-preview" aria-live="polite" style={{ opacity: preview.isFetching ? 0.6 : 1 }}>
          <span className="goal-preview__needs">{t.goals.needs(formatMoney(preview.data.requiredPerWeek, lang, { whole: true }))}</span>
          <VerdictCard verdict={preview.data.verdict} title={t.goals.verdict[preview.data.verdict]} />
          <AiText aiGenerated={preview.data.aiGenerated ?? true}>
            <p className="text-sm">{preview.data.aiText}</p>
            {preview.data.verdict !== 'green' && preview.data.plan.length > 0 && (
              <ul className="tips text-sm">
                {preview.data.plan.map((p) => (
                  <li key={p.savingId}>
                    {p.title}: −{formatMoney(p.monthlyImpact, lang)}
                    {t.common.perMonth}
                  </li>
                ))}
              </ul>
            )}
          </AiText>
        </div>
      )}

      {failed && (
        <p className="field__error" role="alert">
          {t.common.errorBody}
        </p>
      )}

      <div className="actions">
        {editing && (
          <button type="button" className="btn btn--ghost" onClick={onDelete} disabled={remove.isPending} style={{ color: 'var(--color-negative)', marginRight: 'auto' }}>
            <Trash size={18} aria-hidden /> {t.common.delete}
          </button>
        )}
        <button type="button" className="btn btn--secondary" onClick={onDone}>
          {t.common.cancel}
        </button>
        <button type="submit" className="btn btn--primary" disabled={saving}>
          {t.common.save}
        </button>
      </div>
    </form>
  )
}
