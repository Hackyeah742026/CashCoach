import clsx from 'clsx'
import { ChevronLeft, ChevronRight, Pause, Play, X } from 'lucide-react'
import { useCallback, useEffect, useRef, useState, type CSSProperties, type MouseEvent, type PointerEvent, type ReactNode } from 'react'
import { useBodyScrollLock } from '../../hooks/useBodyScrollLock'
import { useReducedMotion } from '../../hooks/useMediaQuery'
import { useT } from '../../i18n/context'
import type { Wrapped } from '../../types'
import { BiggestChangeCard, IntroCard, OutroCard, PersonalityCard, TopMerchantCard, TotalSpentCard } from './WrappedCards'

const CARD_MS = 6000
const HOLD_MS = 250
const SWIPE_PX = 50

interface StoryPlayerProps {
  data: Wrapped
  onClose: () => void
  onSeeHow: () => void
}

/**
 * Instagram/Spotify-style story player.
 * Tap right = next, tap left = previous, hold = pause, swipe = navigate,
 * ←/→/Space/Esc on keyboard. With reduced motion there is no auto-advance.
 */
export function StoryPlayer({ data, onClose, onSeeHow }: StoryPlayerProps) {
  const t = useT()
  const reducedMotion = useReducedMotion()
  const [index, setIndex] = useState(0)
  const [paused, setPaused] = useState(false)
  const [sharing, setSharing] = useState(false)
  const [shareError, setShareError] = useState(false)
  const rootRef = useRef<HTMLDivElement>(null)
  const frameRef = useRef<HTMLDivElement>(null)
  const pointer = useRef<{ x: number; time: number; handled: boolean } | null>(null)
  useBodyScrollLock(true)

  const share = useCallback(async () => {
    if (!frameRef.current) return
    setSharing(true)
    setShareError(false)
    try {
      const { toPng } = await import('html-to-image')
      const dataUrl = await toPng(frameRef.current, {
        pixelRatio: 2,
        filter: (node) => !(node instanceof HTMLElement && node.dataset.noShare !== undefined),
      })
      const blob = await (await fetch(dataUrl)).blob()
      const file = new File([blob], `cashcoach-wrapped-${data.month}.png`, { type: 'image/png' })
      if (navigator.canShare?.({ files: [file] })) {
        await navigator.share({ files: [file], title: 'CashCoach Wrapped' })
      } else {
        const a = document.createElement('a')
        a.href = dataUrl
        a.download = file.name
        a.click()
      }
    } catch (err) {
      if (!(err instanceof DOMException && err.name === 'AbortError')) setShareError(true)
    } finally {
      setSharing(false)
    }
  }, [data.month])

  const cards: ReactNode[] = [
    <IntroCard data={data} />,
    <TotalSpentCard data={data} />,
    <TopMerchantCard data={data} />,
    <BiggestChangeCard data={data} />,
    <PersonalityCard data={data} />,
    <OutroCard data={data} onSeeHow={onSeeHow} onShare={share} sharing={sharing} />,
  ]
  const last = cards.length - 1

  const next = useCallback(() => setIndex((i) => Math.min(last, i + 1)), [last])
  const prev = useCallback(() => setIndex((i) => Math.max(0, i - 1)), [])

  // Keyboard + initial focus
  useEffect(() => {
    rootRef.current?.focus()
    function onKey(e: KeyboardEvent) {
      if (document.querySelector('.sheet')) return // evidence drawer open on top
      if (e.key === 'ArrowRight') next()
      else if (e.key === 'ArrowLeft') prev()
      else if (e.key === 'Escape') onClose()
      else if (e.key === ' ') {
        e.preventDefault()
        setPaused((p) => !p)
      }
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [next, prev, onClose])

  function onPointerDown(e: PointerEvent) {
    if ((e.target as HTMLElement).closest('button')) return
    pointer.current = { x: e.clientX, time: e.timeStamp, handled: false }
    setPaused(true)
  }

  function onPointerUp(e: PointerEvent) {
    const p = pointer.current
    if ((e.target as HTMLElement).closest('button')) return // buttons handle their own clicks
    setPaused(false)
    if (!p) return
    const dx = e.clientX - p.x
    if (Math.abs(dx) > SWIPE_PX) {
      p.handled = true
      if (dx < 0) next()
      else prev()
    } else if (e.timeStamp - p.time > HOLD_MS) {
      p.handled = true // it was a hold-to-pause, not a tap
    }
  }

  function onStageClick(e: MouseEvent<HTMLDivElement>) {
    if (pointer.current?.handled) return
    const rect = e.currentTarget.getBoundingClientRect()
    if (e.clientX - rect.left < rect.width / 3) prev()
    else next()
  }

  const autoAdvance = !reducedMotion && index < last

  return (
    <div
      ref={rootRef}
      className={clsx('story', paused && 'story--paused')}
      role="dialog"
      aria-modal="true"
      aria-label={`${t.wrapped.title} — ${t.wrapped.cardOf(index + 1, cards.length)}`}
      tabIndex={-1}
      style={{ '--story-duration': `${CARD_MS}ms` } as CSSProperties}
      onClickCapture={(e) => {
        // Opening the evidence drawer pauses the story until the user resumes
        if ((e.target as HTMLElement).closest('.evidence-btn')) setPaused(true)
      }}
    >
      <button type="button" className="icon-btn story__nav story__nav--prev" onClick={prev} disabled={index === 0} aria-label={t.wrapped.prev}>
        <ChevronLeft />
      </button>

      <div ref={frameRef} className="story__frame">
        <div className="story__top" data-no-share>
          <div className="story__segments" aria-hidden>
            {cards.map((_, i) => (
              <div key={i} className="story__segment">
                <div
                  key={i === index ? `active-${index}` : i}
                  className={clsx(
                    'story__segment-fill',
                    (i < index || (i === index && !autoAdvance)) && 'story__segment-fill--done',
                    i === index && autoAdvance && 'story__segment-fill--active',
                  )}
                  onAnimationEnd={i === index && autoAdvance ? next : undefined}
                />
              </div>
            ))}
          </div>
          <div className="story__controls">
            <button type="button" className="icon-btn icon-btn--sm" onClick={() => setPaused((p) => !p)} aria-label={paused ? t.wrapped.resume : t.wrapped.pause}>
              {paused ? <Play /> : <Pause />}
            </button>
            <button type="button" className="icon-btn icon-btn--sm" onClick={onClose} aria-label={t.wrapped.close}>
              <X />
            </button>
          </div>
        </div>

        <div
          key={index}
          className={`story__card story__card--${index + 1}`}
          onPointerDown={onPointerDown}
          onPointerUp={onPointerUp}
          onPointerCancel={() => setPaused(false)}
          onClick={onStageClick}
          aria-live="polite"
        >
          {cards[index]}
          {shareError && index === last && (
            <p className="story__pill" role="alert" data-no-share>
              {t.wrapped.shareFailed}
            </p>
          )}
        </div>
      </div>

      <button type="button" className="icon-btn story__nav story__nav--next" onClick={next} disabled={index === last} aria-label={t.wrapped.next}>
        <ChevronRight />
      </button>
    </div>
  )
}
