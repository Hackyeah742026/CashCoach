import clsx from 'clsx'
import { ArrowRight, LoaderCircle, ShieldCheck, Sparkles } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import '../components/onboarding/onboarding.css'
import { IncomeStep } from '../components/onboarding/IncomeStep'
import { TransactionUpload } from '../components/onboarding/TransactionUpload'
import { Segmented } from '../components/ui/Segmented'
import { useChangeLanguage, useImportTransactions } from '../hooks/useSettings'
import { useLanguage, useT } from '../i18n/context'
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

/** Welcome → upload (CSV or demo) → confirm the income found in the data → result. */
export function Onboarding() {
  const [step, setStep] = useState(0)
  const [result, setResult] = useState<ImportResult | null>(null)

  return (
    <div className="onboarding">
      <div className="container container--narrow onboarding__top">
        <Brand />
        <StepDots step={step} total={3} />
      </div>
      <main className="container container--narrow">
        <div className="card onboarding__card">
          {step === 0 ? (
            <WelcomeStep onNext={() => setStep(1)} />
          ) : step === 1 ? (
            <UploadStep
              onDone={(r) => {
                setResult(r)
                setStep(2)
              }}
            />
          ) : step === 2 || !result ? (
            <IncomeStep onDone={() => setStep(3)} />
          ) : (
            <ResultStep result={result} />
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
