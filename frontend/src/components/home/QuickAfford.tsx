import { ArrowRight } from 'lucide-react'
import { useState, type SyntheticEvent } from 'react'
import { useT } from '../../i18n/context'
import { inputToMoney } from '../../lib/format'
import type { Money } from '../../types'
import { Field } from '../ui/Field'

export interface AffordQuestion {
  item: string
  price: Money
}

/** Small "Can I afford…?" form on Home; the answer opens in AffordSheet. */
export function QuickAfford({ onCheck }: { onCheck: (q: AffordQuestion) => void }) {
  const t = useT()
  const [item, setItem] = useState('')
  const [price, setPrice] = useState('')
  const [errors, setErrors] = useState<{ item?: string; price?: string }>({})

  function submit(e: SyntheticEvent) {
    e.preventDefault()
    const money = inputToMoney(price)
    const next = {
      item: item.trim() ? undefined : t.afford.invalidItem,
      price: money && Number(money) > 0 ? undefined : t.afford.invalidPrice,
    }
    setErrors(next)
    if (next.item || next.price || !money) return
    onCheck({ item: item.trim(), price: money })
  }

  return (
    <section className="card quick-afford" aria-labelledby="afford-title">
      <div className="section-title">
        <h2 id="afford-title">{t.home.affordTitle}</h2>
      </div>
      <form onSubmit={submit} noValidate>
        <Field
          label={t.home.affordItem}
          placeholder={t.home.affordItemPlaceholder}
          value={item}
          onChange={(e) => setItem(e.target.value)}
          error={errors.item}
          autoComplete="off"
        />
        <Field
          label={t.home.affordPrice}
          placeholder="1200"
          inputMode="decimal"
          value={price}
          onChange={(e) => setPrice(e.target.value)}
          error={errors.price}
        />
        <button type="submit" className="btn btn--primary">
          {t.home.affordCheck} <ArrowRight size={18} aria-hidden />
        </button>
      </form>
    </section>
  )
}
