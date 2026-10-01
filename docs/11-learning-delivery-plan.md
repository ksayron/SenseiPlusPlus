# Learning delivery plan and verification

**Decision date: 24 September 2026. Status: documentation delivered; application implementation pending.**

The authoritative behavior is in [learning architecture](09-learning-system.md) and [learner experience](10-learning-experience.md). This document translates it into ordered implementation increments, fixture requirements and acceptance evidence. It does not create GitHub issues, schedule work, or claim any application feature is complete.

## 1. Scope and prerequisites

Implement the Phase 1 learning foundation in the existing web/backend project. Keep subject names in data. C# fundamentals demonstrate behavior; do not delay the engine for a comprehensive curriculum or add a content-authoring product.

Use the current concept IDs, owner boundary, PostgreSQL unit of work, ETag/If-Match transport, cursor lists and module dependency rules. Preserve existing evidence and experience data. Read project instructions and recheck the working tree before implementation; this inventory is based on the checkout inspected on 24 September 2026.

The local development owner header is not production authentication. Learning can be implemented and verified locally without making the application publicly accessible. Real authentication is a prerequisite for any non-local release. No paid service or configured AI provider is needed to prove Phase 1.

This is a dependency-based plan, not a calendar promise. Dates, development throughput, later platform choices and future content-production capacity are not blockers for the documentation or the first engine implementation.

## 2. Ordered increments

| Increment | Work | Dependency | Completion evidence |
| --- | --- | --- | --- |
| L1: contracts and persistence | Extend concepts with relations; define consumable material/exercise versions/families, session/result/event identities, goals and roadmap records; additive migrations and validated fixtures | Current backend conventions | Migrate an existing database without changing concept IDs or losing history; reject malformed fixture definitions and invalid graphs |
| L2: topic workspace | Browse/search concepts; directly address overview/material/practice/activity sections; safe material renderer; scope/type/count setup and empty states | L1 read contracts | Two unrelated subjects render through the same components; unsupported blocks cannot execute code; available-count preview is truthful |
| L3: exercise engine | Six accessible interaction types, deterministic evaluators, safe presentation/feedback separation, optional ungraded notes | L1, L2 shell | Accepted alternatives pass; wrong answers score 0; invalid payloads create no attempt; no pre-answer key leakage |
| L4: durable sessions | Pinned selection, one resumable session, draft saves, assistance, immutable attempts, receipts, lifecycle, immediate feedback and summaries | L3 | Refresh/resume, early exit, duplicate submission, lost response and two-tab conflict scenarios preserve acknowledged work |
| L5: evidence integration | Structured trusted observations, host adapters, same-transaction projection, objective policy, cooldown admission, evidence history and status changes | L4 | Rollback leaves no partial result/evidence; repeated families do not inflate breadth; concurrent self-review cannot bypass limits |
| L6: recommendations and roadmaps | Explicit goals, policy-based daily/scoped selection, enrollment, advisory prerequisites, traversal and evidence coverage | L5 | Free practice changes shared roadmap coverage; manual skip/read/self-review cannot manufacture demonstrated progress |
| L7: complete verification | Contract/OpenAPI checks, backend and client checks, interaction automation, browser walkthroughs, documentation alignment | L1-L6 | Required acceptance scenarios below pass with evidence; remaining limitations are stated explicitly |

Early increments may expose raw activity before the scored projection exists. Do not ship misleading placeholder estimates while waiting for L5. The complete Phase 1 release includes all seven increments; an intermediate slice is not the finished milestone.

### Cross-cutting implementation responsibilities

- Keep evaluation and projection policy deterministic and clock-injectable so tests can exercise time boundaries without sleeping.
- Keep Learning/Evidence integration in host adapters and shared transactions. Do not weaken module dependency tests to take a shortcut.
- Add owner-consistent source validation and database constraints; UI checks are not authorization or uniqueness guarantees.
- Keep command receipts stable across retries. A result's identity, the current session ETag and the receipt's historical response version have different purposes.
- Update API documentation and frontend transport types with the implementation. Do not change the generated OpenAPI snapshot merely to describe unimplemented routes.
- Avoid a new broker, generalized plugin loader, arbitrary code runner, content editor, or full event-sourcing framework.

## 3. Test-content requirements

Use a small, explicitly labeled fixture library. Fixture completeness means coverage of behavior, not a finished course.

### C# fixture roadmap

Create data-defined stages covering value/reference semantics (including structs/classes), OOP, and delegates/events. Use canonical concept IDs and relationships; title text is replaceable. Include optional material and all six exercise types somewhere in this fixture library.

At least one roadmap node must have enough distinct unassisted objective exercise families to meet the default requirement across two sessions. Another must intentionally lack enough families so the insufficient-coverage state is demonstrable. Provide variants within at least one family to verify that switching variants does not create extra independent evidence.

A separate synthetic subject must use unrelated names/keys and reuse the same graph, material blocks, exercise interfaces, selection and roadmap behavior. It verifies engine independence without expanding C# lessons into a production content project.

### Required fixture cases

| Fixture | Purpose |
| --- | --- |
| Material with all supported block types | Headings, prose, lists, code, image alt text and links render safely |
| Topic with material but no exercises | Readable content and explicit practice unavailability |
| Topic with exercises but no material | Practice remains available |
| Fewer eligible families than requested | Honest session downsizing without repeats |
| Two accepted orderings/mappings | Valid alternatives do not fail a single-key assumption |
| MultipleChoice with multiple correct options | Exact-set evaluation; selecting every option is not automatically correct |
| BugSpotting with No issue as correct | Prevent a universal hidden-bug assumption |
| Flashcard with optional written note | Reveal/rate flow and no free-text grading |
| Same family across exercises/versions | Repetition updates one family's contribution without inflating breadth |
| Superseded content version | Existing session remains pinned while new sessions use the replacement |
| Unsupported schema, invalid IDs, unsafe content | Reject or clearly report unusable content without script execution |
| Disabled exercise and missing asset | New selection avoids unusable content; history remains recoverable |

Review authored technical answers before using them to make claims about actual users. Automated fixture passing proves deterministic behavior against the fixture definition; it does not establish the educational validity of every answer.

## 4. Acceptance matrix

### Domain and policy checks

| ID | Scenario | Expected result |
| --- | --- | --- |
| D01 | Add self-edge, duplicate relation or prerequisite/containment cycle | Rejected; existing graph unchanged |
| D02 | Two concurrent graph mutations jointly form a cycle | At most one commits; no cyclic graph persists |
| D03 | Correct, incorrect and accepted-alternative responses for each objective type | Exact binary result; valid alternative accepted |
| D04 | Unknown option, duplicate IDs, incomplete mapping/order or No issue plus findings | Validation error; no scored attempt/result/receipt of success |
| D05 | Free-text note resembles correct answer but objective choice is wrong | Objective result remains wrong; note ungraded |
| D06 | Latest eligible attempts span more than 20 families | Only newest 20 distinct families contribute, with deterministic tie-breaking |
| D07 | Correct answer follows hint/reference or answer reveal | Hint/reference weight is 0.5; revealed retry is excluded from assessment |
| D08 | Only self-reviews exist | Assessed estimate unknown; history retained |
| D09 | Objective 0 from one unassisted family plus admitted Recalled | Estimate uplift capped at 0.05 |
| D10 | Four correct/one incorrect unassisted families plus admitted Recalled | Estimate is 4.1/5.1; no accumulated prior self-review weight |
| D11 | Recalled just before, exactly at and after the 72-hour boundary | Before blocked; exact/after eligible; authoritative time used |
| D12 | Recalled when estimate is already 1 or no objective evidence exists | Admission still consumes allowance; no fabricated gain/assessment |
| D13 | Again/Partly or repeated blocked Recalled | Activity saved; no automatic negative assessed result or positive gain |
| D14 | Qualifying evidence disputed/withdrawn/superseded | Projection excludes it while preserving attempt; cooldown not reset |
| D15 | Replay/rebuild after blocked self-reviews and evidence correction | Original admissions retained; no retroactive new gains |
| D16 | New exercise variant from same family | Independent family count unchanged |
| D17 | Roadmap requirement met only through self-review, assisted work, or retries | Requirement remains unmet |
| D18 | Objective evidence meets 3-family, 2-session, 80% requirement | Evidence requirement met with links to qualifying observations |
| D19 | Available library cannot support the family requirement | Insufficient exercise coverage, not a misleading mastery gate |
| D20 | Request EndOfSession in Phase 1 | Explicit unsupported-mode validation; no silent immediate-mode substitution |

### Persistence, contracts and trust

| ID | Scenario | Expected result |
| --- | --- | --- |
| P01 | Apply additive migration to pre-learning database | Existing concepts/evidence/experience survive; legacy observations remain unscored |
| P02 | Two owners access direct/nested session, result, goal, enrollment and profile IDs | No cross-owner reads or writes |
| P03 | Manual evidence declares an arbitrary trusted Attempt source | Cannot enter the scored projection through a source label alone |
| P04 | Same operation/payload submitted twice, including after session version advances | One result/contribution; original receipt replay succeeds |
| P05 | Same operation ID with a different answer, action or target | 409; original accepted result unchanged |
| P06 | Missing, malformed and stale If-Match | Distinct 428, 400 and 412 Problem Details responses |
| P07 | Fault after saving answer but before evidence/session/receipt commit | Whole transaction rolls back; no partial success |
| P08 | Commit succeeds, response disappears, client retries | Exactly one accepted result and cooldown admission |
| P09 | Two simultaneous positive reviews touch the same owner/concept | One new admission within the interval; both valid activities may be retained |
| P10 | Two simultaneous starts for one owner | At most one Active/Paused session |
| P11 | First allowance row created concurrently, or overlapping multi-concept cards | Constraints/locks preserve per-concept limits without partial observations |
| P12 | Two tabs save against the same session version | One stale mutation fails; no lost acknowledged edit |
| P13 | Content changes between preview/start or during active session | Start returns actual count; active session stays on pinned versions |
| P14 | Fetch learner presentation before submit/reveal | No answer keys, private evaluator definitions or premature feedback |
| P15 | Owner changes while a request/receipt is pending | Prior-owner data is neither displayed nor applied under the new owner |
| P16 | Policy rebuild after process restart | Same inputs/admissions produce same projection and provenance |

Use PostgreSQL integration tests for locking, uniqueness, FK ownership, migration and rollback assertions. An in-memory repository cannot establish these guarantees. Existing architecture tests must continue passing unchanged in principle.

### Client and browser checks

| ID | Scenario | Expected result |
| --- | --- | --- |
| U01 | Cold start with no goals/history | Browse and mixed practice work; unknown is not labeled zero/beginner |
| U02 | Direct URL to each topic section; Back after session | Section/query/context restored |
| U03 | All six exercise flows | Correct controls, validation, saved notes and authored feedback |
| U04 | Ordering/matching by keyboard and touch | Full operation without drag-only controls |
| U05 | Hint/reference/reveal request fails | Error visible, same operation retry, no falsely recorded saved assistance |
| U06 | Autosave acknowledgements arrive after further typing | New text remains unsaved until its own acknowledgement |
| U07 | Pause, refresh and resume | Last acknowledged draft and item restored |
| U08 | Save failure during navigation or abrupt close | Honest last-saved status; no Phase 1 offline durability claim |
| U09 | Final answer and early exit | Feedback remains until Continue; partial summary leaves unvisited work unassessed |
| U10 | Retry after feedback | Original answer retained; retry labeled answer-aware |
| U11 | Request ten but only four eligible families exist | Actual count shown; no silent repetition |
| U12 | Free practice, then another roadmap | Shared coverage visible; traversal does not fabricate visits/skips |
| U13 | Malicious/unsupported content blocks and missing images | No execution; understandable missing-content state; usable remaining text |
| U14 | AI unconfigured | Every Phase 1 flow completes without an AI call |
| U15 | Desktop and narrow-screen walkthrough | Readable code, no page-wide overflow, accessible panels/focus, text status indicators |

## 5. Verification commands and evidence to retain

For later application implementation, use the repository CI shape, adjusting only for intentionally changed dependencies. These commands are not necessary for this documentation-only change:

```powershell
dotnet restore backend/Sensei.sln --locked-mode -m:1
dotnet build backend/Sensei.sln --configuration Release --no-restore -m:1
git diff --exit-code -- docs/api/openapi-v1.json
dotnet test backend/Sensei.sln --configuration Release --no-build
```

From the client directory:

```powershell
npm ci
npm run lint
npm test
npm run build
```

If implementation changes the public API, intentionally regenerate and review the OpenAPI snapshot first; then verify regeneration is stable against that reviewed snapshot. A changed API is not expected to match the pre-feature snapshot. Integration tests require a working PostgreSQL/Testcontainers environment. Record environmental failures separately from test assertions; do not report blocked execution as a pass.

Add browser interaction coverage for complete learning journeys rather than only API mocks or implementation-shaped component tests. Retain the tested viewport/input mode, build revision, scenario IDs, and any failures. Manual visual inspection remains necessary for narrow/desktop layout and feedback focus behavior.

Record relevant operational measurements during implementation: submission latency/failures, stale conflicts, duplicate-operation replay, projection failures, cooldown admission and session recovery. Logs identify operation/result IDs and safe error categories; do not log raw answers, notes, keys or private content. This is diagnostic support, not a new analytics service or an efficacy study.

## 6. Later phases and compatibility gates

| Phase | Must reuse | Gate before claiming completion |
| --- | --- | --- |
| 2 | Concepts, exact result provenance, assistance, family identity, policy versions | Dimension-specific evidence rules, schedule/decay tests, diagnostic behavior and explainable recommendations; estimates remain appropriately qualified |
| 3 | Shared item/session engine and feedback-policy contract | Deferred responses reveal no interim answers/scores; intensive formats have their own explicit evaluator and interaction policies |
| 4 | Operation/event IDs, immutable content versions, owner-scoped receipts and server-authoritative policy | Download/restart/offline practice/restart/reconnect on a target device; duplicate, conflict, clock-skew, deletion and account-switch checks |
| 5 | Generic signal contract and typed provenance | Actual provider reports cannot mint broad knowledge or disclose raw private source by default |
| 6 | Ordinary exercises, hints and observations | Provider absence does not disable core learning; free-only/privacy rules hold; AI assessment is attributed and contestable |

Future implementations must not reuse the Phase 1 self-review cooldown as a spaced-repetition algorithm. A cooldown controls evidence gain; a review schedule recommends when to practice. These remain different policies.

## 7. Documentation delivery and decisions register

This change delivers:

- [System architecture and contracts](09-learning-system.md).
- [Learner experience](10-learning-experience.md).
- This delivery/acceptance plan.
- [Unchanged supplied input](inputs/learning-system-specification.md).
- Updated index and older documents with explicit learning-phase precedence and current HTTP conventions.

The source input is a historical proposal; conflicting examples and phase wording remain untouched there. The decision table in the architecture document records how the discussion refined it. The older product roadmap retains valuable reflection/experience/native/enterprise planning but does not make AI, offline or work-reflection delivery a prerequisite for learning Phase 1.

Implementation defaults are distinguished from confirmed product decisions. The initial selection weights, 750 ms draft debounce, evidence window, weighting/caps and coverage thresholds are centralized defaults, to be validated through the tests above. Changing a default that affects evidence requires policy-version handling; changing subject content must not require engine changes.

No unresolved content-sourcing decision blocks this implementation design. Production authoring/import, curriculum breadth, native-client framework validation, exact Phase 3 formats, and Phase 4 retention/sync details belong to their separate efforts. This documentation does not silently approve them as Phase 1 scope.

### Validation of this documentation change

Validate local Markdown links, table/fence structure, decision coverage, source-copy hash, whitespace/diff quality, and the documentation-only file boundary. Do not run code generation, migrations, or application builds merely to publish this design. Application test matrices above describe future work and must not be reported as already passing.
