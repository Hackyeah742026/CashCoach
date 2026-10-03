import clsx from 'clsx'
import { useEffect, useState } from 'react'
import { useReducedMotion } from '../../hooks/useMediaQuery'
import { useLanguage } from '../../i18n/context'
import { formatMoney, type MoneyFormatOptions } from '../../lib/format'
import type { Money } from '../../types'

interface MoneyTextProps extends MoneyFormatOptions {
  value: Money
  variant?: 'default' | 'hero'
  /** auto = green for positive values, neutral otherwise (spending is never shamed in red) */
  tone?: 'auto' | 'neutral' | 'positive' | 'alert'
  /** Animate from 0 on first render (display only) */
  countUp?: boolean
  className?: string
}

export function MoneyText({ value, variant = 'default', tone = 'neutral', countUp = false, className, ...format }: MoneyTextProps) {
  const { lang } = useLanguage()
  const reduced = useReducedMotion()
  const animated = useCountUp(Number(value), countUp && !reduced)
  const n = Number(value)
  const resolvedTone = tone === 'auto' ? (n > 0 ? 'positive' : 'neutral') : tone

  return (
    <span
      className={clsx(
        'money',
        variant === 'hero' && 'money--hero',
        resolvedTone === 'positive' && 'money--positive',
        resolvedTone === 'alert' && 'money--alert',
        className,
      )}
    >
      {formatMoney(animated ?? value, lang, format)}
    </span>
  )
}

/** Counts up to `target` over ~700ms. Returns null when not animating. */
function useCountUp(target: number, enabled: boolean): number | null {
  const [current, setCurrent] = useState<number | null>(enabled ? 0 : null)

  useEffect(() => {
    if (!enabled || Number.isNaN(target)) return
    const duration = 700
    let start: number | null = null
    let frame = requestAnimationFrame(function step(ts) {
      start ??= ts
      const p = Math.min(1, (ts - start) / duration)
      const eased = 1 - Math.pow(1 - p, 3)
      setCurrent(p === 1 ? null : target * eased)
      if (p < 1) frame = requestAnimationFrame(step)
    })
    return () => cancelAnimationFrame(frame)
  }, [target, enabled])

  return enabled ? current : null
}
