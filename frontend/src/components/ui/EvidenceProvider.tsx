import { useCallback, useState, type ReactNode } from 'react'
import { EvidenceDrawer } from './EvidenceDrawer'
import { EvidenceContext, type EvidenceRequest } from './evidenceContext'

/** One Evidence drawer for the whole app; any component can open it via useOpenEvidence(). */
export function EvidenceProvider({ children }: { children: ReactNode }) {
  const [request, setRequest] = useState<EvidenceRequest | null>(null)
  const close = useCallback(() => setRequest(null), [])

  return (
    <EvidenceContext.Provider value={setRequest}>
      {children}
      <EvidenceDrawer request={request} onClose={close} />
    </EvidenceContext.Provider>
  )
}
