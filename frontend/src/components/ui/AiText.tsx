import clsx from 'clsx'
import { ShieldCheck, Sparkles } from 'lucide-react'
import type { ReactNode } from 'react'
import { useT } from '../../i18n/context'
import type { FactCheck } from '../../types'

/** ✨ AI badge — marks any AI-written text. */
export function AiBadge() {
  const t = useT()
  return (
    <span className="badge badge--ai" title={t.common.aiBadgeTitle}>
      <Sparkles aria-hidden /> {t.common.aiBadge}
    </span>
  )
}

export function VerifiedBadge() {
  const t = useT()
  return (
    <span className="badge badge--verified" title={t.common.verifiedTitle}>
      <ShieldCheck aria-hidden /> {t.common.verified}
    </span>
  )
}

interface AiTextProps {
  children: ReactNode
  factCheck?: FactCheck
  /** Optional heading shown next to the badges */
  title?: string
  aiGenerated?: boolean
  className?: string
}

/** Block of AI-written text: violet accent line, ✨ badge and "verified" when fact-check passed. */
export function AiText({ children, factCheck, title, aiGenerated = true, className }: AiTextProps) {
  return (
    <div className={clsx('ai-block', aiGenerated && 'ai-text', className)}>
      <div className="ai-block__meta">
        {aiGenerated && <AiBadge />}
        {title && <span className="text-sm text-muted">{title}</span>}
        {aiGenerated && factCheck === 'passed' && <VerifiedBadge />}
      </div>
      {children}
    </div>
  )
}
