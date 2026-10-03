import { ArrowRight, Check, LoaderCircle, Pencil } from 'lucide-react'
import { useState } from 'react'
import { useConfirmIncome, useIncomeDetection } from '../../hooks/useSettings'
import { useLanguage, useT } from '../../i18n/context'
import { formatMoney } from '../../lib/format'
import type { IncomeAnswer, IncomeCandidate } from '../../types'
import { EvidenceButton } from '../ui/EvidenceButton'
import { IncomeForm } from './IncomeForm'

type Phase = 'ask' | 'guess' | 'form'

/** Onboarding: "Do you get a regular income?", then the guess from the CSV to confirm or correct. */
export function IncomeStep({ onDone }: { onDone: () => void }) {
  const t = useT()
  const detection = useIncomeDetection()
  const confirm = useConfirmIncome()
  const [phase, setPhase] = useState<Phase>('ask')
  const guess = detection.data?.guess ?? null

  const save = (answer: IncomeAnswer) => confirm.mutate(answer, { onSuccess: onDone })

  if (phase === 'ask') {
    return (
      <>
        <h1>{t.income.question}</h1>
        <p className="onboarding__lead">{t.income.questionBody}</p>
        {confirm.isError && <p className="field__error">{t.common.errorBody}</p>}
        <div className="actions">
          <button type="button" className="btn btn--secondary" disabled={confirm.isPending} onClick={() => save({ hasIncome: false })}>
            {t.income.no}
          </button>
          <button
            type="button"
            className="btn btn--primary"
            disabled={detection.isLoading}
            onClick={() => setPhase(guess ? 'guess' : 'form')}
          >
            {detection.isLoading ? <LoaderCircle className="spinner" size={18} aria-hidden /> : null} {t.income.yes}{' '}
            <ArrowRight size={18} aria-hidden />
          </button>
        </div>
        <p className="text-sm text-muted">{t.income.noneNote}</p>
      </>
    )
  }

  if (phase === 'guess' && guess) {
    return (
      <>
        <h1>{t.income.guessTitle}</h1>
        <GuessCard guess={guess} />
        {confirm.isError && <p className="field__error">{t.common.errorBody}</p>}
        <div className="actions">
          <button type="button" className="btn btn--secondary" onClick={() => setPhase('form')}>
            <Pencil size={18} aria-hidden /> {t.income.correct}
          </button>
          <button
            type="button"
            className="btn btn--primary"
            disabled={confirm.isPending}
            onClick={() => save({ hasIncome: true, day: guess.day, dayRule: guess.dayRule, amount: guess.amount, source: guess.source })}
          >
            <Check size={18} aria-hidden /> {t.income.confirm}
          </button>
        </div>
      </>
    )
  }

  return (
    <>
      <h1>{guess ? t.income.formTitle : t.income.noGuessTitle}</h1>
      {!guess && <p className="onboarding__lead">{t.income.noGuessBody}</p>}
      <IncomeForm
        initial={guess ? { day: guess.day, dayRule: guess.dayRule, amount: guess.amount } : undefined}
        source={guess?.source}
        pending={confirm.isPending}
        error={confirm.isError}
        onSubmit={save}
        onBack={() => setPhase(guess ? 'guess' : 'ask')}
      />
    </>
  )
}

function GuessCard({ guess }: { guess: IncomeCandidate }) {
  const t = useT()
  const { lang } = useLanguage()
  const day = guess.dayRule === 'last_working_day' ? t.income.dayLast : t.income.dayFixed(guess.day)
  const badge = guess.confidence === 'high' ? 'badge--positive' : guess.confidence === 'medium' ? 'badge--caution' : 'badge--negative'

  return (
    <div className="card stack" style={{ background: 'var(--color-surface-muted)' }}>
      <p style={{ fontSize: 'var(--text-lg)', fontWeight: 'var(--weight-medium)' }}>
        {t.income.guess(formatMoney(guess.amount, lang), day, guess.source)}
      </p>
      {guess.amountMin !== guess.amountMax && (
        <p className="text-sm text-muted">{t.income.range(formatMoney(guess.amountMin, lang), formatMoney(guess.amountMax, lang))}</p>
      )}
      <div className="cluster">
        <span className={`badge ${badge}`}>{t.income.confidence[guess.confidence]}</span>
        <span className="text-sm text-muted">{t.income.monthsSeen(guess.monthsSeen)}</span>
        <EvidenceButton evidence={guess.evidence} subject={guess.source} label={t.income.transactions} />
      </div>
    </div>
  )
}
