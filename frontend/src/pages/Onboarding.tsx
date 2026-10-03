import clsx from 'clsx'
import { ArrowRight, LoaderCircle, ShieldCheck, Sparkles } from 'lucide-react'
import { useState, type SyntheticEvent } from 'react'
import { useNavigate } from 'react-router'
import '../components/onboarding/onboarding.css'
import { TransactionUpload } from '../components/onboarding/TransactionUpload'
import { Field } from '../components/ui/Field'
import { Segmented } from '../components/ui/Segmented'
import { useChangeLanguage, useImportTransactions, useSettings, useUpdateSettings } from '../hooks/useSettings'
import { useLanguage, useT } from '../i18n/context'
import { inputToMoney, moneyToInput } from '../lib/format'
import { Brand } from '../layout/AppShell'
import type { DemoPersona } from '../api/client'
import type { ImportResult, Language } from '../types'
import '../layout/layout.css'

function StepDots({ step, total }: { step: number; total: number }) {
  const t = useT()
  return (
    <div className="step-dots" role="img" aria-label={t.onboarding.stepLabel(step + 1, total)}>
      {Array.from({ length: total }, (_, i) => (
        <span key={i} className={clsx('step-dots__dot', i === step && 'step-dots__dot--active', i < step && 'step-dots__dot--done')} />
      ))}
    </div>
  )
}

export function Onboarding() {
  const [step, setStep] = useState(0)
  const [result, setResult] = useState<ImportResult | null>(null)

  return (
    <div className="onboarding">
      <div className="container container--narrow onboarding__top">
        <Brand />
        <StepDots step={result ? 3 : step} total={3} />
      </div>
      <main className="container container--narrow">
        <div className="card onboarding__card">
          {result ? (
            <ResultStep result={result} />
          ) : step === 0 ? (
            <WelcomeStep onNext={() => setStep(1)} />
          ) : step === 1 ? (
            <FinancesStep onNext={() => setStep(2)} />
          ) : (
            <UploadStep onDone={setResult} />
          )}
        </div>
      </main>
    </div>
  )
}

function WelcomeStep({ onNext }: { onNext: () => void }) {
  const t = useT()
  const { lang } = useLanguage()
  const changeLanguage = useChangeLanguage()
  return (
    <>
      <h1>{t.onboarding.welcomeTitle}</h1>
      <p className="onboarding__lead">{t.onboarding.welcomeBody}</p>
      <div className="field">
        <span className="text-sm" style={{ fontWeight: 'var(--weight-medium)' }}>
          {t.onboarding.language}
        </span>
        <Segmented<Language>
          label={t.onboarding.language}
          value={lang}
          onChange={changeLanguage}
          options={[
            { value: 'pl', label: 'Polski' },
            { value: 'en', label: 'English' },
          ]}
        />
      </div>
      <div className="actions">
        <button type="button" className="btn btn--primary" onClick={onNext}>
          {t.onboarding.continue} <ArrowRight size={18} aria-hidden />
        </button>
      </div>
    </>
  )
}

function FinancesStep({ onNext }: { onNext: () => void }) {
  const t = useT()
  const { data: settings } = useSettings()
  const update = useUpdateSettings()
  const [payday, setPayday] = useState<string | null>(null)
  const [balance, setBalance] = useState<string | null>(null)
  const [buffer, setBuffer] = useState<string | null>(null)

  // Show saved values until the user types
  const paydayValue = payday ?? String(settings?.payday ?? 10)
  const balanceValue = balance ?? ''
  const bufferValue = buffer ?? moneyToInput(settings?.safetyBuffer ?? '300')

  const paydayNum = Number(paydayValue)
  const paydayError = Number.isInteger(paydayNum) && paydayNum >= 1 && paydayNum <= 31 ? null : '1–31'
  const balanceMoney = balanceValue ? inputToMoney(balanceValue) : undefined
  const bufferMoney = inputToMoney(bufferValue || '0')

  function submit(e: SyntheticEvent) {
    e.preventDefault()
    if (paydayError || balanceMoney === null || !bufferMoney) return
    update.mutate(
      { payday: paydayNum, safetyBuffer: bufferMoney, ...(balanceMoney ? { currentBalance: balanceMoney } : {}) },
      { onSuccess: onNext },
    )
  }

  return (
    <form className="onboarding__card" onSubmit={submit} noValidate>
      <h1>{t.onboarding.financesTitle}</h1>
      <p className="onboarding__lead">{t.onboarding.financesBody}</p>
      <Field
        label={t.onboarding.payday}
        type="number"
        min={1}
        max={31}
        inputMode="numeric"
        value={paydayValue}
        onChange={(e) => setPayday(e.target.value)}
        error={payday !== null ? paydayError : null}
      />
      <Field
        label={t.onboarding.balance}
        hint={t.onboarding.balanceHint}
        inputMode="decimal"
        placeholder="1600"
        value={balanceValue}
        onChange={(e) => setBalance(e.target.value)}
        error={balanceMoney === null ? t.afford.invalidPrice : null}
      />
      <Field
        label={t.onboarding.buffer}
        hint={t.onboarding.bufferHint}
        inputMode="decimal"
        value={bufferValue}
        onChange={(e) => setBuffer(e.target.value)}
      />
      {update.isError && <p className="field__error">{t.common.errorBody}</p>}
      <div className="actions">
        <button type="submit" className="btn btn--primary" disabled={update.isPending}>
          {t.onboarding.continue} <ArrowRight size={18} aria-hidden />
        </button>
      </div>
    </form>
  )
}

function UploadStep({ onDone }: { onDone: (result: ImportResult) => void }) {
  const t = useT()
  const importer = useImportTransactions()
  const [file, setFile] = useState<File | null>(null)
  const [fileError, setFileError] = useState<string | null>(null)
  const [persona, setPersona] = useState<DemoPersona>('bnpl_heavy')

  function pick(f: File | null) {
    setFile(f)
    setFileError(f && !f.name.toLowerCase().endsWith('.csv') ? t.onboarding.invalidFile : null)
  }

  function run(source: File | DemoPersona) {
    importer.mutate(source, { onSuccess: onDone })
  }

  if (importer.isPending) {
    return (
      <div className="empty-state" role="status">
        <LoaderCircle className="spinner" style={{ width: 40, height: 40, color: 'var(--color-primary)' }} aria-hidden />
        <h2 style={{ fontSize: 'var(--text-xl)' }}>{t.onboarding.importing}</h2>
      </div>
    )
  }

  return (
    <>
      <h1>{t.onboarding.uploadTitle}</h1>
      <p className="onboarding__lead">{t.onboarding.uploadBody}</p>
      <TransactionUpload file={file} onFile={pick} error={fileError ?? (importer.isError ? t.onboarding.importError : null)} />
      <p className="privacy-note">
        <ShieldCheck aria-hidden /> {t.onboarding.privacy}
      </p>
      <div className="field">
        <span className="text-sm" style={{ fontWeight: 'var(--weight-medium)' }}>
          {t.onboarding.demoPersona}
        </span>
        <Segmented<DemoPersona>
          label={t.onboarding.demoPersona}
          value={persona}
          onChange={setPersona}
          options={(['student', 'first_job', 'bnpl_heavy'] as const).map((p) => ({ value: p, label: t.onboarding.personas[p] }))}
        />
      </div>
      <div className="actions">
        <button type="button" className="btn btn--ghost" onClick={() => run(persona)}>
          <Sparkles size={18} aria-hidden /> {t.onboarding.useDemo}
        </button>
        <button type="button" className="btn btn--primary" disabled={!file || Boolean(fileError)} onClick={() => file && run(file)}>
          {t.onboarding.import} <ArrowRight size={18} aria-hidden />
        </button>
      </div>
    </>
  )
}

function ResultStep({ result }: { result: ImportResult }) {
  const t = useT()
  const navigate = useNavigate()
  const items = [
    t.onboarding.resultImported(result.imported),
    t.onboarding.resultRules(result.categorizedByRules),
    t.onboarding.resultAi(result.categorizedByAi),
    t.onboarding.resultReview(result.needsReview),
  ]
  return (
    <>
      <h1>{t.onboarding.resultTitle}</h1>
      <div className="import-result">
        {items.map((text) => {
          const [value, ...rest] = text.split(' ')
          return (
            <div key={text} className="import-result__item">
              <div className="import-result__value">{value}</div>
              <div className="text-sm text-muted">{rest.join(' ')}</div>
            </div>
          )
        })}
      </div>
      <div className="actions">
        <button type="button" className="btn btn--primary" onClick={() => navigate('/', { replace: true })}>
          {t.onboarding.goHome} <ArrowRight size={18} aria-hidden />
        </button>
      </div>
    </>
  )
}
