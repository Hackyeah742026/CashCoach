import { Play, Star } from 'lucide-react'
import { Link } from 'react-router'
import '../components/wrapped/wrapped.css'
import { SkeletonCard } from '../components/ui/Skeleton'
import { EmptyState, ErrorState } from '../components/ui/States'
import { useWrappedMonths } from '../hooks/useWrapped'
import { useLanguage, useT } from '../i18n/context'
import { formatMonth } from '../lib/format'

/** Wrapped tab: one tile per month; tapping plays the story. */
export function WrappedPage() {
  const t = useT()
  const { lang } = useLanguage()
  const { data, isLoading, isError, refetch } = useWrappedMonths()

  return (
    <>
      <header className="page-header">
        <div>
          <h1>{t.wrapped.title}</h1>
          <p>{t.wrapped.subtitle}</p>
        </div>
      </header>

      {isError && <ErrorState onRetry={() => refetch()} />}

      <div className="grid-auto">
        {isLoading && [0, 1].map((i) => <SkeletonCard key={i} height={180} />)}
        {data?.map(({ month, isNew }) => (
          <Link key={month} to={`/wrapped/${month}`} className="wrapped-tile" aria-label={t.wrapped.play(formatMonth(month, lang))}>
            <div className="cluster" style={{ justifyContent: 'space-between' }}>
              <span className="wrapped-tile__kicker">★ Wrapped</span>
              {isNew && <span className="badge">{t.wrapped.isNew}</span>}
            </div>
            <div className="cluster" style={{ justifyContent: 'space-between', alignItems: 'flex-end' }}>
              <span className="wrapped-tile__month">{formatMonth(month, lang)}</span>
              <span className="wrapped-tile__play" aria-hidden>
                <Play size={22} />
              </span>
            </div>
          </Link>
        ))}
      </div>

      {data?.length === 0 && <EmptyState icon={Star} title={t.wrapped.title} body={t.wrapped.empty} />}
    </>
  )
}
