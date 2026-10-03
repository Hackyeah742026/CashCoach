import { Target } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router'
import { useDebouncedValue } from '../../hooks/useDebouncedValue'
import { useAffordability } from '../../hooks/useInsights'
import { useSettings } from '../../hooks/useSettings'
import { useLanguage, useT } from '../../i18n/context'
import { formatMoney, inputToMoney, moneyToInput } from '../../lib/format'
import type { AffordabilityAssumptions } from '../../types'
import { AiText } from '../ui/AiText'
import { EvidenceButton } from '../ui/EvidenceButton'
import { Field } from '../ui/Field'
import { MoneyText } from '../ui/MoneyText'
import { Sheet } from '../ui/Sheet'
import { Skeleton } from '../ui/Skeleton'
import { ErrorState } from '../ui/States'
import { VerdictCard } from '../ui/VerdictCard'
import type { AffordQuestion } from './QuickAfford'

interface AffordSheetProps {
  question: AffordQuestion | null
  onClose: () => void
}

export function AffordSheet({ question, onClose }: AffordSheetProps) {
  const t = useT()
  return (
    <Sheet open={question !== null} onClose={onClose} title={t.afford.title}>
      {/* key: fresh state for every new question */}
      {question && <AffordResult key={`${question.item}-${question.price}`} question={question} />}
    </Sheet>
  )
}

interface AssumptionEdits {
  payday?: string
  safetyBuffer?: string
  currentBalance?: string
}

function AffordResult({ question }: { question: AffordQuestion }) {
  const t = useT()
  const { lang } = useLanguage()
  const navigate = useNavigate()
  const { data: settings } = useSettings()
  const afford = useAffordability()
  const { mutate } = afford
  const [edits, setEdits] = useState<AssumptionEdits>({})
  const debouncedEdits = useDebouncedValue(edits, 350)

  // Assumptions = saved settings, overridden by what the user typed (if valid)
  const assumptions = useMemo<AffordabilityAssumptions | undefined>(() => {
    if (!settings) return undefined
    const payday = Number(debouncedEdits.payday)
    return {
      payday: debouncedEdits.payday && payday >= 1 && payday <= 31 ? payday : settings.payday,
      safetyBuffer: (debouncedEdits.safetyBuffer && inputToMoney(debouncedEdits.safetyBuffer)) || settings.safetyBuffer,
      currentBalance: (debouncedEdits.currentBalance && inputToMoney(debouncedEdits.currentBalance)) || settings.currentBalance,
    }
  }, [settings, debouncedEdits])

  // (Re)calculate on the backend whenever the question or assumptions change
  useEffect(() => {
    if (assumptions) mutate({ item: question.item, price: question.price, assumptions })
  }, [assumptions, mutate, question])

  const result = afford.data
  const verdictSubtitle = result
    ? result.shortfall
      ? t.afford.shortBy(formatMoney(result.shortfall, lang))
      : result.leftAfter
        ? t.afford.leftAfter(formatMoney(result.leftAfter, lang))
        : undefined
    : undefined

  return (
    <>
      <p>
        <span className="afford-item">{question.item}</span> · <MoneyText value={question.price} />
      </p>

      {afford.isError && <ErrorState onRetry={() => assumptions && mutate({ ...question, assumptions })} />}
      {!result && !afford.isError && (
        <>
          <Skeleton height={80} radius="var(--radius-lg)" />
          <Skeleton height={120} />
        </>
      )}

      {result && (
        <div className="stack" aria-busy={afford.isPending} style={{ opacity: afford.isPending ? 0.6 : 1 }}>
          <VerdictCard verdict={result.verdict} title={t.afford.verdict[result.verdict]} subtitle={verdictSubtitle} />

          <section>
            <h3 className="text-sm text-muted" style={{ marginBottom: 'var(--space-1)' }}>
              {t.afford.breakdownTitle}
            </h3>
            <div className="kv-list">
              {result.breakdown.map((line) => (
                <div key={line.label} className="kv-list__row">
                  <span className="kv-list__label">
                    {line.label}
                    {line.transactionIds && <EvidenceButton evidence={{ transactionIds: line.transactionIds }} subject={line.label} />}
                  </span>
                  <MoneyText value={line.amount} />
                </div>
              ))}
              <div className="kv-list__row kv-list__row--total">
                <span>{t.afford.safeToSpend}</span>
                <MoneyText value={result.safeToSpend} />
              </div>
            </div>
          </section>

          <AiText factCheck={result.explanation.factCheck} aiGenerated={result.explanation.aiGenerated}>
            <p>{result.explanation.text}</p>
            {result.explanation.tips.length > 0 && (
              <ul className="tips">
                {result.explanation.tips.map((tip) => (
                  <li key={tip}>{tip}</li>
                ))}
              </ul>
            )}
          </AiText>
        </div>
      )}

      {settings && (
        <details className="assumptions">
          <summary>{t.afford.assumptions}</summary>
          <p className="text-sm text-muted" style={{ marginBottom: 'var(--space-3)' }}>
            {t.afford.assumptionsHint}
          </p>
          <div className="stack stack--sm">
            <Field
              label={t.afford.payday}
              type="number"
              min={1}
              max={31}
              inputMode="numeric"
              value={edits.payday ?? String(settings.payday)}
              onChange={(e) => setEdits((s) => ({ ...s, payday: e.target.value }))}
            />
            <Field
              label={t.afford.buffer}
              inputMode="decimal"
              value={edits.safetyBuffer ?? moneyToInput(settings.safetyBuffer)}
              onChange={(e) => setEdits((s) => ({ ...s, safetyBuffer: e.target.value }))}
            />
            <Field
              label={t.afford.balance}
              inputMode="decimal"
              value={edits.currentBalance ?? moneyToInput(settings.currentBalance)}
              onChange={(e) => setEdits((s) => ({ ...s, currentBalance: e.target.value }))}
            />
          </div>
        </details>
      )}

      <div className="actions">
        <button
          type="button"
          className="btn btn--secondary"
          onClick={() =>
            navigate(`/goals?new=1&name=${encodeURIComponent(question.item)}&target=${encodeURIComponent(question.price)}`)
          }
        >
          <Target size={18} aria-hidden /> {t.afford.makeGoal}
        </button>
      </div>
    </>
  )
}
