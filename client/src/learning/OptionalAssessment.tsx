import { useEffect, useRef, useState } from "react";
import { Button } from "../components/ui/button";
import { all, read, send } from "./api";
import { assessmentUi as ui } from "./uiMessages";

type Criterion = { id: string; judgment: string; explanation: string; answerQuote: string | null; referenceIds: string[] };
type Job = {
  id: string; state: string; versionToken: string; failureCode: string | null; notBefore: string | null;
  feedback: null | { revision: number; scope: string; origin: string; templateVersion: string; contractVersion: string;
    output: { contextSufficiency: string; criteria: Criterion[]; missingAssumptions: string[]; nextActions: string[] };
    provenance: { actualModel: string | null; actualProvider: string | null } };
};
type Preview = { available: boolean; reason: string; digest: string | null; disclosure: null | {
  systemPrompt: string; input: unknown; schema: string; model: string; provider: string; origin: string;
  routerPrivacyPolicy: string; providerPrivacyPolicy: string; routerPrivacyReviewedAt: string; providerPrivacyReviewedAt: string;
} };
const active = (j: Job) => ["Queued", "Running", "Retry"].includes(j.state);

export function OptionalAssessment({ attemptId }: { attemptId: string }) {
  const [open, setOpen] = useState(false);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [accepted, setAccepted] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const operation = useRef<{ id: string; digest: string } | null>(null);
  const cancellation = useRef<Record<string, string>>({});
  const pending = jobs.some(active);
  useEffect(() => {
    if (!open || !pending) return;
    let alive = true;
    const timer = setInterval(() => {
      void all<Job>(`learning/attempts/${attemptId}/assessments`).then((values) => {
        if (alive) setJobs(values);
      }).catch((e: Error) => { if (alive) setError(e.message); });
    }, 5000);
    return () => { alive = false; clearInterval(timer); };
  }, [attemptId, open, pending]);
  async function inspect() {
    setOpen(true); setBusy(true); setError(""); setAccepted(false);
    try {
      const [p, history] = await Promise.all([
        send<Preview>(`learning/attempts/${attemptId}/assessment-preview`, {}),
        all<Job>(`learning/attempts/${attemptId}/assessments`),
      ]);
      setPreview(p); setJobs(history);
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  async function request() {
    if (!accepted || !preview?.available || !preview.digest) return;
    setBusy(true); setError("");
    if (operation.current?.digest !== preview.digest) operation.current = { id: crypto.randomUUID(), digest: preview.digest };
    try {
      await send<Job>(`learning/attempts/${attemptId}/assessments`, { operationId: operation.current.id, disclosureDigest: operation.current.digest });
      setJobs(await all<Job>(`learning/attempts/${attemptId}/assessments`));
      operation.current = null; setAccepted(false);
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  async function cancel(job: Job) {
    setBusy(true); setError("");
    cancellation.current[job.id] ??= crypto.randomUUID();
    try {
      await send<Job>(`learning/assessments/${job.id}/cancel`, { operationId: cancellation.current[job.id] }, job.versionToken);
      setJobs(await all<Job>(`learning/attempts/${attemptId}/assessments`));
    } catch (e) {
      setError((e as Error).message);
      try { const current = await read<Job>(`learning/assessments/${job.id}`); setJobs((values) => values.map((j) => j.id === job.id ? current : j)); }
      catch { /* Keep the last saved status and the retry operation ID. */ }
    } finally { setBusy(false); }
  }
  return <div className="optional-assessment">
    <Button variant="secondary" disabled={busy} onClick={() => void inspect()}>{ui.open}</Button>
    {open && <section aria-label={ui.region} className="learning-card">
      <h4>{ui.title}</h4>
      <p>{ui.intro}</p>
      {error && <p role="alert">{error}</p>}
      {busy && <p role="status">{ui.loading}</p>}
      {preview && <p>{preview.reason}</p>}
      {preview?.available && preview.disclosure && <>
        <h5>{ui.review}</h5>
        <p>{ui.recipients(preview.disclosure.provider, preview.disclosure.model)}</p>
        {preview.disclosure.origin === "Fixture" && <p><strong>{ui.fixtureNotice}</strong></p>}
        <p><a href={preview.disclosure.routerPrivacyPolicy} target="_blank" rel="noreferrer">{ui.routerPrivacy}</a> · <a href={preview.disclosure.providerPrivacyPolicy} target="_blank" rel="noreferrer">{ui.providerPrivacy}</a></p>
        <details><summary>{ui.details}</summary>
          <h5>{ui.prompt}</h5><pre>{preview.disclosure.systemPrompt}</pre>
          <h5>{ui.input}</h5><pre>{JSON.stringify(preview.disclosure.input, null, 2)}</pre>
          <h5>{ui.schema}</h5><pre>{preview.disclosure.schema}</pre>
        </details>
        <label className="learning-check"><input type="checkbox" checked={accepted} onChange={(e) => setAccepted(e.target.checked)} disabled={busy || pending} />{ui.accept}</label>
        <Button className="primary-button" disabled={busy || pending || !accepted} onClick={() => void request()}>{ui.request}</Button>
      </>}
      {jobs.map((job) => <article key={job.id}>
        <h5>{ui.status}: {ui.states[job.state] ?? ui.unavailable}</h5>
        {job.notBefore && <p>{ui.nextCheck}: {new Date(job.notBefore).toLocaleString()}.</p>}
        {job.failureCode && <p>{ui.failures[job.failureCode] ?? ui.unavailable} {ui.failure}</p>}
        {active(job) && <><p>{ui.leave}</p><Button variant="secondary" disabled={busy} onClick={() => void cancel(job)}>{ui.cancel}</Button></>}
        {job.feedback && <>
          <p><strong>{job.feedback.origin === "Fixture" ? ui.fixtureFeedback : ui.providerFeedback}</strong> · {ui.revision} {job.feedback.revision}</p>
          <p>{ui.context}: {job.feedback.output.contextSufficiency}. {ui.separate}</p>
          {job.feedback.output.criteria.map((c) => <div key={c.id}><h5>{c.id === "reasoning" ? ui.reasoning : ui.communication}: {c.judgment.replaceAll("_", " ")}</h5><p>{c.explanation}</p>{c.answerQuote && <blockquote>{c.answerQuote}</blockquote>}</div>)}
          {job.feedback.output.missingAssumptions.length > 0 && <><h5>{ui.assumptions}</h5><ul>{job.feedback.output.missingAssumptions.map((s, i) => <li key={i}>{s}</li>)}</ul></>}
          {job.feedback.output.nextActions.length > 0 && <><h5>{ui.nextActions}</h5><ul>{job.feedback.output.nextActions.map((s, i) => <li key={i}>{s}</li>)}</ul></>}
          <p>{ui.source}: {job.feedback.provenance.actualProvider ?? job.feedback.origin} · {job.feedback.provenance.actualModel ?? "fixture"}. {ui.template} {job.feedback.templateVersion}; {ui.contract} {job.feedback.contractVersion}.</p>
        </>}
      </article>)}
      <Button variant="ghost" disabled={busy} onClick={() => setOpen(false)}>{ui.close}</Button>
    </section>}
  </div>;
}
