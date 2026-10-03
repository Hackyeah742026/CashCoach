import { Send, Square } from 'lucide-react'
import { useRef, useState, type SyntheticEvent, type KeyboardEvent } from 'react'
import { useT } from '../../i18n/context'

interface ChatInputProps {
  onSend: (text: string) => void
  onStop: () => void
  streaming: boolean
}

/** Enter sends, Shift+Enter adds a new line. Grows with the text. */
export function ChatInput({ onSend, onStop, streaming }: ChatInputProps) {
  const t = useT()
  const [text, setText] = useState('')
  const ref = useRef<HTMLTextAreaElement>(null)

  function resize() {
    const el = ref.current
    if (!el) return
    el.style.height = 'auto'
    el.style.height = `${el.scrollHeight}px`
  }

  function submit(e?: SyntheticEvent) {
    e?.preventDefault()
    if (!text.trim() || streaming) return
    onSend(text)
    setText('')
    requestAnimationFrame(resize)
  }

  function onKeyDown(e: KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing) {
      e.preventDefault()
      submit()
    }
  }

  return (
    <form className="chat-input" onSubmit={submit}>
      <label htmlFor="chat-text" className="visually-hidden">
        {t.chat.placeholder}
      </label>
      <textarea
        id="chat-text"
        ref={ref}
        rows={1}
        value={text}
        placeholder={t.chat.placeholder}
        onChange={(e) => {
          setText(e.target.value)
          resize()
        }}
        onKeyDown={onKeyDown}
      />
      {streaming ? (
        <button type="button" className="btn btn--secondary" onClick={onStop} aria-label={t.chat.stop}>
          <Square size={18} aria-hidden />
        </button>
      ) : (
        <button type="submit" className="btn btn--primary" disabled={!text.trim()} aria-label={t.chat.send}>
          <Send size={18} aria-hidden />
        </button>
      )}
    </form>
  )
}
