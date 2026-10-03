import clsx from 'clsx'
import { Search } from 'lucide-react'
import { useT } from '../../i18n/context'
import type { ChatMessage } from '../../types'
import { AiBadge } from '../ui/AiText'
import { EvidenceButton } from '../ui/EvidenceButton'

export function MessageBubble({ message }: { message: ChatMessage }) {
  const t = useT()
  const isUser = message.role === 'user'
  const streaming = message.status === 'streaming'
  const error = message.status === 'error'
  const tools = message.toolsUsed ?? []

  return (
    <div className={clsx('msg', isUser ? 'msg--user' : 'msg--assistant', error && 'msg--error')}>
      <span className="visually-hidden">{isUser ? t.chat.you : t.chat.coach}:</span>

      {!isUser && tools.length > 0 && (
        <div className="msg__meta">
          <Search aria-hidden /> {t.chat.toolUsed} {tools.map((name) => t.chat.tools[name] ?? name).join(', ')}
        </div>
      )}

      <div className="msg__bubble">
        {error && !message.content ? (
          t.chat.error
        ) : streaming && !message.content ? (
          <span className="typing" aria-label={t.chat.typing}>
            <span />
            <span />
            <span />
          </span>
        ) : (
          <>
            {message.content}
            {streaming && <span className="msg__cursor" aria-hidden />}
          </>
        )}
      </div>

      {!isUser && message.status === 'done' && message.content && (
        <div className="msg__meta">
          {message.fallback ? <span className="text-xs text-muted">{t.chat.fallback}</span> : <AiBadge />}
          {message.evidence && message.evidence.transactionIds.length > 0 && (
            <EvidenceButton
              evidence={message.evidence}
              subject={t.chat.sources(message.evidence.transactionIds.length)}
              label={t.chat.sources(message.evidence.transactionIds.length)}
            />
          )}
        </div>
      )}
      {error && message.content && <div className="msg__meta">{t.chat.error}</div>}
    </div>
  )
}
