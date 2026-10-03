import { X } from 'lucide-react'
import { useEffect, useId, useRef, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { useBodyScrollLock } from '../../hooks/useBodyScrollLock'
import { useT } from '../../i18n/context'

interface SheetProps {
  open: boolean
  onClose: () => void
  title: ReactNode
  children: ReactNode
  /** Actions pinned under the content */
  footer?: ReactNode
}

const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

/** Bottom sheet on phones, right side panel from tablet up (styles in index.css). */
export function Sheet({ open, onClose, title, children, footer }: SheetProps) {
  const t = useT()
  const titleId = useId()
  const panelRef = useRef<HTMLDivElement>(null)
  const onCloseRef = useRef(onClose)
  useBodyScrollLock(open)

  useEffect(() => {
    onCloseRef.current = onClose
  })

  // Focus management: move focus in, trap Tab, close on Esc, restore focus on close
  useEffect(() => {
    if (!open) return
    const previouslyFocused = document.activeElement as HTMLElement | null
    const panel = panelRef.current
    panel?.focus()

    function onKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') {
        e.stopPropagation()
        onCloseRef.current()
        return
      }
      if (e.key !== 'Tab' || !panel) return
      const items = Array.from(panel.querySelectorAll<HTMLElement>(FOCUSABLE))
      if (items.length === 0) return
      const first = items[0]
      const last = items[items.length - 1]
      if (e.shiftKey && (document.activeElement === first || document.activeElement === panel)) {
        e.preventDefault()
        last.focus()
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault()
        first.focus()
      }
    }

    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('keydown', onKeyDown)
      previouslyFocused?.focus?.()
    }
  }, [open])

  if (!open) return null

  return createPortal(
    <>
      <div className="sheet-backdrop" onClick={onClose} aria-hidden />
      <div ref={panelRef} className="sheet" role="dialog" aria-modal="true" aria-labelledby={titleId} tabIndex={-1}>
        <div className="sheet__handle" aria-hidden />
        <div className="sheet__header">
          <h2 id={titleId}>{title}</h2>
          <button type="button" className="icon-btn" onClick={onClose} aria-label={t.common.close}>
            <X />
          </button>
        </div>
        <div className="sheet__body">{children}</div>
        {footer && <div className="actions" style={{ marginTop: 'var(--space-6)' }}>{footer}</div>}
      </div>
    </>,
    document.body,
  )
}
