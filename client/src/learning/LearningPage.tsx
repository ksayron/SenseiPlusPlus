import { Button } from "../components/ui/button";
import { ui } from "./uiMessages";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "../components/ui/tabs";
import { useEffect, useRef, useState, type ReactNode } from "react";
import {
  Link,
  NavLink,
  Route,
  Routes,
  useLocation,
  useNavigate,
  useParams,
  useSearchParams,
} from "react-router-dom";
import { getOwnerId } from "../api";
import { all, read, send } from "./api";
import type {
  Activity,
  Goal,
  Knowledge,
  Material,
  Mutation,
  Preview,
  Roadmap,
  RoadmapView,
  Session,
  Setup,
  Topic,
} from "./contracts";
import { exerciseTypes } from "./contracts";
import { MaterialBlocks } from "./Content";
import { SessionPage } from "./SessionPage";
import { messages } from "./messages";
import "./learning.css";
import { useLoad } from "./useLoad";
import { EvidenceHistory } from "./EvidenceHistory";

export function ErrorNotice({ error }: { error?: string }) {
  return error ? (
    <p role="alert" className="learning-error">
      {error}
    </p>
  ) : null;
}
export function LearningPage() {
  const [owner, setOwner] = useState(getOwnerId);
  useEffect(() => {
    const update = () => setOwner(getOwnerId());
    window.addEventListener("sensei-owner-change", update);
    window.addEventListener("storage", update);
    return () => {
      window.removeEventListener("sensei-owner-change", update);
      window.removeEventListener("storage", update);
    };
  }, []);
  return (
    <div className="learning" key={owner}>
      <nav className="learning-nav" aria-label={ui.learning}>
        <NavLink to="/learn" end>{ui.learningHome}</NavLink>
        <NavLink to="/learn/topics">{ui.exploreTopics}</NavLink>
        <NavLink to="/learn/session-setup">{ui.dailyPractice}</NavLink>
      </nav>
      <Routes>
        <Route path="/learn" element={<Home />} />
        <Route path="/learn/topics" element={<Topics />} />
        <Route path="/learn/topics/:id" element={<TopicWorkspace />} />
        <Route path="/learn/session-setup" element={<SessionSetup />} />
        <Route path="/learn/sessions/:id" element={<SessionPage />} />
        <Route
          path="/learn/sessions/:id/summary"
          element={<SessionPage summary />}
        />
        <Route path="/learn/roadmaps/:id" element={<RoadmapWorkspace />} />
        <Route
          path="*"
          element={
            <p>
              {ui.learningPageNotFound}
              <Link to="/learn">{ui.returnHome}</Link>
            </p>
          }
        />
      </Routes>
    </div>
  );
}
function Home() {
  const state = useLoad(async () => {
    const [sessions, goals, roadmaps, topics, activity] = await Promise.all([
      all<Session>("learning/sessions?resumable=true"),
      all<Goal>("learning/goals"),
      all<Roadmap>("learning/roadmaps"),
      all<Topic>("learning/topics"),
      all<Activity>("learning/activity"),
    ]);
    return { sessions, goals, roadmaps, topics, activity };
  }, "home");
  const [intention, setIntention] = useState("");
  const [target, setTarget] = useState("");
  const [error, setError] = useState("");
  const [editing, setEditing] = useState<Goal | null>(null);
  async function createGoal() {
    try {
      const [kind, id] = target.split(":");
      await send(
        editing ? "learning/goals/" + editing.id : "learning/goals",
        {
          intention,
          conceptIds: kind === "topic" ? [id] : [],
          roadmapVersionId: kind === "roadmap" ? id : null,
        },
        editing?.versionToken,
        editing ? "PUT" : "POST",
      );
      setIntention("");
      setEditing(null);
      state.reload();
    } catch (e) {
      setError((e as Error).message);
    }
  }
  return (
    <>
      <ErrorNotice error={state.error || error} />
      <section className="learning-hero">
        <p className="eyebrow">{ui.aLittlePracticeClearEvidence}</p>
        <h2>{ui.whatWillYouWorkOnToday}</h2>
        <p>{ui.exploreFreelyTestAnIdeaAndKeepARecord}</p>
        <div className="learning-actions">
          <Link className="primary-button" to="/learn/session-setup">
            {ui.setUpDailyPractice}
          </Link>
          <Link to="/learn/topics">{ui.browseTheLibrary}</Link>
        </div>
      </section>
      {state.data?.sessions.map((s) => (
        <section className="learning-panel" key={s.id}>
          <h3>{ui.continueYourSession}</h3>
          <p>
            {s.position + 1}
            {ui.of}
            {s.actualCount} · {s.state}
          </p>
          <Link className="ink-button" to={`/learn/sessions/${s.id}`}>
            {ui.resumeExisting}
          </Link>
        </section>
      ))}
      <div className="learning-columns">
        <section className="learning-panel">
          <h3>{ui.yourIntentions}</h3>
          {state.data?.goals
            .filter((g) => !g.archived)
            .map((g) => (
              <div className="learning-row" key={g.id}>
                <span>{g.intention}</span>
                <Button variant="secondary"
                  onClick={() => {
                    setEditing(g);
                    setIntention(g.intention);
                    setTarget(
                      g.roadmapVersionId
                        ? "roadmap:" + g.roadmapVersionId
                        : "topic:" + g.conceptIds[0],
                    );
                  }}
                >
                  {ui.edit}
                </Button>
                <Button variant="secondary"
                  onClick={() =>
                    void send(
                      `learning/goals/${g.id}`,
                      {},
                      g.versionToken,
                      "DELETE",
                    )
                      .then(state.reload)
                      .catch((e) => setError(e.message))
                  }
                >
                  {ui.archive}
                </Button>
              </div>
            ))}
          <label>
            {ui.whatWouldYouLikeToWorkOn}
            <input
              value={intention}
              onChange={(e) => setIntention(e.target.value)}
              maxLength={500}
            />
          </label>
          <label>
            {ui.goalTarget}
            <select value={target} onChange={(e) => setTarget(e.target.value)}>
              <option value="">{ui.chooseATopicOrRoadmap}</option>
              {state.data?.topics.map((t) => (
                <option key={t.concept.id} value={`topic:${t.concept.id}`}>
                  {t.concept.name}
                </option>
              ))}
              {state.data?.roadmaps.map((r) => (
                <option key={r.id} value={`roadmap:${r.id}`}>
                  {r.title}
                </option>
              ))}
            </select>
          </label>
          <Button variant="secondary"
            disabled={!target || !intention.trim()}
            onClick={() => void createGoal()}
          >
            {editing ? ui.saveGoal : ui.addGoal}
          </Button>
          {editing && (
            <Button variant="secondary"
              onClick={() => {
                setEditing(null);
                setIntention("");
                setTarget("");
              }}
            >
              {ui.cancelEdit}
            </Button>
          )}
        </section>
        <section className="learning-panel">
          <h3>{ui.routesThroughTheLibrary}</h3>
          {state.data?.roadmaps.map((r) => (
            <Link
              className="learning-card"
              key={r.id}
              to={`/learn/roadmaps/${r.id}`}
            >
              <strong>{r.title}</strong>
              <span>{r.description}</span>
            </Link>
          ))}
        </section>
      </div>
      <section className="learning-panel">
        <h3>{ui.recentActivity}</h3>
        {state.data?.activity.length === 0 && <p>{messages.empty}</p>}
        <ActivityList items={state.data?.activity.slice(0, 8) ?? []} />
      </section>
    </>
  );
}
function Topics() {
  const [query, setQuery] = useSearchParams();
  const search = query.get("search") ?? "";
  const parent = query.get("parent");
  const state = useLoad(
    () =>
      all<Topic>(
        `learning/topics?search=${encodeURIComponent(search)}${parent ? `&parent=${parent}` : ""}`,
      ),
    search + parent,
  );
  return (
    <section className="learning-panel">
      <h2>{ui.exploreTopics}</h2>
      <label>
        {ui.searchNameOrKey}
        <input
          value={search}
          onChange={(e) =>
            setQuery(
              { ...Object.fromEntries(query), search: e.target.value },
              { replace: true },
            )
          }
        />
      </label>
      {parent && <Link to="/learn/topics">{ui.showAllTopics}</Link>}
      <ErrorNotice error={state.error} />
      <div className="topic-grid">
        {state.data?.map((t) => (
          <Link
            key={t.concept.id}
            to={`/learn/topics/${t.concept.id}?section=overview&back=${encodeURIComponent("/learn/topics?" + query)}`}
            className="learning-card"
          >
            <small>
              {t.concept.locale} · {t.availableFamilies}
              {ui.exerciseFamilies}
            </small>
            <h3>{t.concept.name}</h3>
            <p>{t.concept.description}</p>
            <span>
              {t.materialVersions.length
                ? ui.materialAvailable
                : ui.practiceOnly}
            </span>
          </Link>
        ))}
      </div>
      {state.data?.length === 0 && <p>{ui.noTopicsMatchThisSearch}</p>}
    </section>
  );
}
function TopicWorkspace() {
  const { id } = useParams();
  const [query, setQuery] = useSearchParams();
  const section = query.get("section") ?? "overview";
  const location = useLocation();
  const state = useLoad(async () => {
    const [topic, topics, knowledge, activity] = await Promise.all([
      read<Topic>(`learning/topics/${id}`),
      all<Topic>("learning/topics"),
      read<Knowledge>(`evidence/knowledge/${id}`),
      all<Activity>(`learning/activity?conceptId=${id}`),
    ]);
    return { topic, topics, knowledge, activity };
  }, id ?? "");
  if (!state.data)
    return (
      <>
        <ErrorNotice error={state.error} />
        <p>{ui.loadingTopic}</p>
      </>
    );
  const { topic, knowledge } = state.data;
  const title = (conceptId: string) =>
    state.data?.topics.find((t) => t.concept.id === conceptId)?.concept.name ??
    ui.relatedTopic;
  const setup = `/learn/session-setup?conceptId=${id}&returnContext=${encodeURIComponent(location.pathname + location.search)}`;
  return (
    <section className="learning-panel">
      <Link
        to={
          query.get("back")?.startsWith("/learn/topics?")
            ? query.get("back")!
            : "/learn/topics"
        }
      >
        {ui.topics}
      </Link>
      <h2>{topic.concept.name}</h2>
      <p>{topic.concept.description}</p>
      <Tabs value={section} onValueChange={(value) => setQuery({ ...Object.fromEntries(query), section: String(value) })}>
      <TabsList className="learning-tabs" variant="line" aria-label={ui.topicSections}>
        {["overview", "material", "practice", "activity"].map((s) => (
          <TabsTrigger
            key={s}
            value={s}
          >
            {s}
          </TabsTrigger>
        ))}
      </TabsList>
      <TabsContent value="overview">
        <>
          <p>
            {knowledge.familyCount
              ? ui.evidenceFacts(knowledge.familyCount, knowledge.assistedCount)
              : messages.unknown}
          </p>
          <div className="learning-actions">
            <Button variant="secondary"
              onClick={() =>
                setQuery({ ...Object.fromEntries(query), section: "material" })
              }
            >
              {ui.readMaterial}
            </Button>
            <Link className="ink-button" to={setup}>
              {ui.practice}
            </Link>
          </div>
          <h3>{ui.connectedTopics}</h3>
          {topic.relations.map((r, i) => (
            <p key={i}>
              {r.kind === "Prerequisite"
                ? ui.prerequisiteSuggestion
                : r.kind === "PartOf"
                  ? ui.topicHierarchy
                  : ui.relatedTopic}
              :{" "}
              <Link
                to={`/learn/topics/${r.sourceId === id ? r.targetId : r.sourceId}`}
              >
                {title(r.sourceId === id ? r.targetId : r.sourceId)}
              </Link>
            </p>
          ))}
          <TopicRoadmaps conceptId={id!} />
        </>
      </TabsContent>
      <TabsContent value="material">
        {topic.materialVersions.length ? (
          topic.materialVersions.map((m) => <MaterialReader id={m} key={m} />)
        ) : (
          <p>{ui.noMaterialIsAvailableYouCanStillPractice}</p>
        )}
      </TabsContent>
      <TabsContent value="practice">
        <>
          <p>
            {ui.practiceIncludesThisTopicAndItsContainmentDescendantsRelated}
          </p>
          <Link className="primary-button" to={setup}>
            {ui.chooseSessionSizeAndFormats}
          </Link>
        </>
      </TabsContent>
      <TabsContent value="activity">
        <>
          <ActivityList items={state.data.activity} />
          <EvidenceHistory conceptId={id!} />
        </>
      </TabsContent>
      </Tabs>
    </section>
  );
}
function TopicRoadmaps({ conceptId }: { conceptId: string }) {
  const state = useLoad(async () => {
    const roadmaps = await all<Roadmap>("learning/roadmaps");
    const views = await Promise.all(roadmaps.map(r => read<RoadmapView>(`learning/roadmaps/${r.id}`)));
    return views.filter(r => r.stages.some(s => s.stage.conceptId === conceptId));
  }, conceptId);
  return <><ErrorNotice error={state.error} />{!!state.data?.length && <><h3>{ui.topicRoadmaps}</h3>{state.data.map(r => <p key={r.roadmap.id}><Link to={`/learn/roadmaps/${r.roadmap.id}`}>{r.roadmap.title}</Link></p>)}</>}</>;
}
export function MaterialReader({ id }: { id: string }) {
  const state = useLoad(() => read<Material>(`learning/materials/${id}`), id);
  const operation = useRef({ id, operationId: crypto.randomUUID() });
  const [activityError, setActivityError] = useState("");
  async function recordView() {
    if (operation.current.id !== id)
      operation.current = { id, operationId: crypto.randomUUID() };
    try {
      await send("learning/material-views", {
        operationId: operation.current.operationId,
        materialVersionId: id,
      });
      setActivityError("");
    } catch (error) {
      setActivityError((error as Error).message);
    }
  }
  useEffect(() => {
    if (state.data) void recordView();
  }, [state.data]);
  return (
    <>
      <ErrorNotice error={state.error} />
      {state.data && (
        <>
          <small>
            {ui.contentLanguage}
            {state.data.locale}
          </small>
          <MaterialBlocks blocks={state.data.blocks} />
          {activityError && (
            <div role="status">
              <p>
                {ui.readingActivityWasNotAcknowledged}
                {activityError}
              </p>
              <Button variant="secondary" onClick={() => void recordView()}>
                {ui.retryRecordingThisView}
              </Button>
            </div>
          )}
        </>
      )}
    </>
  );
}
function ActivityList({ items }: { items: Activity[] }) {
  const [filter, setFilter] = useState("");
  return (
    <>
      <label>
        {ui.activityFilter}
        <select value={filter} onChange={(e) => setFilter(e.target.value)}>
          <option value="">{ui.allActivity}</option>
          <option value="attempts">{ui.objectiveAttempts}</option>
          <option value="self-reviews">{ui.selfReview}</option>
          <option value="Hint">{ui.hints}</option>
          <option value="Reference">{ui.references}</option>
          <option value="Reveal">{ui.reveals}</option>
        </select>
      </label>
      {items
        .filter((a) => !filter || a.kind === filter)
        .map((a) => (
          <div className="learning-row" key={a.id}>
            <span>
              {a.kind} · {new Date(a.receivedAt).toLocaleString()}
            </span>
            {a.sessionId && (
              <Link
                to={`/learn/sessions/${a.sessionId}/summary${a.attemptId ? `#${a.attemptId}` : ""}`}
              >
                {ui.inspectSavedWork}
              </Link>
            )}
          </div>
        ))}
    </>
  );
}
function SessionSetup() {
  const [query] = useSearchParams();
  const navigate = useNavigate();
  const [count, setCount] = useState(5);
  const [types, setTypes] = useState<typeof exerciseTypes>([]);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [operation, setOperation] = useState<string | null>(null);
  const setup: Setup = {
    count,
    types,
    conceptId: query.get("conceptId") ?? undefined,
    roadmapStageId: query.get("roadmapStageId") ?? undefined,
    enrollmentId: query.get("enrollmentId") ?? undefined,
    locale: "en",
    feedbackPolicy: "Immediate",
    returnContext: query.get("returnContext") ?? "/learn",
  };
  const key = JSON.stringify(setup);
  const preview = useLoad(
    () => send<Preview>("learning/session-previews", setup),
    key,
  );
  const existing = useLoad(
    () => all<Session>("learning/sessions?resumable=true"),
    "existing",
  );
  async function start() {
    setBusy(true);
    setError("");
    const op = operation ?? crypto.randomUUID();
    setOperation(op);
    try {
      const result = await send<Mutation>("learning/sessions", {
        operationId: op,
        setup,
      });
      navigate(`/learn/sessions/${result.session.id}`);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="learning-panel">
      <h2>{ui.makeRoomForPractice}</h2>
      <p>{ui.fixedCountNoTimerYouDecideWhenToContinue}</p>
      <ErrorNotice error={error || preview.error} />
      {existing.data?.map((s) => (
        <div className="learning-error" key={s.id}>
          <p>
            {ui.a}
            {s.state.toLowerCase()}
            {ui.sessionIsWaiting}
          </p>
          <Link to={`/learn/sessions/${s.id}`}>{ui.resumeExisting}</Link>
          <Button variant="secondary"
            onClick={() =>
              void send(
                `learning/sessions/${s.id}/end`,
                { operationId: crypto.randomUUID() },
                s.versionToken,
              )
                .then(() => {
                  existing.reload();
                  navigate(`/learn/sessions/${s.id}/summary`);
                })
                .catch((e) => setError(e.message))
            }
          >
            {ui.endExistingAndViewPartialSummary}
          </Button>
        </div>
      ))}
      <fieldset disabled={busy || operation !== null}>
        <legend>{ui.sessionSize}</legend>
        {[3, 5, 10].map((n) => (
          <label className="inline-choice" key={n}>
            <input
              type="radio"
              checked={count === n}
              onChange={() => setCount(n)}
            />
            {n}
            {ui.exercises}
          </label>
        ))}
      </fieldset>
      <fieldset disabled={busy || operation !== null}>
        <legend>{ui.formatsNoneSelectedMeansAll}</legend>
        {exerciseTypes.map((t) => (
          <label className="inline-choice" key={t}>
            <input
              type="checkbox"
              checked={types.includes(t)}
              onChange={(e) =>
                setTypes(
                  e.target.checked
                    ? [...types, t]
                    : types.filter((v) => v !== t),
                )
              }
            />
            {t}
          </label>
        ))}
      </fieldset>
      {preview.data && (
        <>
          <p>
            {preview.data.actualCount}
            {ui.availableAbout}
            {Math.ceil(preview.data.estimatedSeconds / 60)}
            {ui.minutes}
          </p>
          {preview.data.actualCount < count && <p>{messages.shortage}</p>}
          {preview.data.englishFallback && (
            <p>{ui.englishContentWillBeUsedAsAFallback}</p>
          )}
          {preview.data.unavailableReason && (
            <p>
              {preview.data.unavailableReason}{" "}
              <Link to="/learn/topics">{ui.exploreTopics}</Link>
            </p>
          )}
        </>
      )}
      <Button variant="default"
        className="primary-button"
        disabled={busy || !preview.data?.actualCount || !!existing.data?.length}
        onClick={() => void start()}
      >
        {busy
          ? ui.starting
          : `${operation ? ui.retryStart : "Start"} ${preview.data?.actualCount ?? ""} exercises`}
      </Button>
    </section>
  );
}
function RoadmapWorkspace() {
  const { id } = useParams();
  const state = useLoad(
    () => read<RoadmapView>(`learning/roadmaps/${id}`),
    id ?? "",
  );
  const [error, setError] = useState("");
  async function action(stage?: string, traversal?: string) {
    try {
      if (!state.data?.enrollment)
        await send("learning/roadmap-enrollments", {
          operationId: crypto.randomUUID(),
          roadmapVersionId: id,
        });
      else {
        const e = state.data.enrollment;
        await send(
          `learning/roadmap-enrollments/${e.id}`,
          {
            operationId: crypto.randomUUID(),
            paused: stage ? e.paused : !e.paused,
            stageId: stage,
            traversal,
          },
          e.versionToken,
          "PUT",
        );
      }
      state.reload();
    } catch (e) {
      setError((e as Error).message);
    }
  }
  return (
    <section className="learning-panel">
      <ErrorNotice error={state.error || error} />
      {state.data && (
        <>
          <h2>{state.data.roadmap.title}</h2>
          <p>{state.data.roadmap.description}</p>
          <Button variant="secondary" onClick={() => void action()}>
            {!state.data.enrollment
              ? ui.followThisRoadmap
              : state.data.enrollment.paused
                ? ui.resumeGuidance
                : ui.pauseGuidance}
          </Button>
          {state.data.stages.map((s) => (
            <article className="roadmap-stage" key={s.stage.id}>
              <small>
                {ui.stage}
                {s.stage.position + 1} · {s.traversal}
              </small>
              <h3>
                <Link to={`/learn/topics/${s.stage.conceptId}`}>
                  {s.stage.title}
                </Link>
              </h3>
              <p>{s.evidenceStatus}</p>
              <p>
                {s.evidence?.qualifyingFamilies ?? 0}/{s.stage.minimumFamilies}
                {ui.families}
                {s.evidence?.qualifyingSessions ?? 0}/{s.stage.minimumSessions}
                {ui.sessions}
                {Math.round(s.stage.minimumCorrectness * 100)}
                {ui.requiredObjectiveCorrectness}
              </p>
              <p>
                {s.availableFamilies}
                {
                  ui.qualifyingFamiliesAvailablePrerequisitesAreSuggestionsEveryStageIs
                }
              </p>
              <div className="learning-actions">
                <Link
                  to={`/learn/session-setup?roadmapStageId=${s.stage.id}&returnContext=${encodeURIComponent("/learn/roadmaps/" + id)}${state.data!.enrollment ? `&enrollmentId=${state.data!.enrollment.id}` : ""}`}
                >
                  {ui.practiceThisStage}
                </Link>
                {state.data!.enrollment && (
                  <>
                    <Button variant="secondary" onClick={() => void action(s.stage.id, "Visited")}>
                      {ui.markVisited}
                    </Button>
                    <Button variant="secondary" onClick={() => void action(s.stage.id, "Skipped")}>
                      {ui.skipInThisRoadmap}
                    </Button>
                  </>
                )}
              </div>
              {s.evidence?.observationIds.length ? (
                <Link
                  to={`/learn/topics/${s.stage.conceptId}?section=activity`}
                >
                  {ui.inspectSupportingActivity}
                </Link>
              ) : null}
            </article>
          ))}
        </>
      )}
    </section>
  );
}
export function Panel({ children }: { children: ReactNode }) {
  return <section className="learning-panel">{children}</section>;
}
