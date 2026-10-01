import { Button } from "../components/ui/button";
import { AlertDialog } from "@base-ui/react/alert-dialog";
import { Progress } from "../components/ui/progress";
import { ui } from "./uiMessages";
import { useCallback, useEffect, useRef, useState } from "react";
import {
  Link,
  useBeforeUnload,
  useBlocker,
  useNavigate,
  useParams,
} from "react-router-dom";
import { ApiError, getOwnerId } from "../api";
import { read, session as loadSession, mutateSession } from "./api";
import type { Draft, Item, Knowledge, Session } from "./contracts";
import { AnswerControl, MaterialBlocks } from "./Content";
import { SessionCommands } from "./SessionCommands";
import { ErrorNotice, MaterialReader, Panel } from "./LearningPage";
import { messages } from "./messages";
import { initialAnswer } from "./answer";
import { useLoad } from "./useLoad";
import { EvidenceHistory } from "./EvidenceHistory";

export function SessionPage({ summary = false }: { summary?: boolean }) {
  const { id } = useParams();
  const state = useLoad(() => loadSession(id!), id!);
  if (!state.data)
    return (
      <Panel>
        <ErrorNotice error={state.error} />
        <p>{ui.loadingSavedSession}</p>
      </Panel>
    );
  return <SessionPlayer key={id} initial={state.data} summary={summary} />;
}
function SessionPlayer({
  initial,
  summary,
}: {
  initial: Session;
  summary: boolean;
}) {
  const navigate = useNavigate();
  const [session, setSession] = useState(initial);
  const commands = useRef<SessionCommands | null>(null);
  if (!commands.current) {
    const owner = getOwnerId();
    commands.current = new SessionCommands(initial, (c) => {
      if (getOwnerId() !== owner)
        return Promise.reject(
          new ApiError(ui.ownerChangedPendingWorkWasNotSent, 409),
        );
      return mutateSession(initial.id, c.action, c.body, c.version, c.method);
    });
  }
  const current = session.items[session.position];
  const [draft, setDraft] = useState<Draft>(() =>
    current
      ? { answer: initialAnswer(current), note: current.draft.note }
      : { answer: null, note: "" },
  );
  const draftRef = useRef(draft);
  const generation = useRef(0);
  const acknowledged = useRef(0);
  const itemRef = useRef<string | undefined>(current?.id);
  const [saveStatus, setSaveStatus] = useState<string>(messages.saved);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [conflict, setConflict] = useState(false);
  const [retryMode, setRetryMode] = useState(false);
  const [inspected, setInspected] = useState<Session | null>(null);
  const navigationAttempt = useRef<string | null>(null);
  const [help, setHelp] = useState<{
    hints: string[];
    reference: string | null;
    answer: string | null;
  } | null>(null);
  const [helpOpen, setHelpOpen] = useState(false);
  const helpTrigger = useRef<HTMLButtonElement | null>(null);
  const feedbackRef = useRef<HTMLElement | null>(null);
  const pendingGeneration = useRef<number | null>(null);
  const alive = useRef(true);
  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
    };
  }, []);
  const apply = useCallback((value: Session) => {
    if (!alive.current) return;
    setSession(value);
    const next = value.items[value.position];
    if (itemRef.current !== next?.id) {
      itemRef.current = next?.id;
      generation.current = 0;
      acknowledged.current = 0;
      const nextDraft = next
        ? { answer: initialAnswer(next), note: next.draft.note }
        : { answer: null, note: "" };
      draftRef.current = nextDraft;
      setDraft(nextDraft);
      setSaveStatus(messages.saved);
      setHelp(null);
      setHelpOpen(false);
      setRetryMode(false);
    }
  }, []);
  function fail(e: unknown) {
    if (!alive.current) return;
    setError((e as Error).message);
    setSaveStatus(messages.failed);
    if (e instanceof ApiError && e.status === 412) {
      setConflict(true);
      setError(messages.conflict);
    }
  }
  const flush = useCallback(async () => {
    if (generation.current === acknowledged.current || !itemRef.current) return;
    const acceptedGeneration = generation.current;
    const itemId = itemRef.current;
    const value = structuredClone(draftRef.current);
    setSaveStatus(messages.saving);
    pendingGeneration.current = acceptedGeneration;
    try {
      const result = await commands.current!.execute(
        `items/${itemId}/draft`,
        value as unknown as Record<string, unknown>,
        "PUT",
      );
      if (!alive.current) return;
      acknowledged.current = Math.max(acknowledged.current, acceptedGeneration);
      pendingGeneration.current = null;
      apply(result.session);
      setError("");
      setSaveStatus(
        generation.current === acknowledged.current
          ? messages.saved
          : messages.unsaved,
      );
    } catch (e) {
      fail(e);
      throw e;
    }
  }, [apply]);
  useEffect(() => {
    if (
      generation.current === acknowledged.current ||
      conflict ||
      busy ||
      retryMode ||
      current?.outcome ||
      commands.current?.pending
    )
      return;
    const timer = window.setTimeout(() => {
      void flush().catch(() => undefined);
    }, 750);
    return () => clearTimeout(timer);
  }, [
    draft,
    conflict,
    busy,
    retryMode,
    current?.outcome,
    session.versionToken,
    flush,
  ]);
  useBeforeUnload(
    useCallback((e) => {
      if (
        generation.current !== acknowledged.current ||
        commands.current?.pending
      )
        e.preventDefault();
    }, []),
  );
  const blocker = useBlocker(
    () =>
      generation.current !== acknowledged.current ||
      !!commands.current?.pending,
  );
  useEffect(() => {
    if (blocker.state !== "blocked") {
      navigationAttempt.current = null;
      return;
    }
    if (
      conflict ||
      commands.current?.pending ||
      navigationAttempt.current === blocker.location.key
    )
      return;
    navigationAttempt.current = blocker.location.key;
    void flush()
      .then(() => blocker.proceed())
      .catch(() => undefined);
  }, [blocker, conflict, flush]);
  useEffect(() => {
    if (
      !current?.id ||
      (!current.revealed && !current.hintUsed && !current.referenceUsed)
    )
      return;
    let keep = true;
    read<{ hints: string[]; reference: string | null; answer: string | null }>(
      `learning/sessions/${session.id}/items/${current.id}/assistance`,
    )
      .then((value) => {
        if (keep) setHelp(value);
      })
      .catch((e) => {
        if (keep) setError(e.message);
      });
    return () => {
      keep = false;
    };
  }, [
    session.id,
    current?.id,
    current?.revealed,
    current?.hintUsed,
    current?.referenceUsed,
  ]);
  function edit(value: Draft) {
    draftRef.current = value;
    generation.current++;
    setDraft(value);
    setSaveStatus(messages.unsaved);
  }
  async function action(
    action: string,
    body: Record<string, unknown> = {},
    itemAction = false,
  ) {
    setBusy(true);
    setError("");
    try {
      if (!retryMode) await flush();
      const result = await commands.current!.execute(
        itemAction ? `items/${current.id}/${action}` : action,
        body,
      );
      if (!alive.current) return;
      apply(result.session);
      setSaveStatus(messages.saved);
      if (itemAction && action !== "assistance") {
        acknowledged.current = generation.current;
        setRetryMode(false);
        requestAnimationFrame(() => feedbackRef.current?.focus());
      }
      if (
        action === "end" ||
        (action === "advance" &&
          result.session.position >= result.session.actualCount)
      )
        navigate(`/learn/sessions/${session.id}/summary`);
    } catch (e) {
      fail(e);
    } finally {
      if (alive.current) setBusy(false);
    }
  }
  async function retryPending() {
    setBusy(true);
    try {
      const result = await commands.current!.retry();
      apply(result.session);
      if (pendingGeneration.current !== null) {
        acknowledged.current = pendingGeneration.current;
        pendingGeneration.current = null;
      }
      setError("");
      setSaveStatus(
        generation.current === acknowledged.current
          ? messages.saved
          : messages.unsaved,
      );
    } catch (e) {
      fail(e);
    } finally {
      setBusy(false);
    }
  }
  async function inspect() {
    try {
      setInspected(await loadSession(session.id));
      setError(ui.serverStateLoadedBelowYourLocalInputIsPreserved);
    } catch (e) {
      fail(e);
    }
  }
  function recover(keepInput: boolean) {
    if (!inspected) return;
    commands.current!.current = inspected;
    commands.current!.pending = null;
    if (!keepInput) {
      itemRef.current = undefined;
      apply(inspected);
    } else {
      setSession(inspected);
      setSaveStatus(messages.unsaved);
    }
    setInspected(null);
    setConflict(false);
    setError("");
  }
  const terminal = !!current?.outcome && !retryMode;
  const finished =
    summary ||
    session.state === "EndedEarly" ||
    session.position >= session.actualCount;
  const disabled = busy || conflict || !!commands.current.pending;
  return (
    <Panel>
      <ErrorNotice error={error} />
      <p role="status" className="save-status">
        {saveStatus}
      </p>
      {commands.current.pending && (
        <Button variant="secondary" disabled={busy} onClick={() => void retryPending()}>
          {ui.retryPendingRequest}
        </Button>
      )}
      {conflict && (
        <div className="learning-error">
          <Button variant="secondary" onClick={() => void inspect()}>
            {ui.inspectCurrentServerSession}
          </Button>
          <Button variant="secondary"
            onClick={() =>
              void navigator.clipboard.writeText(JSON.stringify(draft, null, 2))
            }
          >
            {ui.copyUnsavedInput}
          </Button>
        </div>
      )}
      <AlertDialog.Root open={blocker.state === "blocked"} onOpenChange={(open) => { if (!open && blocker.state === "blocked") blocker.reset() }}>
        <AlertDialog.Portal>
          <AlertDialog.Backdrop className="confirmation-backdrop" />
          <AlertDialog.Popup className="confirmation-dialog">
          <AlertDialog.Title>{ui.unsavedWork}</AlertDialog.Title>
          <AlertDialog.Description>{messages.disconnected}</AlertDialog.Description>
          <div className="learning-actions">
          <Button variant="secondary" onClick={() => blocker.reset?.()}>{ui.stayAndRetry}</Button>
          <Button variant="secondary" onClick={() => blocker.proceed?.()}>
            {ui.leaveUnsavedChanges}
          </Button>
          </div>
          </AlertDialog.Popup>
        </AlertDialog.Portal>
      </AlertDialog.Root>
      {inspected && (
        <aside className="assistance-panel">
          <h3>{ui.currentServerState}</h3>
          <p>
            {inspected.state}
            {ui.item}
            {Math.min(inspected.position + 1, inspected.actualCount)}
            {ui.of}
            {inspected.actualCount}
          </p>
          <pre>
            {JSON.stringify(
              inspected.items[inspected.position]?.draft,
              null,
              2,
            )}
          </pre>
          <Button variant="secondary" onClick={() => recover(false)}>
            {ui.useSavedServerState}
          </Button>
          {inspected.state === "Active" &&
            inspected.items[inspected.position]?.id === itemRef.current &&
            !inspected.items[inspected.position]?.outcome && (
              <Button variant="secondary" onClick={() => recover(true)}>
                {ui.keepMyInputAndSaveThisItem}
              </Button>
            )}
        </aside>
      )}
      {finished ? (
        <Summary session={session} />
      ) : (
        <>
          <div className="learning-row">
            <span>
              {ui.item2}
              {session.position + 1}
              {ui.of}
              {session.actualCount} · {current?.type}
            </span>
            <Link to={`/learn/topics/${current?.conceptId}`}>
              {ui.topicWorkspace}
            </Link>
          </div>
          <Progress value={session.position} max={session.actualCount} aria-label="Activities visited" className="session-progress" />
          {session.actualCount < session.requestedCount && (
            <p>
              {ui.requested}
              {session.requestedCount}
              {ui.thisSessionContains}
              {session.actualCount}
              {ui.availableFamilies}
            </p>
          )}
          {session.state === "Paused" ? (
            <>
              <h2>{ui.pausedWithYourWorkSaved}</h2>
              <Button variant="default"
                className="primary-button"
                disabled={disabled}
                onClick={() => void action("resume")}
              >
                {ui.resumeSession}
              </Button>
              <Button variant="secondary" disabled={disabled} onClick={() => void action("end")}>
                {ui.endEarly}
              </Button>
            </>
          ) : (
            current && (
              <>
                <small>
                  {ui.contentLanguage}
                  {current.locale}
                </small>
                <MaterialBlocks blocks={current.prompt} />
                <AnswerControl
                  item={current}
                  answer={draft.answer ?? initialAnswer(current)}
                  onChange={(answer) => edit({ ...draft, answer })}
                  disabled={disabled || terminal}
                />
                <label>
                  {messages.notes}
                  <textarea
                    value={draft.note}
                    maxLength={10000}
                    disabled={terminal || busy}
                    onChange={(e) => edit({ ...draft, note: e.target.value })}
                  />
                </label>
                {!terminal && (
                  <div className="learning-actions">
                    {current.type === "Flashcard" ? (
                      current.revealed ? (
                        <>
                          {help?.answer && (
                            <blockquote>{help.answer}</blockquote>
                          )}
                          {["Again", "Partly", "Recalled"].map((rating) => (
                            <Button variant="secondary"
                              disabled={disabled}
                              key={rating}
                              onClick={() =>
                                void action(
                                  "self-reviews",
                                  { rating, note: draft.note },
                                  true,
                                )
                              }
                            >
                              {rating}
                            </Button>
                          ))}
                        </>
                      ) : (
                        <Button variant="secondary"
                          disabled={disabled}
                          onClick={() =>
                            void action(
                              "assistance",
                              { assistance: "Reveal" },
                              true,
                            )
                          }
                        >
                          {ui.revealAnswer}
                        </Button>
                      )
                    ) : (
                      <Button variant="default"
                        className="primary-button"
                        disabled={disabled}
                        onClick={() =>
                          void action(
                            "attempts",
                            {
                              answer: draft.answer,
                              note: draft.note,
                              previousAttemptId: retryMode
                                ? current.feedback.at(-1)?.attemptId
                                : null,
                            },
                            true,
                          )
                        }
                      >
                        {retryMode
                          ? ui.submitAnswerAwareRetry
                          : ui.submitAnswer}
                      </Button>
                    )}
                    {!retryMode && (
                      <Button variant="secondary"
                        disabled={disabled}
                        onClick={() => void action("skip", {}, true)}
                      >
                        {ui.skip}
                      </Button>
                    )}
                  </div>
                )}
                {!terminal && !retryMode && (
                  <div className="learning-actions">
                    {current.hintCount > 0 && (
                      <Button variant="secondary"
                        disabled={disabled}
                        onClick={(e) => {
                          helpTrigger.current = e.currentTarget;
                          setHelpOpen(true);
                          void action(
                            "assistance",
                            { assistance: "Hint" },
                            true,
                          );
                        }}
                      >
                        {ui.hint}
                      </Button>
                    )}
                    {current.materialVersionId && (
                      <Button variant="secondary"
                        disabled={disabled}
                        onClick={(e) => {
                          helpTrigger.current = e.currentTarget;
                          setHelpOpen(true);
                          void action(
                            "assistance",
                            { assistance: "Reference" },
                            true,
                          );
                        }}
                      >
                        {ui.reference}
                      </Button>
                    )}
                  </div>
                )}
                {helpOpen && help && (
                  <aside
                    className="assistance-panel"
                    aria-label={ui.savedAssistance}
                  >
                    <Button variant="secondary"
                      onClick={() => {
                        setHelpOpen(false);
                        helpTrigger.current?.focus();
                      }}
                    >
                      {ui.closeAssistance}
                    </Button>
                    {help.hints.map((hint, i) => (
                      <p key={i}>{hint}</p>
                    ))}
                    {help.reference && <MaterialReader id={help.reference} />}
                  </aside>
                )}
                {terminal && (
                  <section
                    ref={feedbackRef}
                    tabIndex={-1}
                    className="feedback-panel"
                    aria-label={ui.savedFeedback}
                  >
                    {current.outcome === "Skipped" ? (
                      <h3>{ui.skippedUnassessed}</h3>
                    ) : (
                      current.feedback.map((f) => (
                        <div id={f.attemptId} key={f.attemptId}>
                          <h3>
                            {f.score === null
                              ? messages.selfReview
                              : f.score === 1
                                ? ui.correct
                                : ui.incorrect}
                          </h3>
                          <p>{f.explanation}</p>
                          <blockquote>{f.referenceAnswer}</blockquote>
                          <p>
                            {f.assisted
                              ? ui.assistanceUsed
                              : ui.noProductAssistanceRecorded}
                            {f.answerAware &&
                              ui.answerAwareRetryExcludedFromAssessment}
                          </p>
                          {f.note && (
                            <p>
                              {ui.yourNote}
                              {f.note}
                            </p>
                          )}
                        </div>
                      ))
                    )}
                    {current.outcome === "SelfReviewed" && (
                      <Cooldown
                        conceptId={current.conceptId}
                        revision={session.versionToken}
                      />
                    )}
                    <Button variant="default"
                      className="primary-button"
                      disabled={disabled}
                      onClick={() => void action("advance")}
                    >
                      {ui.continue}
                    </Button>
                    {current.type !== "Flashcard" &&
                      current.outcome === "Answered" && (
                        <Button variant="secondary"
                          disabled={disabled}
                          onClick={() => {
                            setRetryMode(true);
                            setError(
                              ui.practiceAfterSeeingTheAnswerTheOriginalAssessmentStays,
                            );
                          }}
                        >
                          {ui.practiceAgainAfterSeeingAnswer}
                        </Button>
                      )}
                  </section>
                )}
                {session.state === "Active" && (
                  <div className="learning-actions session-footer">
                    <Button variant="secondary"
                      disabled={disabled}
                      onClick={() => void action("pause")}
                    >
                      {ui.pause}
                    </Button>
                    <Button variant="secondary"
                      disabled={disabled}
                      onClick={() => void action("end")}
                    >
                      {ui.endEarly}
                    </Button>
                    <small>{messages.disconnected}</small>
                  </div>
                )}
              </>
            )
          )}
        </>
      )}
    </Panel>
  );
}
function Cooldown({
  conceptId,
  revision,
}: {
  conceptId: string;
  revision: string;
}) {
  const state = useLoad(
    () => read<Knowledge>(`evidence/knowledge/${conceptId}`),
    conceptId + revision,
  );
  return state.data?.nextPositiveReviewAt ? (
    <p>
      {ui.reviewSavedAnotherPositiveSelfReviewContributionIsAvailable}
      {new Date(state.data.nextPositiveReviewAt).toLocaleString()}.
    </p>
  ) : null;
}
function Summary({ session }: { session: Session }) {
  const first = session.items.flatMap((i) =>
    i.feedback.filter((f) => !f.previousAttemptId),
  );
  const objective = first.filter((f) => f.score !== null);
  return (
    <>
      <h2>
        {session.state === "Completed" ? ui.sessionComplete : ui.yourSavedWork}
      </h2>
      <p>
        {objective.filter((f) => f.score === 1).length}
        {ui.of}
        {objective.length}
        {ui.objectiveAnswersCorrect}
        {first.filter((f) => f.score === null).length}
        {ui.selfReviews}
        {first.filter((f) => f.assisted).length}
        {ui.assistedAnswers}
      </p>
      <p>
        {session.items.filter((i) => i.outcome === "Skipped").length}
        {ui.skipped}
        {session.items.filter((i) => !i.outcome).length}
        {ui.unvisitedAndUnassessed}
      </p>
      {session.items.map((item) => (
        <SavedItem key={item.id} item={item} />
      ))}
      <div className="learning-actions">
        <Link className="ink-button" to={session.returnContext}>
          {ui.returnToLearningContext}
        </Link>
        <Link to="/learn/session-setup">{ui.startAnotherSession}</Link>
      </div>
    </>
  );
}
function SavedItem({ item }: { item: Item }) {
  return (
    <details className="summary-item">
      <summary>
        {ui.item2}
        {item.position + 1} · {item.type} · {item.outcome ?? ui.unvisited}
      </summary>
      <Link to={`/learn/topics/${item.conceptId}?section=activity`}>
        {ui.topicActivity}
      </Link>
      <MaterialBlocks blocks={item.prompt} />
      {item.feedback.map((f) => (
        <div id={f.attemptId} key={f.attemptId}>
          <h4>
            {f.score === null
              ? f.selfReview
              : f.score === 1
                ? ui.correct
                : ui.incorrect}
            {f.answerAware && ui.answerAwareRetry}
          </h4>
          <p>{f.explanation}</p>
          <pre>{JSON.stringify(f.answer, null, 2)}</pre>
          <p>{f.note}</p>
          <small>
            {f.assisted ? ui.assisted : ui.noProductAssistanceRecorded} ·{" "}
            {new Date(f.receivedAt).toLocaleString()}
          </small>
          <EvidenceHistory conceptId={item.conceptId} attemptId={f.attemptId} />
        </div>
      ))}
    </details>
  );
}
