import type { ChatEvent } from '../types'

/**
 * Reads a Server-Sent Events stream from a fetch Response (EventSource can't POST).
 * Format per docs/API.md:  event: <name>\n data: <json>\n\n
 */
export async function* readSse(response: Response): AsyncGenerator<ChatEvent> {
  if (!response.body) throw new Error('Response has no body')
  const reader = response.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''

  try {
    while (true) {
      const { value, done } = await reader.read()
      if (done) break
      buffer += decoder.decode(value, { stream: true })

      let sep: number
      while ((sep = buffer.indexOf('\n\n')) !== -1) {
        const raw = buffer.slice(0, sep)
        buffer = buffer.slice(sep + 2)
        const event = parseEvent(raw)
        if (event) yield event
      }
    }
  } finally {
    reader.releaseLock()
  }
}

function parseEvent(raw: string): ChatEvent | null {
  let name = 'message'
  const dataLines: string[] = []
  for (const line of raw.split('\n')) {
    if (line.startsWith('event:')) name = line.slice(6).trim()
    else if (line.startsWith('data:')) dataLines.push(line.slice(5).trim())
  }
  if (dataLines.length === 0) return null

  let data: Record<string, unknown>
  try {
    data = JSON.parse(dataLines.join('\n'))
  } catch {
    return null
  }

  switch (name) {
    case 'delta':
      return { type: 'delta', text: String(data.text ?? '') }
    case 'tool':
      return { type: 'tool', name: String(data.name ?? '') }
    case 'evidence':
      return {
        type: 'evidence',
        evidence: {
          transactionIds: (data.transactionIds as string[]) ?? [],
          figures: data.figures as { label: string; amount: string }[] | undefined,
        },
      }
    case 'done':
      return { type: 'done', factCheck: (data.factCheck as 'passed' | 'failed' | 'fallback') ?? 'passed' }
    case 'error':
      return { type: 'error', message: String(data.message ?? 'Unknown error') }
    default:
      return null
  }
}
