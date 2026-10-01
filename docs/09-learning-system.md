# Learning subsystem: architecture and implementation contract

**Decision date: 24 September 2026. Status: agreed design, not implemented by this documentation change.**

This document specifies the first learning milestone and the contracts needed for later expansion. Read it with [learner experience](10-learning-experience.md) and [delivery and verification](11-learning-delivery-plan.md). The [original supplied specification](inputs/learning-system-specification.md) is preserved byte-for-byte. Where it differs from the decisions below, this document governs the new learning work.

## 1. Product contract and decisions

Sensei++ is a learning system for experienced developers. Shared concepts, exercise results, and inspectable evidence connect independent practice, roadmaps, and eventually work-derived learning signals. A roadmap recommends a route through knowledge; it does not own knowledge or reset progress.

Technology names are content data. The engine must not branch on C#, ASP.NET Core, RabbitMQ, or a particular concept key. The same graph, exercise interfaces, selection policy, topic workspace, and history support future languages and technologies. C# fundamentals are test fixtures, not a hardcoded curriculum. Producing lessons, sourcing production content, authoring tools, and publication workflows are separate concerns.

| Decision | Agreed behavior | Reason |
| --- | --- | --- |
| Audience | Experienced developers; no mandatory beginner course or diagnostic | Users can choose what is useful without proving eligibility |
| First platform | Existing responsive React web client and .NET/PostgreSQL backend | Extend the application already present |
| Main interaction | Topic workspace with overview, material, practice, and activity | Put reference and practice in one navigable context |
| Session size | 3, 5, or 10 exercises; default 5 | A bounded session is understandable without time pressure |
| Short-session feedback | After each answer; explicit Continue | Preserve time to inspect an explanation |
| Exam feedback | Deferred review contract now, usable mode in Phase 3 | Avoid adding exams to Phase 1 through an engine abstraction |
| AI | No provider required for core learning | Availability and quota must not determine whether practice works |
| Visible knowledge | Evidence summaries in Phase 1; per-dimension estimates in Phase 2 | Avoid presenting a coarse selection score as broad competence |
| Self-review | Low influence; positive contribution at most once per owner/concept per 72 hours | Repeated self-ratings must not manufacture progress |
| Offline | Stable fact identities and versioning now; working offline execution/sync in Phase 4 | Preserve the future path without requiring an offline client in the first release |
| Cost/language | Local deployment, no paid dependencies, English first | Preserve existing constraints; later AI remains configurable and free-only |

### Refinements of the supplied specification

| Input or earlier proposal | Conclusion from discussion |
| --- | --- |
| Example technologies might look like product modules | Subjects are independently supplied content; C# structs/classes, OOP, events, and delegates provide test data only |
| Initial roadmap choice was ASP.NET Core | Superseded by the user's clarification: use C# fundamentals for testing and keep all subjects independent of the engine |
| Initial suggestion was practice-first | Superseded by the selected topic workspace |
| Daily sessions described through time budgets | Use fixed counts; display estimated time without a countdown |
| Phase 1/2 overlap on estimates, decay, priorities | Follow phases strictly: a basic internal estimate now; dimensions, decay, scheduling, diagnostics, and richer priorities in Phase 2 |
| Original examples show aggregate technology percentages | Do not show an overall technology mastery percentage; future estimates are per concept/dimension |
| Self-review could be excluded from estimates | Admit a small, capped contribution with the selected 72-hour cooldown |
| Intensive/exam feedback might extend Phase 1 | Reserve the policy now; deliver deferred-feedback sessions in Phase 3 |
| Earlier project documents required offline in the first checkpoint | Full offline support moves to learning Phase 4; identities and contracts remain an architectural requirement now |
| Agent signals appear in both Phase 2 and Phase 5 | Phase 2 defines ingestion contracts; Phase 5 implements actual providers/integrations |
| Content authoring proposed as a repository/editor workflow | Supply strategy is explicitly deferred; specify only content the learning runtime consumes |

### Delivery phases

| Phase | Capabilities | Explicit exclusions from Phase 1 |
| --- | --- | --- |
| 1: learning foundation | Graph, consumable versioned material, six exercise types, sessions, goals, basic selection/estimate, evidence summaries, one data-defined test roadmap | No production content pipeline |
| 2: adaptation | Spaced repetition, decay, multiple knowledge dimensions, diagnostics, weak-area sessions, priorities, recommendation explanations, generic signal contracts | No scheduled review or decay in Phase 1 |
| 3: intensive work | Exam sessions and deferred review; reasoning, debugging, architecture, review, interview and project formats added incrementally | No arbitrary code execution, open-answer grading, or intensive UI in Phase 1 |
| 4: offline | Packs, local execution/storage, local projection, push/pull sync, reconciliation and visible sync state | Phase 1 does not promise disconnected restart/recovery |
| 5: integrations | Local agent, commit reports, IDE and other external signal providers | No source ingestion prerequisite |
| 6: optional AI | Hints, explanation assessment, follow-ups, generation and voice as selected | No AI key, request, fixture impersonating a live assessment, or quota dependency in core practice |

## 2. Verified baseline and module boundaries

Source inspection on the decision date found an existing modular backend, PostgreSQL persistence/migration, concept CRUD, manually recorded evidence, and a React workbench. Exercises, learning sessions, goals, roadmaps, and scored knowledge projections are new planned work. This is a source inventory, not a claim of running or testing the application.

Relevant implementation anchors:

- [Concept identity](../backend/src/Modules/Learning/Sensei.Modules.Learning.Domain/Concept.cs) and [Learning endpoints](../backend/src/Modules/Learning/Sensei.Modules.Learning.Api/LearningEndpoints.cs).
- [Existing evidence](../backend/src/Modules/Evidence/Sensei.Modules.Evidence.Domain/EvidenceObservation.cs) and [host cross-module mappings](../backend/src/Sensei.Host/Persistence/CrossModuleModelConfiguration.cs).
- [Dependency tests](../backend/tests/Sensei.ArchitectureTests/ProjectDependencyTests.cs), [current client](../client/src/App.tsx), and [HTTP contract helpers](../backend/src/BuildingBlocks/Sensei.BuildingBlocks.Api/HttpContract.cs).

Retain `Concept.Id` as the canonical concept identity. The product phrase KnowledgeNode does not justify another identity/table hierarchy that breaks existing evidence and experience references. Add relationships and consumable content around existing concepts. Technology-neutral keys remain stable; localized labels and material versions must not change identity.

| Boundary | Owns | Integration contract |
| --- | --- | --- |
| Learning | Concepts/relations, material/exercise versions, sessions/items/attempts/results, goals, roadmaps/enrollment, selection | Requests knowledge summaries and stages validated result evidence through consumer-owned ports |
| Evidence | Structured observations, contribution eligibility, self-review allowance, knowledge projection | Stages observations and projection updates without a nested commit; provides owner-scoped summaries |
| Host composition | Port adapters, cross-module FK configuration, shared transaction composition | Connects application contracts without direct module-to-module project references |
| React | Topic navigation, accessible interactions, transient drafts, feedback and recovery | Uses API resources; never supplies trusted score or ownership |
| Future sync boundary | Receipts/change feed/device registration and reconciliation | Applies authorized domain operations; cannot directly replace profile values |

Keep one backend EF unit of work. Do not add RabbitMQ, independently deployed services, or a universal workflow framework. Domain projects remain independent; modules do not reference other modules. A Learning application port can be implemented by a host adapter using the Evidence application service. In a coordinated submission, owning services stage their writes and the caller commits once.

## 3. Domain and persistence

### Records and invariants

| Record | Minimum responsibility and invariants |
| --- | --- |
| Concept relation | Source/target concept IDs and relation kind; no self-edge or duplicate edge |
| Material version | Immutable ID/version, topic, locale, ordered safe content blocks and asset references |
| Exercise family | Stable ID grouping equivalent variants; evidence diversity is measured by family, not exercise ID |
| Exercise version | Immutable identity/version, family, type, interaction schema, prompt/assets, assessed concepts, hints, feedback, evaluation definition |
| Learning goal | Owner, target concept(s) or roadmap, stated intention, active/archived state and concurrency version |
| Learning session | Owner, scope, requested/actual item count, feedback policy, pinned item list, selection-policy version, lifecycle, current position |
| Session item | Session/position, pinned exercise version, draft, terminal outcome and assistance facts |
| Attempt | Immutable answer/note, exact item/version, first-submission or answer-aware retry, previous-attempt link, recorded/received times |
| Exercise result | Exact attempt, evaluator version, objective binary result or self-review classification; no inferred free-text grade |
| Learning event | Stable fact ID, operation ID, schema version, owner and typed source references, origin and timestamps |
| Knowledge observation | Owner/concept and validated source result, family/session, score or self-report, assistance, status and policy provenance |
| Knowledge state | Rebuildable owner/concept projection, supporting observation IDs, unknown/known internal estimate and policy version |
| Self-review allowance | Owner/concept admission state needed to serialize positive contributions and retain cooldown history |
| Roadmap version | Immutable ordered stages referencing shared concepts/material/exercises and configured evidence requirements |
| Roadmap enrollment | Owner, roadmap version, active/paused state and navigation/traversal; knowledge remains a shared projection |
| Operation receipt | Owner/operation ID, command kind/target, canonical payload digest and committed outcome identity |

Use relational columns and foreign keys for ownership, identities, lifecycle, ordering, versions, and core relations. Use validated versioned JSON for format-specific interaction definitions and answer payloads. Do not put the entire domain in an opaque JSON session transcript.

New scored observations need validated result provenance. A legacy polymorphic `SourceKind + SourceId` label is insufficient authority. Use typed source links and owner-consistent validation; host mappings can connect module-owned IDs where required without introducing assembly dependency cycles. Enforce uniqueness for a result's contribution to a concept and for operation receipts.

An append-only learning log records activity facts for traceability and future sync. Ordinary aggregates and immutable revisions remain the operational source of truth; this is not full application event sourcing. Evidence status revisions are separate facts; historical answers never change when an observation is excluded.

### Graph semantics

Phase 1 relation directions are `PartOf(child, parent)`, `Prerequisite(requiredConcept, dependentConcept)`, and symmetric `RelatedTo`. Treat reverse RelatedTo pairs as one logical edge. Reject cycles within containment and within prerequisites. Validate additions against the existing graph transactionally so concurrent writes cannot each approve half of a cycle.

Topic-scoped practice includes the selected concept and its containment descendants. Prerequisites and related topics remain navigation suggestions; they do not silently expand an explicitly selected topic's session. The UI may let the user navigate to them deliberately.

### Versioning and compatibility

Mutable resource versions and immutable content versions are different. ETags guard current edits; a session references the exact immutable content/evaluator versions it used.

New sessions select available versions. Content updates cannot change an existing item's prompt, answer key, or feedback. Disabled content is excluded from new selection while history remains inspectable. Apply additive migrations, preserve concept IDs, and leave existing unscored manual evidence visible without converting it into numeric evidence.

## 4. Consumable content and evaluation

The content boundary specifies what the runtime needs, not how authors create or publish it. Tests may supply fixtures through test setup; no authoring UI or production importer is implied.

### Material and exercise envelope

Material uses ordered declarative blocks: heading, paragraph, list, code with language label, image with alternative text, and reference link. Render supported blocks through application controls; never execute arbitrary HTML, JavaScript, content-supplied React components, or learner code. Validate supported URL/asset schemes and show an explicit unavailable state for missing assets.

Each exercise includes stable exercise/version/family IDs, type/schema version, locale, prompt/supporting material, assessed concept mappings, difficulty, estimated seconds, hints, feedback, and evaluator identity. Technology/version labels are metadata. Separate assessed concepts from incidental concepts appearing in a prompt. Optional dimension tags reserve future interpretation without calculating dimensions in Phase 1.

Split learner presentation, evaluator input, and post-answer feedback into separate projections. Initial online responses omit keys, explanations that reveal the answer, and internal grading metadata. General topic material can intentionally explain the subject; opening it during an item is recorded assistance rather than claimed exam secrecy.

| Type | Answer representation | Evaluation and validation |
| --- | --- | --- |
| Flashcard | Optional note plus Again/Partly/Recalled after reveal | Self-report only; no semantic grading of the note |
| Prediction | Stable outcome ID | Match accepted authored outcome; reject unknown ID |
| MultipleChoice | Stable selected-option IDs | Exact accepted set; enforce single/multiple choice cardinality; reject duplicates/unknown IDs |
| Ordering | Complete ordered item-ID list | Match any accepted authored order; require every item exactly once |
| Matching | Mapping of left IDs to counterpart IDs | Match an accepted mapping; require complete valid mapping and declared one-to-one behavior |
| BugSpotting | Selected declared-region/finding IDs or explicit No issue | Exact accepted set; No issue is exclusive and is correct only if authored as valid |

Objective scores are 0 or 1. No partial credit in Phase 1. Invalid/incomplete payloads receive validation errors without consuming an attempt; valid incorrect answers are persisted results. Optional feedback may explain correct components without changing binary scoring. Open notes are stored faithfully and remain ungraded.

Record hint use, material/reference opening, and answer reveal separately. An assisted correct answer remains correct. A retry after feedback is a new linked answer-aware practice attempt; it cannot overwrite the first result or create independent evidence. Skipping creates no failed score.

## 5. Session lifecycle and transaction protocol

### Lifecycle

Persist `Active`, `Paused`, `Completed`, and `EndedEarly`. Creation freezes the selected item list and activates the first item. At most one ordinary Active or Paused session exists per owner in Phase 1; enforce this in persistence, not only the screen.

Pause preserves draft and position. Resume returns to that position. An answered/self-reviewed/skipped item is terminal for session progress. Submission retains the current presentation position so refresh can recover its feedback; an explicit Continue/advance command moves to the next item. When all selected items are terminal, complete the session; the client still shows the last item's feedback until Continue opens the summary. Ending early leaves unvisited items unassessed and keeps all completed work. Optional retries are linked practice, not replacements for terminal outcomes.

Persist `Immediate` and reserve `EndOfSession`. Phase 1 creation rejects unsupported deferred mode instead of silently changing it. The future deferred contract saves immutable answers without interim correctness/keys/explanations/score and releases review after finalization. Hint restrictions, timers, exam resume rules, and other exam policy remain Phase 3 work.

### Atomic submission

After identifying the owner, check the operation receipt before applying a new mutation's stale-version test. For a new submission, one transaction must:

1. Validate session/item ownership, active state, request identity and expected version.
2. Load the pinned exercise/evaluator and validate the payload.
3. Save the immutable answer and assistance snapshot.
4. Evaluate deterministically, or record explicitly labeled self-review.
5. Save result and activity event.
6. Stage structured observations and rebuild the affected knowledge projection.
7. Update item/session state and any owner/concept self-review allowance.
8. Save the receipt and commit once.

Do not queue an AI job for ordinary Phase 1 feedback. Failure at any required step rolls back domain effects and receipt. A retry after commit returns the original outcome and does not regrade or consume cooldown again. Same operation ID with different semantic payload/target is a conflict. Authentication/ownership checks still apply before any receipt is disclosed.

Serialize allowance admission by owner/concept, using a database uniqueness/locking boundary that also handles first-row creation. Acquire multiple concept locks in stable ID order. Concurrent session creation must also enforce one resumable session. These are required race invariants; in-memory locks are insufficient.

Evidence dispute/withdrawal removes the observation from active projections in the same transaction while retaining the source attempt. It must not rewind a consumed self-review allowance and permit the same positive activity to mint another gain. Projection rebuilds replay recorded admissions; they do not reconsider formerly cooldown-blocked reviews as newly eligible.

## 6. Knowledge and gain policy

Phase 1 displays evidence, assistance, coverage and dates. No universal mastery percentage and no dimension estimates are exposed. Internal values are bounded policy heuristics used for practice selection, not calibrated probabilities or certifications.

### Objective estimate

For each owner/concept:

1. Exclude disputed, superseded, withdrawn, skipped, answer-aware and operational-failure records.
2. Select the latest eligible first submission in each exercise family, with server receipt time and stable ID tie-breaking.
3. Keep at most 20 most recently observed distinct families.
4. Give unassisted results weight 1.0 and hint/reference-assisted results weight 0.5.
5. Let `N = sum(weight * binaryScore)` and `W = sum(weight)`.
6. The objective estimate is `N / W` if W is positive; otherwise it is unknown.

Repeated variants can change the current observation for one family but cannot increase independent family count. History retains all attempts. There is no temporal decay, speed penalty, or scheduled-review progression in this phase.

### Self-review

Only Recalled is eligible for a positive contribution. Admit at most one per rolling 72 hours for an owner/concept, measured by authoritative server receipt time. The exact 72-hour boundary is eligible. A multi-concept card applies this rule independently to its assessed concepts, with atomic result recording.

Save all self-reviews. Again and Partly remain visible reports without automatic negative assessed evidence. Cooldown-blocked reviews produce history but no gain. Only the most recent still-active admitted positive contribution participates in the current estimate; old positive reviews never stack. If an admitted observation is withdrawn, exclude it without inventing a replacement admission or resetting the allowance.

With objective evidence and an eligible positive contribution:

```text
objective = N / W
candidate = (N + 0.1) / (W + 0.1)
estimate = min(candidate, objective + 0.05, 1.0)
```

Without an eligible contribution, use the objective estimate. Without objective evidence, keep assessed knowledge unknown. Consuming the allowance does not require a numeric increase: an already perfect estimate or absent objective evidence still consumes an admitted Recalled review. Replayed requests return the original decision.

Examples: one unassisted incorrect family gives objective 0; a positive self-review proposes about 0.091 but is capped at 0.05. Four unassisted correct families and one incorrect family give 0.8; the positive review yields approximately 0.804. With no objective family, only self-review history appears. These examples explain policy behavior, not measured learning efficacy.

### Roadmap evidence

Traversal and evidence coverage are independent. A node's initial configurable requirement is three distinct families, unassisted first-submission objective evidence, at least two sessions, and at least 80% correctness across qualifying observations. Use the same latest-per-family and 20-family window, filtered to unassisted objective observations, for this check. Self-review never participates.

Expose counts, session coverage, correctness and supporting attempts with the label **Evidence requirement met**. Evidence changes can change coverage; historical traversal is retained. If available content cannot provide enough distinct qualifying families, show **Insufficient exercise coverage**. Reading and manual skipping cannot satisfy the requirement.

Keep policy versions and settings centralized. Defaults are: window 20, weights 1.0/0.5/0.1, positive cooldown 72 hours, uplift cap 0.05, and roadmap minimums 3 families/2 sessions/0.8 correctness. Change policies through an explicit versioned rebuild, not by silently rewriting historical results or cooldown decisions.

## 7. Selection, goals and roadmap navigation

Filter first: supported type/schema, available version/assets, locale, explicit scope and type filters. Prefer the configured content locale; show an explicit English fallback rather than silently mixing languages. Goal targets and active roadmap context influence mixed daily sessions; they do not enlarge an explicitly selected topic.

Rank candidates using goal relevance, observed objective difficulty, unseen family coverage, current roadmap relevance, and recent-family repetition penalties. Centralize configuration and persist the chosen policy/version and item list. Do not implement due-review or agent-signal allocations when those inputs do not yet exist.

For an implementation starting point, use normalized factors in [0,1] and weights 4 for goal relevance, 3 for observed weakness, 2 for unseen-family coverage, 1 for current-roadmap relevance, and -3 for a family used in the preceding 24 hours. Unknown knowledge has no observed-weakness value and is handled by coverage/exploration, not scored as failure. These are tunable engineering defaults rather than user-established learning science. Prerequisite relevance is an ordering preference among otherwise comparable candidates, never a lock.

Select at most one variant of each family per session. Prefer a different primary concept and type from the previous item when alternatives exist; otherwise take the highest ranked candidate. Break ties by stable IDs. Estimate duration by summing selected metadata. In cold start with no goal/topic, offer a balanced available sample labeled mixed practice, not a personalized diagnosis.

Return requested and actual counts. With no eligible exercise, return a readable unavailable reason rather than creating an empty session. With fewer exercises than requested, show the actual count before starting and do not repeat questions to fill it. Selection preview is provisional; creation revalidates content and pins the final set.

Roadmaps define sequence and node evidence requirements over shared concepts. Enrollment pins a roadmap version and stores navigation/active state. Every stage can be entered; prerequisites are advisory. A new enrollment references existing knowledge and never copies, resets, or owns it. Goal archive and enrollment pause affect future selection, not history or other learning contexts.

## 8. Proposed API surface

All routes below are **planned**, except existing concept CRUD. Retain `/api/v1` JSON, cursor envelopes, stable Problem Details codes and `traceId`. Route names make the intended boundary concrete; generated OpenAPI is updated only with application implementation.

| API area | Proposed operations | Contract essentials |
| --- | --- | --- |
| Topics | `GET /learning/topics`, `GET /learning/topics/{conceptId}` | Search/browse, overview/relations, safe material metadata and exercise availability; owner-scoped activity is separate |
| Material | `GET /learning/materials/{versionId}` | Immutable safe blocks and asset references; no inferred reading assessment |
| Reading activity | `POST /learning/material-views` | Optional owner-scoped unscored reading fact with operation ID and material version; a GET alone does not mutate history |
| Goals | `GET/POST /learning/goals`, `PUT/DELETE /learning/goals/{id}` | Create/list/revise/archive; concept or roadmap targets; DELETE archives |
| Selection | `POST /learning/session-previews` | Read-only preview of scope/types/count/duration/shortage; no answer key |
| Sessions | `GET/POST /learning/sessions`, `GET /learning/sessions/{id}` | Recent/resumable sessions; creation scope/count/Immediate/operation ID; immutable pinned items |
| Session lifecycle | `POST /learning/sessions/{id}/pause`, `/resume`, `/end` | Version-guarded state transition; end means EndedEarly unless already complete |
| Presentation position | `POST /learning/sessions/{id}/advance` | Explicit Continue after a terminal item; versioned position change; final Continue opens the summary |
| Item presentation | `GET /learning/sessions/{sessionId}/items/{itemId}` | Owned assigned item, draft, assistance and allowed feedback; never arbitrary answer-key lookup |
| Drafts | `PUT /learning/sessions/{sessionId}/items/{itemId}/draft` | Versioned selected IDs/mapping/order and optional note; no result |
| Assistance | `POST /learning/sessions/{sessionId}/items/{itemId}/assistance` | Kind and hint/material/reveal target; save fact before returning gated hint/flashcard answer |
| Submission | `POST /learning/sessions/{sessionId}/items/{itemId}/attempts` | Stable operation ID, answer, optional note/previous-attempt link; server evaluates |
| Self-review/skip | `POST /learning/sessions/{sessionId}/items/{itemId}/self-reviews`, `/skip` | Revealed flashcard rating or unscored skip; idempotent facts |
| Results | `GET /learning/sessions/{id}/summary`, `GET /learning/attempts/{id}` | Exact owned result/feedback, assistance, retries and partial/completed summary |
| Roadmaps | `GET /learning/roadmaps`, `GET /learning/roadmaps/{versionId}` | Stages, concepts and requirements; no duplicated knowledge |
| Enrollment | `GET/POST /learning/roadmap-enrollments`, `PUT /learning/roadmap-enrollments/{id}` | Active/paused state and navigation; versioned traversal actions include visit/manual skip |
| Profile/history | `GET /evidence/knowledge`, `GET /evidence/knowledge/{conceptId}`, `GET /learning/activity` | Owner-scoped evidence/coverage, filters and supporting source links; no public Phase 1 mastery percentage |

Paths are relative to `/api/v1`. Content read access does not confer write authority. Owner identity is derived from the request context; payload-supplied owner IDs and scores cannot mint authoritative observations.

Use the session ETag as the Phase 1 concurrency boundary for item and lifecycle mutations. Responses return the new session token; the client serializes its draft/assistance/submit commands. Goal and enrollment mutations use their own ETags. Immutable result identities remain distinct from concurrency versions.

Require strong `If-Match` on existing-resource mutations: absent -> 428, malformed -> 400, stale -> 412. Creates do not require If-Match. Creating immutable facts also requires an operation ID; same ID/different payload -> 409. Lists return `{ items, nextCursor }`. Cross-owner IDs use the existing not-found behavior without leaking resource details. Domain conflicts such as an already open session remain 409, distinguishable from stale resource versions.

Receipt replay must return the original result even if the session has since advanced. It must not regress the client's current ETag; fetch current session state if replaying an older receipt. Public manual-evidence endpoints cannot create trusted scored observations through arbitrary source labels; legacy observations remain unscored unless connected by an authorized internal flow.

## 9. Recovery and future offline compatibility

Phase 1 saves drafts to the server after 750 ms of idle editing and before deliberate pause/navigation. Saving, Saved, and Save failed are separate UI states. Keep failed edits in the open page; do not claim durability beyond the last acknowledged save. Refresh recovers the server draft and committed progress. No localStorage cache of answers or unimplemented sync indicator is implied.

Concurrency conflicts preserve in-page text and offer reload/copy/recovery. Do not automatically resubmit against a newer version without inspecting it. Lost-response retries reuse the same operation ID. A terminal result cannot be changed by late autosave.

Establish the learning-event envelope now: event ID, operation ID, schema version, typed session/item/attempt/content references, origin, server receipt time, and optional client-recorded time/device ID/sequence. Device metadata is provenance, not authentication or trusted time.

Phase 4 clients will save input plus pending operation atomically, evaluate downloaded deterministic content locally, and show provisional results. The server revalidates/recomputes against pinned content rather than accepting uploaded mastery. Preserve distinct attempts from both devices; collapse duplicates by identity and retain family contribution limits. Server receipt-time admission remains authoritative for self-review, so local gains can be corrected during reconciliation and must be labeled provisional.

Retain content required by unsynchronized work under an explicit future pack-retention policy. Do not overwrite profiles using latest-device-wins. Future receipts, cursors, tombstones, account isolation and conflict UX are detailed in [offline expansion](08-offline-clients-and-expansion.md); implementing those mechanisms is Phase 4 work.

## 10. Completion boundary for this document

This change records decisions, contracts and implementation defaults. It does not add database migrations, endpoints, exercises, learner UI, tests, or running services. [Delivery and verification](11-learning-delivery-plan.md) defines what a subsequent implementation must prove. The older full-product documents remain useful for reflection/experience and later expansion, but do not override the learning phases or require AI/offline functionality before Phase 1 can ship.
