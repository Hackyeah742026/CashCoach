import { ArrowRight, Share2 } from 'lucide-react'
import { useLanguage, useT } from '../../i18n/context'
import { CATEGORY_META, categoryLabel } from '../../lib/categories'
import { formatMonth, formatPercent } from '../../lib/format'
import type { Wrapped } from '../../types'
import { AiText } from '../ui/AiText'
import { EvidenceButton } from '../ui/EvidenceButton'
import { MoneyText } from '../ui/MoneyText'

// One component per story card. Numbers come from the API (computed);
// captions and the personality are AI-written and marked as such.

export function IntroCard({ data }: { data: Wrapped }) {
  const t = useT()
  const { lang } = useLanguage()
  return (
    <>
      <span className="story__kicker">★ {t.wrapped.introKicker}</span>
      <h2 className="story__title">{t.wrapped.introTitle(formatMonth(data.month, lang, false).toLowerCase())}</h2>
      <p className="story__pill">{t.wrapped.transactions(data.transactionCount)}</p>
      <p className="story__text" style={{ marginTop: 'var(--space-10)' }}>
        {t.wrapped.tapToStart}
      </p>
    </>
  )
}

export function TotalSpentCard({ data }: { data: Wrapped }) {
  const t = useT()
  const { lang } = useLanguage()
  return (
    <>
      <span className="story__kicker">{t.wrapped.youSpent}</span>
      <MoneyText value={data.totalSpent} absolute whole className="story__big" countUp />
      {data.changePct !== null && (
        <span className="story__pill">
          {formatPercent(data.changePct, lang)} {t.wrapped.vsPrev}
        </span>
      )}
      <AiText>
        <p className="story__text">{data.captions.totalSpent}</p>
      </AiText>
    </>
  )
}

export function TopMerchantCard({ data }: { data: Wrapped }) {
  const t = useT()
  const m = data.topMerchant
  return (
    <>
      <span className="story__kicker">{t.wrapped.topPlace}</span>
      <span className="story__emoji" aria-hidden>
        🏆
      </span>
      <h2 className="story__title">{m.name}</h2>
      <p className="story__text">
        {t.wrapped.orders(m.count)} · <MoneyText value={m.amount} whole />{' '}
        {m.evidence.transactionIds.length > 0 && <EvidenceButton evidence={m.evidence} subject={m.name} />}
      </p>
      <AiText>
        <p className="story__text">{data.captions.topMerchant}</p>
      </AiText>
    </>
  )
}

export function BiggestChangeCard({ data }: { data: Wrapped }) {
  const t = useT()
  const { lang } = useLanguage()
  const c = data.biggestChange
  return (
    <>
      <span className="story__kicker">{t.wrapped.biggestChange}</span>
      {c ? (
        <>
          <span className="story__emoji" aria-hidden>
            {CATEGORY_META[c.category].emoji}
          </span>
          <h2 className="story__title">{categoryLabel(c.category, lang)}</h2>
          <span className="story__big">{formatPercent(c.changePct, lang)}</span>
          <p className="story__text">
            <MoneyText value={c.from} whole /> → <MoneyText value={c.to} whole />
          </p>
          <AiText>
            <p className="story__text">{data.captions.biggestChange}</p>
          </AiText>
        </>
      ) : null}
      <p className="story__pill">{t.wrapped.cheapestDay(t.wrapped.weekdays[data.cheapestWeekday])}</p>
    </>
  )
}

export function PersonalityCard({ data }: { data: Wrapped }) {
  const t = useT()
  const p = data.personality
  return (
    <>
      <span className="story__kicker">{t.wrapped.personality}</span>
      <span className="story__emoji" aria-hidden>
        {p.emoji}
      </span>
      <h2 className="story__title">„{p.title}”</h2>
      <AiText aiGenerated={p.aiGenerated}>
        <p className="story__text">{p.description}</p>
      </AiText>
    </>
  )
}

interface OutroCardProps {
  data: Wrapped
  onSeeHow: () => void
  onShare: () => void
  sharing: boolean
}

export function OutroCard({ data, onSeeHow, onShare, sharing }: OutroCardProps) {
  const t = useT()
  return (
    <>
      <span className="story__kicker">{t.wrapped.couldSave}</span>
      <MoneyText value={data.potentialSavings} whole className="story__big" countUp />
      <span className="story__pill">{t.common.perMonth.replace('/', '/ ')}</span>
      <div className="story__actions" data-no-share>
        <button
          type="button"
          className="btn btn--primary"
          onClick={(e) => {
            e.stopPropagation()
            onSeeHow()
          }}
        >
          {t.wrapped.seeHow} <ArrowRight size={18} aria-hidden />
        </button>
        <button
          type="button"
          className="btn btn--secondary"
          disabled={sharing}
          onClick={(e) => {
            e.stopPropagation()
            onShare()
          }}
        >
          <Share2 size={18} aria-hidden /> {t.wrapped.share}
        </button>
      </div>
    </>
  )
}
