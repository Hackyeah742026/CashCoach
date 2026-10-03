import clsx from 'clsx'
import { CircleCheck, CircleX, TriangleAlert } from 'lucide-react'
import type { Verdict } from '../../types'

const ICONS = { green: CircleCheck, yellow: TriangleAlert, red: CircleX } as const

interface VerdictCardProps {
  verdict: Verdict
  title: string
  subtitle?: string
}

/** Verdicts always combine icon + word + color (never color alone). */
export function VerdictCard({ verdict, title, subtitle }: VerdictCardProps) {
  const Icon = ICONS[verdict]
  return (
    <div className={clsx('verdict', `verdict--${verdict}`)} role="status">
      <span className="verdict__icon">
        <Icon aria-hidden />
      </span>
      <div>
        <div className="verdict__title">{title}</div>
        {subtitle && <div className="verdict__subtitle">{subtitle}</div>}
      </div>
    </div>
  )
}
