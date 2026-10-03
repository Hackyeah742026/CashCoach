import { ShieldCheck, Trash } from 'lucide-react'
import { useState, type SyntheticEvent } from 'react'
import { useNavigate } from 'react-router'
import { Field } from '../components/ui/Field'
import { Segmented } from '../components/ui/Segmented'
import { IncomeForm } from '../components/onboarding/IncomeForm'
import { useChangeLanguage, useConfirmIncome, useDeleteAllData, useSettings, useUpdateSettings } from '../hooks/useSettings'
import { useLanguage, useT } from '../i18n/context'
import { formatMoney, inputToMoney, moneyToInput } from '../lib/format'
import { applyTheme, getThemePreference, type ThemePreference } from '../lib/theme'
import type { Language } from '../types'

export function Settings() {
  const t = useT()
  const { lang } = useLanguage()
  const changeLanguage = useChangeLanguage()
  const [theme, setTheme] = useState<ThemePreference>(getThemePreference)
  const navigate = useNavigate()
  const deleteAll = useDeleteAllData()

  return (
    <div className="container--narrow" style={{ marginInline: 'auto' }}>
      <header className="page-header">
        <h1>{t.settings.title}</h1>
      </header>

      <div className="stack">
        <section className="card stack">
          <div className="field">
            <span className="text-sm" style={{ fontWeight: 'var(--weight-medium)' }}>
              {t.settings.language}
            </span>
            <Segmented<Language>
              label={t.settings.language}
              value={lang}
              onChange={changeLanguage}
              options={[
                { value: 'pl', label: 'Polski' },
                { value: 'en', label: 'English' },
              ]}
            />
          </div>
          <div className="field">
            <span className="text-sm" style={{ fontWeight: 'var(--weight-medium)' }}>
              {t.settings.theme}
            </span>
            <Segmented<ThemePreference>
              label={t.settings.theme}
              value={theme}
              onChange={(v) => {
                setTheme(v)
                applyTheme(v)
              }}
              options={(['system', 'light', 'dark'] as const).map((v) => ({ value: v, label: t.settings.themes[v] }))}
            />
          </div>
        </section>

        <IncomeCard />
        <MoneyForm />

        <section className="card stack">
          <h2 style={{ fontSize: 'var(--text-lg)' }}>{t.settings.data}</h2>
          <p className="privacy-note text-sm text-muted" style={{ display: 'flex', gap: 'var(--space-2)' }}>
            <ShieldCheck size={18} aria-hidden style={{ flexShrink: 0, color: 'var(--color-positive)' }} /> {t.settings.about}
          </p>
          <button
            type="button"
            className="btn btn--secondary"
            style={{ color: 'var(--color-negative)', alignSelf: 'flex-start' }}
            disabled={deleteAll.isPending}
            onClick={() => {
              if (window.confirm(t.settings.deleteConfirm)) {
                deleteAll.mutate(undefined, { onSuccess: () => navigate('/onboarding', { replace: true }) })
              }
            }}
          >
            <Trash size={18} aria-hidden /> {t.settings.deleteAll}
          </button>
        </section>

        <p className="text-xs text-subtle text-center">{t.common.disclaimer}</p>
      </div>
    </div>
  )
}

function IncomeCard() {
  const t = useT()
  const { lang } = useLanguage()
  const { data: settings } = useSettings()
  const confirm = useConfirmIncome()
  const [editing, setEditing] = useState(false)

  if (!settings) return null
  const income = settings.income
  const day = income.dayRule === 'last_working_day' ? t.income.dayLast : t.income.dayFixed(income.day ?? settings.payday)
  const summary =
    income.status === 'none'
      ? t.income.noneSummary
      : income.status === 'confirmed' && income.amount
        ? t.income.summary(formatMoney(income.amount, lang), day)
        : t.income.unknownSummary

  return (
    <section className="card stack">
      <h2 style={{ fontSize: 'var(--text-lg)' }}>{t.income.settingsTitle}</h2>
      {editing ? (
        <>
          <IncomeForm
            initial={{ day: income.day, dayRule: income.dayRule, amount: income.amount }}
            source={income.source}
            pending={confirm.isPending}
            error={confirm.isError}
            onSubmit={(answer) => confirm.mutate(answer, { onSuccess: () => setEditing(false) })}
            onBack={() => setEditing(false)}
          />
          <button
            type="button"
            className="btn btn--ghost"
            style={{ alignSelf: 'flex-start' }}
            disabled={confirm.isPending}
            onClick={() => confirm.mutate({ hasIncome: false }, { onSuccess: () => setEditing(false) })}
          >
            {t.income.noneSummary}
          </button>
        </>
      ) : (
        <div className="cluster" style={{ justifyContent: 'space-between' }}>
          <span>
            {summary}
            {income.source && income.status === 'confirmed' && <span className="text-sm text-muted"> · {income.source}</span>}
          </span>
          <button type="button" className="btn btn--secondary" onClick={() => setEditing(true)}>
            {t.income.edit}
          </button>
        </div>
      )}
    </section>
  )
}

function MoneyForm() {
  const t = useT()
  const { data: settings } = useSettings()
  const update = useUpdateSettings()
  const [edits, setEdits] = useState<{ buffer?: string; balance?: string }>({})

  if (!settings) return null

  const buffer = edits.buffer ?? moneyToInput(settings.safetyBuffer)
  const balance = edits.balance ?? moneyToInput(settings.currentBalance)
  const valid = Boolean(inputToMoney(buffer) && inputToMoney(balance))

  function submit(e: SyntheticEvent) {
    e.preventDefault()
    const safetyBuffer = inputToMoney(buffer)
    const currentBalance = inputToMoney(balance)
    if (!safetyBuffer || !currentBalance) return
    // Send the balance only when it changed, so an estimate is not turned into a "confirmed" value by accident.
    update.mutate({ safetyBuffer, ...(edits.balance !== undefined ? { currentBalance } : {}) }, { onSuccess: () => setEdits({}) })
  }

  return (
    <form className="card stack" onSubmit={submit} noValidate>
      <h2 style={{ fontSize: 'var(--text-lg)' }}>{t.settings.finances}</h2>
      <Field
        label={t.afford.balance}
        hint={settings.balanceIsEstimate && edits.balance === undefined ? t.income.balanceEstimate : undefined}
        inputMode="decimal"
        value={balance}
        onChange={(e) => setEdits((s) => ({ ...s, balance: e.target.value }))}
      />
      <Field label={t.afford.buffer} inputMode="decimal" value={buffer} onChange={(e) => setEdits((s) => ({ ...s, buffer: e.target.value }))} />
      <div className="cluster">
        <button type="submit" className="btn btn--primary" disabled={!valid || update.isPending}>
          {t.common.save}
        </button>
        {update.isSuccess && Object.keys(edits).length === 0 && (
          <span className="text-sm" role="status" style={{ color: 'var(--color-positive)' }}>
            {t.settings.saved}
          </span>
        )}
      </div>
    </form>
  )
}
