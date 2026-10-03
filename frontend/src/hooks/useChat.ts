import { useCallback, useEffect, useRef, useState } from 'react'
import { streamChat } from '../api/client'
import { useLanguage } from '../i18n/context'
import type { ChatMessage } from '../types'

let counter = 0
const newId = () => `m${++counter}`

/** Chat state + streaming. Messages live in memory for the session. */
export function useChat() {
  const { lang } = useLanguage()
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [isStreaming, setIsStreaming] = useState(false)
  const abortRef = useRef<AbortController | null>(null)

  // Abort an in-flight stream when the chat unmounts
  useEffect(() => () => abortRef.current?.abort(), [])

  const patchMessage = useCallback((id: string, patch: (m: ChatMessage) => ChatMessage) => {
    setMessages((prev) => prev.map((m) => (m.id === id ? patch(m) : m)))
  }, [])

  const send = useCallback(
    async (text: string) => {
      const content = text.trim()
      if (!content || isStreaming) return

      const userMsg: ChatMessage = { id: newId(), role: 'user', content, status: 'done' }
      const assistantId = newId()
      const history = [...messages, userMsg]
      setMessages([...history, { id: assistantId, role: 'assistant', content: '', status: 'streaming', toolsUsed: [] }])
      setIsStreaming(true)

      const controller = new AbortController()
      abortRef.current = controller

      try {
        for await (const event of streamChat(history, lang, controller.signal)) {
          switch (event.type) {
            case 'delta':
              patchMessage(assistantId, (m) => ({ ...m, content: m.content + event.text }))
              break
            case 'tool':
              patchMessage(assistantId, (m) => ({ ...m, toolsUsed: [...(m.toolsUsed ?? []), event.name] }))
              break
            case 'evidence':
              patchMessage(assistantId, (m) => ({ ...m, evidence: event.evidence }))
              break
            case 'done':
              patchMessage(assistantId, (m) => ({ ...m, status: 'done' }))
              break
            case 'error':
              patchMessage(assistantId, (m) => ({ ...m, status: 'error' }))
              break
          }
        }
        patchMessage(assistantId, (m) => (m.status === 'streaming' ? { ...m, status: 'done' } : m))
      } catch (err) {
        const aborted = err instanceof DOMException && err.name === 'AbortError'
        patchMessage(assistantId, (m) => ({ ...m, status: aborted ? 'done' : 'error' }))
      } finally {
        setIsStreaming(false)
        abortRef.current = null
      }
    },
    [isStreaming, lang, messages, patchMessage],
  )

  const stop = useCallback(() => abortRef.current?.abort(), [])

  const reset = useCallback(() => {
    abortRef.current?.abort()
    setMessages([])
  }, [])

  return { messages, isStreaming, send, stop, reset }
}
