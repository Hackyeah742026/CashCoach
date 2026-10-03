import { useT } from '../../i18n/context'
import type { Narrative } from '../../types'
import { AiText } from '../ui/AiText'
import { EvidenceButton } from '../ui/EvidenceButton'

/** AI summary of the month. Every bullet links to the transactions behind it. */
export function InsightCard({ narrative }: { narrative: Narrative }) {
  const t = useT()
  return (
    <section className="card" aria-labelledby="insight-title">
      <AiText factCheck={narrative.factCheck} aiGenerated={narrative.aiGenerated} title={t.home.insightTitle}>
        <h2 id="insight-title" className="insight-headline">
          {narrative.headline}
        </h2>
        <ul className="insight-bullets">
          {narrative.bullets.map((b) => (
            <li key={b.text}>
              {b.text}{' '}
              {b.transactionIds.length > 0 && (
                <EvidenceButton evidence={{ transactionIds: b.transactionIds, factKeys: b.factKeys }} subject={b.text} />
              )}
            </li>
          ))}
        </ul>
      </AiText>
    </section>
  )
}
