import { createContext, useContext } from 'react'
import type { Evidence } from '../../types'

export interface EvidenceRequest {
  evidence: Evidence
  /** What is being explained, e.g. "Safe to spend" */
  subject?: string
}

export const EvidenceContext = createContext<((req: EvidenceRequest) => void) | null>(null)

/** Returns a function that opens the Evidence drawer. */
export function useOpenEvidence() {
  const open = useContext(EvidenceContext)
  if (!open) throw new Error('useOpenEvidence must be used inside <EvidenceProvider>')
  return open
}
