import { LoaderCircle } from 'lucide-react'
import { useCallback } from 'react'
import { useNavigate, useParams } from 'react-router'
import { StoryPlayer } from '../components/wrapped/StoryPlayer'
import '../components/wrapped/wrapped.css'
import { ErrorState } from '../components/ui/States'
import { useWrapped } from '../hooks/useWrapped'

/** Full-screen story for one month (route outside the tab shell). */
export function WrappedStory() {
  const { month = '' } = useParams()
  const navigate = useNavigate()
  const { data, isLoading, isError, refetch } = useWrapped(month)
  const close = useCallback(() => navigate('/wrapped'), [navigate])
  const seeHow = useCallback(() => navigate('/#savings-title'), [navigate])

  if (isLoading) {
    return (
      <div className="story">
        <LoaderCircle className="spinner" aria-label="Loading" />
      </div>
    )
  }
  if (isError || !data) {
    return (
      <div className="container container--narrow" style={{ paddingBlock: 'var(--space-16)' }}>
        <ErrorState onRetry={() => refetch()} />
      </div>
    )
  }
  return <StoryPlayer data={data} onClose={close} onSeeHow={seeHow} />
}
