type RecoveryOutcome = "acknowledged" | "replayed" | "conflict" | "failed";
const counts: Record<RecoveryOutcome, number> = {
  acknowledged: 0,
  replayed: 0,
  conflict: 0,
  failed: 0,
};
export function recordRecovery(outcome: RecoveryOutcome, elapsedMs: number) {
  counts[outcome]++;
  // Keep diagnostics local and bounded. Never include command payloads or owner identifiers.
  if (import.meta.env.DEV)
    console.debug("Learning command", {
      outcome,
      elapsedMs: Math.round(elapsedMs),
      count: counts[outcome],
    });
}
