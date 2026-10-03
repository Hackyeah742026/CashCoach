import { RotateCcw, TriangleAlert, type LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'
import { useT } from '../../i18n/context'

interface EmptyStateProps {
  icon?: LucideIcon
  emoji?: string
  title: string
  body?: string
  action?: ReactNode
}

export function EmptyState({ icon: Icon, emoji, title, body, action }: EmptyStateProps) {
  return (
    <div className="empty-state">
      <span className="empty-state__icon" aria-hidden>
        {Icon ? <Icon /> : emoji}
      </span>
      <h2 style={{ fontSize: 'var(--text-xl)' }}>{title}</h2>
      {body && <p>{body}</p>}
      {action}
    </div>
  )
}

export function ErrorState({ onRetry }: { onRetry?: () => void }) {
  const t = useT()
  return (
    <div className="card" role="alert">
      <EmptyState
        icon={TriangleAlert}
        title={t.common.errorTitle}
        body={t.common.errorBody}
        action={
          onRetry && (
            <button type="button" className="btn btn--secondary" onClick={onRetry}>
              <RotateCcw aria-hidden size={18} /> {t.common.retry}
            </button>
          )
        }
      />
    </div>
  )
}
