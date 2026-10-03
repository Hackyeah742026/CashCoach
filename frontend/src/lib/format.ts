import type { IsoDate, Language, Money, MonthKey } from '../types'

// Display-only formatting. Money arrives as decimal strings; Number() here is
// only used to render them, never for calculations.

const locales: Record<Language, string> = { pl: 'pl-PL', en: 'en-GB' }

const moneyFormatters = new Map<string, Intl.NumberFormat>()

function moneyFormatter(lang: Language, fractionDigits: number) {
  const key = `${lang}-${fractionDigits}`
  let f = moneyFormatters.get(key)
  if (!f) {
    f = new Intl.NumberFormat(locales[lang], {
      style: 'currency',
      currency: 'PLN',
      minimumFractionDigits: fractionDigits,
      maximumFractionDigits: fractionDigits,
    })
    moneyFormatters.set(key, f)
  }
  return f
}

export interface MoneyFormatOptions {
  /** Show absolute value (e.g. "Spent 2 875 zł" instead of "-2 875 zł") */
  absolute?: boolean
  /** Prefix positive values with "+" */
  signed?: boolean
  /** Drop grosze ("2 875 zł") */
  whole?: boolean
}

export function formatMoney(value: Money | number, lang: Language, opts: MoneyFormatOptions = {}) {
  let n = typeof value === 'number' ? value : Number(value)
  if (Number.isNaN(n)) return '—'
  if (opts.absolute) n = Math.abs(n)
  const text = moneyFormatter(lang, opts.whole ? 0 : 2).format(n)
  return opts.signed && n > 0 ? `+${text}` : text
}

export function formatPercent(value: number, lang: Language, signed = true) {
  const text = new Intl.NumberFormat(locales[lang], { maximumFractionDigits: 0 }).format(Math.abs(value))
  if (!signed || value === 0) return `${text}%`
  return `${value > 0 ? '+' : '−'}${text}%`
}

function parseIsoDate(iso: IsoDate) {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(y, m - 1, d ?? 1)
}

export function formatDate(iso: IsoDate, lang: Language, style: 'short' | 'long' = 'short') {
  const opts: Intl.DateTimeFormatOptions =
    style === 'short' ? { day: '2-digit', month: '2-digit' } : { day: 'numeric', month: 'long', year: 'numeric' }
  return new Intl.DateTimeFormat(locales[lang], opts).format(parseIsoDate(iso))
}

export function formatMonth(month: MonthKey, lang: Language, withYear = true) {
  const text = new Intl.DateTimeFormat(locales[lang], {
    month: 'long',
    ...(withYear ? { year: 'numeric' } : {}),
  }).format(parseIsoDate(`${month}-01`))
  return text.charAt(0).toUpperCase() + text.slice(1)
}

/** Format a plain number with locale separators (counts, not money). */
export function formatNumber(value: number, lang: Language) {
  return new Intl.NumberFormat(locales[lang]).format(value)
}

/** Value for <input type="number"> from a Money string ("-12.50" → "12.5"). */
export function moneyToInput(value: Money) {
  const n = Math.abs(Number(value))
  return Number.isNaN(n) ? '' : String(n)
}

/** Normalize user input ("1 200,5") to a Money string ("1200.50"), or null if invalid. */
export function inputToMoney(raw: string): Money | null {
  const cleaned = raw.replace(/\s/g, '').replace(',', '.')
  if (cleaned === '' || !/^\d+(\.\d{0,2})?$/.test(cleaned)) return null
  return Number(cleaned).toFixed(2)
}
