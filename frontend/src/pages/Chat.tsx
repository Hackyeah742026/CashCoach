import { MessageCircle, RotateCcw, Sparkles } from 'lucide-react'
import { useEffect, useRef } from 'react'
import { ChatInput } from '../components/chat/ChatInput'
import '../components/chat/chat.css'
import { MessageBubble } from '../components/chat/MessageBubble'
import { EmptyState } from '../components/ui/States'
import { useChat } from '../hooks/useChat'
import { useReducedMotion } from '../hooks/useMediaQuery'
import { useT } from '../i18n/context'

export function Chat() {
  const t = useT()
  const { messages, isStreaming, send, stop, reset } = useChat()
  const endRef = useRef<HTMLDivElement>(null)
  const reducedMotion = useReducedMotion()
  const lastContent = messages[messages.length - 1]?.content

  // Keep the newest text in view while it streams
  useEffect(() => {
    if (messages.length > 0) endRef.current?.scrollIntoView({ behavior: reducedMotion ? 'auto' : 'smooth', block: 'end' })
  }, [messages.length, lastContent, reducedMotion])

  return (
    <div className="container--narrow" style={{ marginInline: 'auto' }}>
      <header className="page-header">
        <h1>
          {t.chat.title} <Sparkles size={24} style={{ display: 'inline', color: 'var(--color-ai)' }} aria-hidden />
        </h1>
        {messages.length > 0 && (
          <button type="button" className="btn btn--ghost" onClick={reset}>
            <RotateCcw size={18} aria-hidden /> {t.chat.newChat}
          </button>
        )}
      </header>

      <div className="chat">
        <div className="chat__messages" role="log" aria-live="polite" aria-relevant="additions">
          {messages.length === 0 && (
            <>
              <EmptyState icon={MessageCircle} title={t.chat.tryAsking} />
              <div className="chat__suggestions">
                {t.chat.suggestions.map((s) => (
                  <button key={s} type="button" className="chip" onClick={() => send(s)}>
                    <Sparkles aria-hidden /> {s}
                  </button>
                ))}
              </div>
            </>
          )}
          {messages.map((m) => (
            <MessageBubble key={m.id} message={m} />
          ))}
          <div ref={endRef} />
        </div>

        <ChatInput onSend={send} onStop={stop} streaming={isStreaming} />
        <p className="text-xs text-subtle text-center" style={{ marginTop: 'var(--space-2)' }}>
          {t.common.disclaimer}
        </p>
      </div>
    </div>
  )
}
