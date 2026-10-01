# Roadmap, feasibility questions and requirement coverage

## Current learning roadmap — 24 September 2026

The learning phases are governed by [learning system](09-learning-system.md), [learning experience](10-learning-experience.md), and [learning delivery/verification](11-learning-delivery-plan.md). Those documents supersede the original learning sequence below. Phase 1 is delivered locally; the [implementation record](13-learning-implementation.md) captures its behavior, checks and limits. Later phases remain planned.

| Learning phase | Delivery boundary |
| --- | --- |
| 1 | AI-independent web learning: topic workspace, six exercise types, 3/5/10-item sessions, immediate feedback, goals, evidence summaries, basic selection/estimate and one test roadmap |
| 2 | Spaced repetition, decay, dimensions, diagnostics, weak-area sessions, priorities, recommendation explanations and generic learning-signal contracts |
| 3 | Intensive exercises and exam/deferred-feedback mode |
| 4 | Working offline packs, local execution/projection, durable events and multi-device synchronization |
| 5 | Actual agent, commit, IDE and other signal-provider integrations |
| 6 | Optional learning AI using ordinary platform objects and the free-only policy |

The engine is subject-independent; C# fundamentals are test fixtures only. Production content sourcing/authoring is a separate concern. Native, reflection, experience and enterprise tracks remain broader product work, not hidden prerequisites for Phase 1. In particular, selecting a live AI route and passing an airplane-mode demo do not gate the first learning release.

## Delivery rules

This remains a design package; the repository now contains application foundations. The creator reports proficiency from about a year on a large ASP.NET Core microservices/RabbitMQ/PostgreSQL project. Plan professional solutions accordingly. Exact checkpoint dates, weekly availability and university rubric remain open. Confirmed constraints are local deployment, no paid dependencies, English first with later languages, and eventual useful offline travel learning. AI is optional.

The immediate priority is the learning loop described above. The broader learning/work/experience loop remains a product objective. Tests protect authority, provenance and recovery rather than reproduce implementation details. Phase 1 tests need no model; selected live free-model evaluation belongs to optional-AI work with its own quota/disclosure limits.

## Broader delivery windows

**First demonstrable learning version:** complete learning Phase 1 and its L1-L7 acceptance gates. A small fixture library is sufficient. The broader graduation/demo boundary should be forecast from actual throughput; the former requirement to include O1 disconnected learning in the first checkpoint is superseded by learning Phase 4.

**Later delivery:** sequence learning Phases 2-6 and broader reflection/experience/native/organization tracks by their actual dependencies. Broader platform coverage, federation/provisioning and infrastructure extraction follow concrete use cases. Hosting stays conditional on its own budget/authorization. Do not reintroduce the historical milestone catalog as an obligation to implement all tracks with Phase 1.

After the first two completed increments, use actual throughput, unresolved quality issues and weekly availability to forecast the remaining tracks. Keep a demonstrable core branch/release at the checkpoint while expansion continues. Professional architecture is compatible with staged delivery; no premature claim that one developer will finish every enterprise feature on an unverified schedule is needed.

## Historical whole-product milestone catalog

The M0-M5/O1/N1/I1/E1/E2/H1 labels below preserve the original full-product proposal and its acceptance mappings. They are not the current learning implementation order. In this catalog, AI pilot requirements apply only to optional inference, scheduled review maps to learning Phase 2, O1 maps to Phase 4, and provider integrations map to Phase 5. Use L1-L7 in the new delivery plan for the next release.

| Milestone | Deliverable | Depends on | Definition of done |
| --- | --- | --- | --- |
| M0: establish feasible baseline | Local setup design realized; one approved free route; English message catalogs; first curated mechanism and fixture pack | This design and eventual implementation request | Local app/database start with no paid dependency; API key stays server-side; free-only policy blocks nonzero/unknown-price routes; 12-example route pilot records quality, privacy, parsing and latency; selected route or honest limitation documented |
| M1: smallest connected learning slice | One lesson and scenario, durable answer/feedback, evidence profile, review occurrence, manual personal-project entry and one approved story export | M0 | A student completes the loop without a job/repository; refresh/crash preserves input; delayed changed scenario links the original evidence; manual entry/story works without AI; two accounts cannot read one another |
| O1: required offline learning and sync | Downloadable pack, PWA local database, authored offline guidance, operation outbox and owner-scoped sync feed | M1; operation IDs/content versions designed in M0 | Airplane mode with backend stopped: download/relaunch/learn/save/relaunch/reconnect; no lost or duplicate attempts; local self-review distinguished; conflict/delete/cursor-reset tests pass |
| M2: work reflection and acknowledgement | Manual episode/excerpt, disclosure preview, context correction, 2–3 questions, help/pause/dispute, version-bound acknowledgement | M1 | Scenarios A/B/E/F pass; uncertainty remains visible; stale acknowledgement requests fail under race; hints/revisions are distinct; unavailable provider does not lose input |
| M3: reuse work and experience | Reflection-derived drafts, factual approval, search, reviewer-note and interview-story previews, contribution/outcome fields | M2 | Same episode produces approved journal revision and related practice without re-entry; exported artifact pins approved facts; tiny diff/non-code entry retains meaningful contribution; changing facts creates new approval boundary |
| M4: breadth, privacy and defense-ready demo | Six curated mechanisms, queue/profile refinements, account export/deletion, restore guide, feedback evaluation and peer walkthrough | M1–M3 and O1 | All 13 original acceptance criteria, A–F and offline scenario G pass; deletion does not resurrect through job/restore/device sync; no private answers in exports; measured live/fixture results and limitations |
| N1: Android and Windows clients | MAUI candidate validated on real Android device and Windows, SQLite offline learning, PKCE sign-in/sync, downloaded journal rehearsal | M1/O1; native auth and packaging spike | Android then Windows deliver the same offline protocol/authority checks; upgrades preserve pending attempts; native keyboard/accessibility and sign-in verified on both targets |
| M5: local CLI | Developer-triggered selection and sanitized event import, stale context detection | M2–M3 and integration spike | No broad repository upload; exact staged/selected context labeled; no automatic Git writes; repeated import deduplicates; offline/stale behavior visible; client-reported evidence distinguished |
| I1: first IDE action | Thin UI invoking the shared local core; selected context, start/resume and sanitized preview | M5 protocol stable; IDE chosen from actual user workflow | No duplicated domain logic or automatic Git writes; cancellation/authentication/reconnect work; installable and demonstrable in one target IDE |
| E1: initial organization features | Teams/membership, content curator/admin roles, team learning paths and explicit artifact-revision sharing | M3, identity/authorization boundary, sync scope tests from O1 | Member/admin cannot inspect private attempts; revoked grants stop online access; personal history survives leaving an organization; offline restrictions disclosed |
| E2: enterprise depth and additional clients | Further native/IDE platforms, federation/provisioning, policy/audit controls and evaluated aggregate trends as selected | N1/I1/E1 and actual priorities | Each selected capability has an acceptance scenario and access/recovery tests; no universal compliance or anonymity claims |
| H1 future: hosted demonstration | Chosen host, credentials/registration/recovery, TLS, off-host backups, cost policy | M4 and explicit hosting/budget decision | Restore tested, live host isolated, budget/account eligibility verified, free-quota limitation accepted or a separately authorized paid policy adopted |

Historical M1 joined multiple now-separated delivery concerns. Current learning Phase 1 establishes fact/content contracts; Phase 4 implements O1 offline functionality. M2-M4 retain reflection, factual approval, security and broader learning goals. Native/tooling/organization work consumes those foundations in later selected tracks. See [offline/expansion design](08-offline-clients-and-expansion.md).

## Risk-focused tests

This table covers the full target product. The Phase 1 release gate is the narrower [learning acceptance matrix](11-learning-delivery-plan.md#4-acceptance-matrix); AI worker, synchronization and organization scenarios gate their corresponding later phases.

| Risk | Meaningful test |
| --- | --- |
| Owner isolation | Two accounts attempt direct IDs, nested source links, search, job status, exports, approval and background callbacks; reject all cross-owner access |
| Acknowledgement race | Concurrent context update and old-bundle acknowledgement; acknowledgement either records the exact old historical version or fails, never approves new input |
| Approval integrity | Edit role/impact after approval; verify new draft and stale artifact warning; fabricated generated metric cannot become confirmed through completion/export alone |
| Crash and retries | Stop worker after input commit, after model response and after result commit; recover without answer loss or duplicate observations/drafts; stale lease cannot commit |
| Dispute/correction | New assessment revision supersedes old active evidence without rewriting attempt, assistance or prior acknowledgement bundle |
| Deletion | Delete source/account while job is running; late response ignored; exports/indexes/projections purged or invalidated; restore reapplies deletions |
| Prompt/schema attacks | Hostile instructions in code/answers, invalid reference IDs, unsafe Markdown, malformed/refused/truncated output; no rights change or accidental approval |
| No spending | Paid/unknown-price model, fallback, plugin and endpoint config rejected; quota exhaustion pauses; no hidden SDK retries or automatic top-up |
| UI resilience | Refresh, disconnected backend, missing AI, two tabs, save failure; clear saved/unsaved state and safe resume |
| Offline operation and sync | Real-device airplane mode/restart, pack update interruption, duplicate push, concurrent drafts, clock skew, expired cursor and token; preserve unsynced data and distinguish self-review |
| Device/organization isolation | Switch accounts with pending operations; revoke grant/membership during disconnect; no old-user uploads or automatic manager access after reconnect |
| Localization foundation | Missing message keys fail checks; stable IDs independent of labels; English content fallback explicit; no claims of Russian grading quality before evaluation |

Use unit tests for deterministic transitions/scheduling/canonicalization; PostgreSQL integration tests for foreign keys, transactions and leases; a few browser E2E tests for complete journeys; reviewed live fixtures for assessment quality. Do not use an in-memory database to claim PostgreSQL concurrency is verified. CI can run offline fixture tests without a provider key.

## Consequential questions and bounded spikes

These are retained expansion questions. The former free-route-first ordering is superseded: no provider spike blocks Phase 1. React is already selected and implemented; no frontend framework comparison is needed for this learning milestone. Content supply and native/exam/offline details remain separate future efforts.

| Question / spike | Small experiment and exit criterion | Decision if it fails |
| --- | --- | --- |
| Free route suitability (first) | Compare up to two current free endpoints on 12 reviewed English examples each; record supported schema mode, refusal/repair rates, latency, quotas, policies and actual route IDs | Select another compatible free route or narrow curriculum/contextual grading; retain manual/curated modes; never enable paid fallback |
| Privacy-compatible free route for real material | Check endpoint policy/capability intersection with restricted-data settings, without sending employer material | Keep live prototype public/synthetic; retain private entries locally; do not imply confidential use is supported |
| Input adequacy | Try a description-only episode, tiny selected diff and non-code investigation; peer rates relevance of 2–3 questions | Improve intake questions and narrower claims; do not add repository ingestion by default |
| Job implementation | Kill/restart two workers around claim, inference and commit, including cancellation/deletion | Fix fencing/idempotency or adopt maintained scheduler; do not invent a generic distributed queue |
| Frontend choice (only if developer familiarity warrants) | Build the same save/resume form and transcript in preferred candidate; assess developer effort and recovery behavior | Choose Blazor if it materially reduces effort while preserving API separation; no parallel full applications |
| Learning schedule usefulness | Try paired explanation/changed-scenario examples; inspect assisted versus unaided schedule outcomes with a few peers | Tune heuristic intervals/queue; FSRS experiment only for stable recall items, without universal competence claims |
| Offline contract (M0–O1) | Download one pack and complete a restarted disconnected session; replay twice, introduce competing edits/deletion and force cursor reset | Fix storage/sync invariants before adding more clients; narrow pack breadth, not the required offline journey |
| Native client suitability | Android + Windows confirmed; MAUI real-device SQLite/sync/input/PKCE/packaging spike compared with React Native only if necessary | Select better-supported client stack from observed results; preserve shared protocol; do not silently replace native scope with PWA |
| CLI and IDE entry | Select staged/unstaged changes in a disposable public repo, re-stage/edit/rebase, preview sanitized import, test offline behavior; pick first IDE | Move its delivery to Window B if needed; retain shared core/protocol in the design |
| Dates/rubric/availability | Confirm exact first-checkpoint/final dates and available weekly time within the stated two windows | Forecast after completed increments; move expansion between windows without losing required core/offline behavior |
| Enterprise priorities | Demonstrate team content and voluntary sharing under revoked membership before selecting federation/provisioning/analytics work | Keep E1 bounded; postpone specific E2 capabilities rather than removing organization architecture |
| Hosting later | Verify jurisdiction/account eligibility, region, expected usage, backup access and actual budget | Remain local until constraints are resolved; retain provider abstraction |

No model-inference hardware spike is required: the final direction uses external inference. Native builds and real-device testing do need a target-platform/toolchain check. No university rule, employer authorization, quota increase or exact launch date is assumed. Package patches, polling defaults and the initial scheduling heuristic are reversible implementation decisions.

## Practical evaluation

For the first learning milestone run the domain, persistence and learner journeys in the new delivery plan with AI unconfigured. The wider evaluation below applies when its associated features exist.

Run A–F and offline scenario G end-to-end, with a recorded fixture-backed demonstration plus selected live free-model calls clearly labeled. Ask roughly 3–5 willing peers if available to try a learner or work scenario using nonconfidential material; this is a proposed convenience sample, not a recruitment prerequisite. Ask one experienced peer to review examples if available; otherwise identify the creator as the reviewer and document the limitation.

Record time to useful output, irrelevant questions, substantive feedback errors/disputes, interruption recovery, reasons for abandoning, entry reuse and quota/latency. An optional later unaided changed scenario illustrates an observed outcome; report sample size and assistance without claiming causal learning benefit. Generated word count, XP and streaks are not success metrics.

## Acceptance-criterion mapping

Numbers below refer to product brief section 17 and the historical milestone catalog; these are future whole-product checkpoints, not current learning release gates or claims of completed tests.

| Criterion | Design mechanism | Completion milestone |
| --- | --- | --- |
| 1. Learning without workplace material | Curated independent learning session | M1 |
| 2. Inspect/correct task interpretation | Versioned context and interpretation confirmation | M2 |
| 3. Context-grounded or labeled-general questions | Question evidence links and relevance validation | M2 |
| 4. Attempts/hints/revisions distinct | Immutable attempt chain and hint-use events | M1–M2 |
| 5. Unresolved uncertainty allowed | Explicit unknown/dispute states and honest closure | M2 |
| 6. Explicit version-specific acknowledgement | User command plus concurrency guard and review bundle | M2 |
| 7. Episode → approved experience + later learning | Shared provenance, draft approval and evidence-based practice | M3 |
| 8. Later related reasoning rather than repetition | Curated scenario families and changed conditions | M1, broadened M4 |
| 9. Exposure separate from understanding | Typed evidence and inspectable profile projection | M1–M3 |
| 10. No fabricated confirmed impact | Versioned role/outcome fields and factual approval | M3 |
| 11. Private attempts isolated from users/exports | Owner authorization and artifact-specific projection | M1, hardened M4 |
| 12. Recover without loss/duplicates | Atomic input/job save, leases, fences and unique result keys | M1–M2 |
| 13. Reuse approved entry | One story preview/rehearsal/export flow | M1 minimal, M3 complete |

## Scenario and architecture-direction mapping

| Source | Design and demonstration |
| --- | --- |
| A: AI-assisted background change | M2 reflection with unverified lifetime/failure boundary; M3 draft and later practice; unresolved questions survive acknowledgement |
| B: One-line investigation fix | M2 description-first intake and evidence questions; M3 contribution/outcome capture independent of diff size |
| C: Student messaging lesson | M1 standalone teaching/answer/feedback; M4 changed scenario; project setting is never employment by default |
| D: Explain past reliability experience | M3 approved revision → editable story/rehearsal; unknown impact stays unknown |
| E: Change after acknowledgement | M2 immutable snapshot and stale version checks; web limitation visible; M5 local manifest comparison |
| F: Provider outage or wrong feedback | M1 durable input/quota pause; M2 disputes/supersession; M4 crash/delete tests |
| G: Offline trip (new user requirement) | O1 pack download → airplane mode → app restart → lesson/variant/self-review → saved attempts → reconnect/deduplicated sync; N1 repeats on native device |
| Shared professional model; individual first | Evidence/profile module consumes learning and approved experience; no required organization; future clients use stable API |
| Modular monolith and domain events | One host/database; module services and transactional notifications; durable AI work with no broker |
| Code need not reach backend from local helper | M5 sanitized import contract; source context stays local; any external inference is separately disclosed |
| Organizations must not expose private learning | E1 membership/content/sharing with explicit artifact revision grants; no manager access inherited from funding; E2 deeper enterprise capabilities |
| Language follow-up | English M0–M4, external message catalogs and content locale/version fields now; Russian evaluation later |
| Free configurable AI follow-up | M0 route configuration, strict zero-spend control, quota-aware jobs and fixture development |
| Experience and delivery-window clarification | Professional module/worker/message boundaries; Window A protected core, Window B planned expansion; accelerate N1/M5/I1/E1 when prerequisites and capacity permit |

## Scope cuts, in order

For current learning Phase 1, keep production content breadth, authoring tools, scheduling/decay, exams, native clients, sync, integrations and AI out of the release rather than treating them as unfinished Phase 1 requirements. Preserve their contract boundaries. Later full-product scope can reduce platform/content/export breadth before weakening data integrity and user authority.

Do not cut durable acknowledged Phase 1 answers, truthful evidence/assistance, owner isolation, retry safety or recoverable save failures. Offline remains a real Phase 4 obligation, not a Phase 1 claim. Correction, acknowledgement, factual approval and experience reuse retain their own broader-product authority guarantees. Add RabbitMQ or distributed deployment only for an actual independent-consumer requirement.
