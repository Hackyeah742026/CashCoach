import { useState, type SyntheticEvent } from 'react'
import { useT } from '../../i18n/context'
import { inputToMoney, moneyToInput } from '../../lib/format'
import type { IncomeAnswer, Money, PaydayRule } from '../../types'
import { Field } from '../ui/Field'

export interface IncomeFormValues {
  day: number | null
  dayRule: PaydayRule
  amount: Money | null
}

interface IncomeFormProps {
  initial?: IncomeFormValues
  source?: string | null
  pending?: boolean
  error?: boolean
  onSubmit: (answer: Extract<IncomeAnswer, { hasIncome: true }>) => void
  onBack?: () => void
}

/** Payday (a day of month or the last working day) and the monthly amount. Shared by onboarding and Settings. */
export function IncomeForm({ initial, source, pending, error, onSubmit, onBack }: IncomeFormProps) {
  const t = useT()
  const [day, setDay] = useState(initial?.day && initial.dayRule === 'fixed_day' ? String(initial.day) : '')
  const [lastWorkingDay, setLastWorkingDay] = useState(initial?.dayRule === 'last_working_day')
  const [amount, setAmount] = useState(initial?.amount ? moneyToInput(initial.amount) : '')
  const [touched, setTouched] = useState(false)

  const dayNum = Number(day)
  const dayValid = lastWorkingDay || (Number.isInteger(dayNum) && dayNum >= 1 && dayNum <= 31)
  const amountMoney = inputToMoney(amount)
  const amountValid = amountMoney !== null && Number(amountMoney) > 0

  function submit(e: SyntheticEvent) {
    e.preventDefault()
    setTouched(true)
    if (!dayValid || !amountValid) return
    onSubmit({
      hasIncome: true,
      day: lastWorkingDay ? 31 : dayNum,
      dayRule: lastWorkingDay ? 'last_working_day' : 'fixed_day',
      amount: amountMoney!,
      source,
    })
  }

  return (
    <form className="stack" onSubmit={submit} noValidate>
      <Field
        label={t.income.day}
        type="number"
        min={1}
        max={31}
        inputMode="numeric"
        placeholder="10"
        value={lastWorkingDay ? '' : day}
        disabled={lastWorkingDay}
        onChange={(e) => setDay(e.target.value)}
        error={touched && !dayValid ? t.income.invalidDay : null}
      />
      <label className="cluster text-sm" style={{ gap: 'var(--space-2)' }}>
        <input type="checkbox" checked={lastWorkingDay} onChange={(e) => setLastWorkingDay(e.target.checked)} />
        {t.income.lastWorkingDay}
      </label>
      <Field
        label={t.income.amount}
        inputMode="decimal"
        placeholder="4500"
        value={amount}
        onChange={(e) => setAmount(e.target.value)}
        error={touched && !amountValid ? t.income.invalidAmount : null}
      />
      {error && <p className="field__error">{t.common.errorBody}</p>}
      <div className="actions">
        {onBack && (
          <button type="button" className="btn btn--ghost" onClick={onBack}>
            {t.income.back}
          </button>
        )}
        <button type="submit" className="btn btn--primary" disabled={pending}>
          {t.income.save}
        </button>
      </div>
    </form>
  )
}
