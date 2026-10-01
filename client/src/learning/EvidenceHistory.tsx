import { Button } from "../components/ui/button";
import { ui } from "./uiMessages";
import { useState } from "react";
import { api } from "../api";
import type { EvidenceObservation, EvidenceStatus } from "../types";
import { all } from "./api";
import { useLoad } from "./useLoad";

export function EvidenceHistory({
  conceptId,
  attemptId,
}: {
  conceptId: string;
  attemptId?: string;
}) {
  const state = useLoad(
    () =>
      all<EvidenceObservation>(
        `evidence/observations?conceptId=${conceptId}&includeInactive=true`,
      ),
    conceptId + attemptId,
  );
  const [error, setError] = useState("");
  async function change(item: EvidenceObservation, status: EvidenceStatus) {
    try {
      await api.evidence.status(item, status);
      state.reload();
    } catch (e) {
      setError((e as Error).message);
    }
  }
  return (
    <section aria-label={ui.evidenceHistory}>
      <h3>{ui.evidenceHistory}</h3>
      {(state.error || error) && <p role="alert">{state.error || error}</p>}
      {state.data
        ?.filter((e) => !attemptId || e.sourceId === attemptId)
        .map((e) => (
          <article key={e.id} className="learning-row">
            <div>
              <strong>{e.status}</strong> · {e.signalKind} · {e.assistance}
              <p>
                {e.status === "Active"
                  ? ui.includedOnlyWhenEligibleUnderTheEvidencePolicy
                  : ui.excludedFromCurrentEstimatesAndCoverageTheOriginalAnswer}
              </p>
              <small>{new Date(e.observedAt).toLocaleString()}</small>
            </div>
            {e.status === "Active" && (
              <Button variant="secondary" onClick={() => void change(e, "Disputed")}>
                {ui.dispute}
              </Button>
            )}
            {e.status === "Disputed" && (
              <Button variant="secondary" onClick={() => void change(e, "Active")}>
                {ui.restoreActive}
              </Button>
            )}
            {(e.status === "Active" || e.status === "Disputed") && (
              <Button variant="secondary" onClick={() => void change(e, "Withdrawn")}>
                {ui.withdraw}
              </Button>
            )}
          </article>
        ))}
    </section>
  );
}
