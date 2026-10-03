import { useState, type SyntheticEvent } from 'react'
import { useSettings, useUpdateSettings } from '../../hooks/useSettings'
import { useLanguage, useT } from '../../i18n/context'
import { formatMoney, inputToMoney } from '../../lib/format'
import { Field } from '../ui/Field'

/** Shown while the balance is only an estimate (the CSV had no balance column): confirm it or type the real one. */
export function BalanceCheckCard() {
  const t = useT()
  const { lang } = useLanguage()
  const { data: settings } = useSettings()
  const update = useUpdateSettings()
  const [editing, setEditing] = useState(false)
  const [value, setValue] = useState('')

  if (!settings?.balanceIsEstimate || !settings.onboarded) return null

  const parsed = inputToMoney(value)
  // An estimate of 0 means the history alone says nothing useful: ask for the number directly.
  const noEstimate = Number(settings.currentBalance) <= 0
  const showForm = editing || noEstimate

  function save(e: SyntheticEvent) {
    e.preventDefault()
    if (parsed) update.mutate({ currentBalance: parsed })
  }

  return (
    <section className="card stack" aria-live="polite">
      <h2 style={{ fontSize: 'var(--text-lg)' }}>
        {noEstimate ? t.income.balanceAsk : t.income.balanceCheck(formatMoney(settings.currentBalance, lang, { whole: true }))}
      </h2>
      <p className="text-sm text-muted">{t.income.balanceCheckBody}</p>
      {showForm ? (
        <form className="stack" onSubmit={save} noValidate>
          <Field label={t.income.balanceLabel} inputMode="decimal" placeholder="1600" value={value} onChange={(e) => setValue(e.target.value)} />
          <div className="actions">
            {!noEstimate && (
              <button type="button" className="btn btn--ghost" onClick={() => setEditing(false)}>
                {t.income.back}
              </button>
            )}
            <button type="submit" className="btn btn--primary" disabled={!parsed || update.isPending}>
              {t.income.save}
            </button>
          </div>
        </form>
      ) : (
        <div className="actions">
          <button type="button" className="btn btn--secondary" onClick={() => setEditing(true)}>
            {t.income.balanceFix}
          </button>
          <button
            type="button"
            className="btn btn--primary"
            disabled={update.isPending}
            onClick={() => update.mutate({ currentBalance: settings.currentBalance })}
          >
            {t.income.balanceYes}
          </button>
        </div>
      )}
      {update.isError && <p className="field__error">{t.common.errorBody}</p>}
    </section>
  )
}
