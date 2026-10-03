import { useTransactionsByIds } from '../../hooks/useTransactions'
import { useLanguage, useT } from '../../i18n/context'
import type { Dictionary } from '../../i18n/strings'
import { categoryMeta } from '../../lib/categories'
import { formatDate } from '../../lib/format'
import { AiBadge } from './AiText'
import type { EvidenceRequest } from './evidenceContext'
import { MoneyText } from './MoneyText'
import { Sheet } from './Sheet'
import { Skeleton } from './Skeleton'
import type { Language, Transaction } from '../../types'

interface EvidenceDrawerProps {
  request: EvidenceRequest | null
  onClose: () => void
}

/** One line about the linked transactions: when they happened and where most of them went. */
function describe(items: Transaction[], t: Dictionary, lang: Language) {
  if (items.length === 0) return null
  if (items.length === 1) return t.evidence.single(items[0].merchant, formatDate(items[0].date, lang))

  const dates = items.map((tx) => tx.date).sort()
  const range = t.evidence.range(formatDate(dates[0], lang), formatDate(dates[dates.length - 1], lang))
  const counts = new Map<string, number>()
  for (const tx of items) counts.set(tx.merchant, (counts.get(tx.merchant) ?? 0) + 1)
  const [merchant, n] = [...counts].sort((a, b) => b[1] - a[1])[0]

  if (counts.size === 1) return `${range}, ${t.evidence.allAt(merchant)}.`
  return n > 1 ? `${range}, ${t.evidence.mostOften(merchant, n)}.` : `${range}.`
}

/** "How do I know?" — shows the transactions and calculation behind a number or AI claim. */
export function EvidenceDrawer({ request, onClose }: EvidenceDrawerProps) {
  const t = useT()
  const { lang } = useLanguage()
  const evidence = request?.evidence
  const ids = evidence?.transactionIds ?? []
  const { data, isLoading } = useTransactionsByIds(ids, request !== null)

  return (
    <Sheet open={request !== null} onClose={onClose} title={request?.subject ?? t.evidence.title}>
      {data && data.items.length > 0 && <p className="text-sm text-muted">{describe(data.items, t, lang)}</p>}

      {evidence?.figures && evidence.figures.length > 0 && (
        <section>
          <h3 className="text-sm text-muted" style={{ marginBottom: 'var(--space-2)' }}>
            {t.evidence.figures}
          </h3>
          <div className="kv-list">
            {evidence.figures.map((f) => (
              <div key={f.label} className="kv-list__row">
                <span>{f.label}</span>
                <MoneyText value={f.amount} />
              </div>
            ))}
          </div>
        </section>
      )}

      <section>
        <h3 className="text-sm text-muted" style={{ marginBottom: 'var(--space-2)' }}>
          {t.evidence.transactions(ids.length)}
        </h3>
        {ids.length === 0 && <p className="text-sm text-subtle">{t.evidence.empty}</p>}
        {isLoading && (
          <div className="stack stack--sm">
            {[0, 1, 2].map((i) => (
              <Skeleton key={i} height={44} />
            ))}
          </div>
        )}
        {data && (
          <>
            <ul role="list" className="tx-list">
              {data.items.map((tx) => (
                <li key={tx.id} className="tx-row">
                  <span className="tx-row__emoji" aria-hidden>
                    {categoryMeta(tx.category).emoji}
                  </span>
                  <div className="tx-row__main">
                    <div className="tx-row__merchant">{tx.merchant}</div>
                    <div className="tx-row__meta">
                      {formatDate(tx.date, lang)} · {tx.description}
                      {tx.categorySource === 'ai' && (
                        <>
                          {' '}
                          <AiBadge />
                        </>
                      )}
                    </div>
                  </div>
                  <MoneyText value={tx.amount} tone="auto" />
                </li>
              ))}
            </ul>
            {data.count > 1 && (
              <div className="kv-list">
                <div className="kv-list__row kv-list__row--total">
                  <span>{t.evidence.total}</span>
                  <MoneyText value={data.total} />
                </div>
              </div>
            )}
          </>
        )}
      </section>
    </Sheet>
  )
}
