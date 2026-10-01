export const episode = {
  title: 'Making background jobs retry-safe',
  project: 'Personal project',
  sourceVersion: 'snapshot-03',
  file: 'OrderWorker.cs',
  summary: 'A background worker can receive the same order twice after a timeout. This change introduces a per-order guard before creating an invoice.',
  question: 'What happens if the worker stops after creating the invoice, but before marking the order as processed?',
  code: [
    'public async Task Handle(OrderPlaced order)',
    '{',
    '    if (await processed.Exists(order.Id))',
    '        return;',
    '',
    '    await invoices.Create(order);',
    '    await processed.Mark(order.Id);',
    '}',
  ],
}

export type Draft = {
  answer: string
  unresolved: string
  assisted: boolean
  firstAttempt: string | null
  acknowledged: boolean
}

const emptyDraft: Draft = { answer: '', unresolved: '', assisted: false, firstAttempt: null, acknowledged: false }
const storageKey = 'sensei-ui-demo:reflection:snapshot-03:v1'

export function loadDraft(): Draft {
  try {
    const value = JSON.parse(localStorage.getItem(storageKey) ?? 'null') as Partial<Draft> | null
    if (!value || typeof value.answer !== 'string' || typeof value.unresolved !== 'string') return { ...emptyDraft }
    return {
      answer: value.answer, unresolved: value.unresolved,
      assisted: value.assisted === true,
      firstAttempt: typeof value.firstAttempt === 'string' ? value.firstAttempt : null,
      acknowledged: value.acknowledged === true,
    }
  } catch { return { ...emptyDraft } }
}

export function saveDraft(draft: Draft) {
  localStorage.setItem(storageKey, JSON.stringify(draft))
}

// Replaceable demo boundary. No answer is transmitted or assessed.
export async function requestFixtureFeedback(simulateFailure: boolean) {
  await new Promise((resolve) => window.setTimeout(resolve, 800))
  if (simulateFailure) throw new Error('The demo feedback provider timed out. Your answer is still here.')
  return 'Consider the boundary between invoice creation and recording completion. A retry can reach invoice creation again. What would make that side effect idempotent across worker restarts?'
}
