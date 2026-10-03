import type { CSSProperties, ReactNode } from 'react'

const clamp = (v: number) => Math.min(1, Math.max(0, v))

interface ProgressBarProps {
  /** 0–1 */
  value: number
  label: string
  color?: string
}

export function ProgressBar({ value, label, color }: ProgressBarProps) {
  const pct = Math.round(clamp(value) * 100)
  return (
    <div
      className="progress"
      role="progressbar"
      aria-label={label}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={pct}
      style={color ? ({ '--progress-color': color } as CSSProperties) : undefined}
    >
      <div className="progress__fill" style={{ width: `${pct}%` }} />
    </div>
  )
}

interface ProgressRingProps {
  /** 0–1 */
  value: number
  label: string
  size?: number
  stroke?: number
  color?: string
  children?: ReactNode
}

export function ProgressRing({ value, label, size = 64, stroke = 7, color, children }: ProgressRingProps) {
  const r = (size - stroke) / 2
  const c = 2 * Math.PI * r
  const pct = Math.round(clamp(value) * 100)
  return (
    <div
      className="ring"
      role="progressbar"
      aria-label={label}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={pct}
      style={{ width: size, height: size, ...(color ? { '--ring-color': color } : {}) } as CSSProperties}
    >
      <svg width={size} height={size} aria-hidden>
        <circle className="ring__track" cx={size / 2} cy={size / 2} r={r} fill="none" strokeWidth={stroke} />
        <circle
          className="ring__value"
          cx={size / 2}
          cy={size / 2}
          r={r}
          fill="none"
          strokeWidth={stroke}
          strokeDasharray={c}
          strokeDashoffset={c * (1 - clamp(value))}
        />
      </svg>
      <span className="ring__label">{children ?? `${pct}%`}</span>
    </div>
  )
}
