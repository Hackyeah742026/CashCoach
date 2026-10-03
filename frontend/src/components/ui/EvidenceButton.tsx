import { Info } from 'lucide-react'
import { useT } from '../../i18n/context'
import type { Evidence } from '../../types'
import { useOpenEvidence } from './evidenceContext'

interface EvidenceButtonProps {
  evidence: Evidence
  /** What the evidence explains, used as the drawer title */
  subject?: string
  /** Show text next to the icon (e.g. "9 transactions") */
  label?: string
}

/** The "ⓘ" next to any number or AI sentence — opens the Evidence drawer. */
export function EvidenceButton({ evidence, subject, label }: EvidenceButtonProps) {
  const t = useT()
  const open = useOpenEvidence()
  return (
    <button
      type="button"
      className="evidence-btn"
      onClick={(e) => {
        e.stopPropagation()
        open({ evidence, subject })
      }}
      aria-label={label ? undefined : subject ? `${t.common.why} — ${subject}` : t.common.why}
      title={t.common.why}
    >
      <Info aria-hidden />
      {label}
    </button>
  )
}
