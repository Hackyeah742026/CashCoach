import { LoaderCircle } from 'lucide-react'
import { Navigate, Outlet } from 'react-router'
import { ErrorState } from '../components/ui/States'
import { useSettings } from '../hooks/useSettings'

/** Sends users without imported data to onboarding. */
export function RequireOnboarded() {
  const { data, isLoading, isError, refetch } = useSettings()

  if (isLoading) {
    return (
      <div className="page-loader">
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
  if (!data.onboarded) return <Navigate to="/onboarding" replace />
  return <Outlet />
}
