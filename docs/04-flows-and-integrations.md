# User flows, interfaces and integration direction

**Learning update, 24 September 2026:** [Learning experience](10-learning-experience.md) is authoritative for the next milestone. The broader reflection/journal/integration journeys below remain future product design. Learning uses no AI in Phase 1; offline storage/sync is Phase 4.

## Navigation and interaction

The original product labels were Home, Learn, Work, Journal and Profile. The current client uses **Today, Learn, Reflect, Evidence, Experience**; preserve those non-learning destinations during this milestone. Career reuse starts from Experience; do not add an empty career platform. Learning adds directly addressable topic/session/roadmap screens.

| Surface | Main action and visible information |
| --- | --- |
| Onboarding | Optional goal/familiar topics; no job/Git link, diagnostic or AI setup required for core learning |
| Home | Resume a session, open learning or record an episode |
| Learn | Topic workspace with overview/material/practice/activity; 3/5/10-item sessions with immediate feedback |
| Offline library (Phase 4) | Download complete practice packs, verify ready status, see storage/last-sync state, and resume learning without backend access |
| Work editor | Intent, constraints, contribution and optional selected excerpt; explicit AI request preview; retain a manual-only path |
| Reflection workspace | One question at a time, exact context version, progress such as 2 of 3, saved-answer status, help/skip/pause/disagree |
| Completion | Checked topics, initial/assisted attempts, unresolved/disputed items and context version; acknowledgement is a separate button |
| Journal | Manual and reflection-derived entries, draft/approved states, search/filter, editable contribution/outcome, source links |
| Profile | Separate exposure, explanation/scenario observations, recency and gaps; each item opens its provenance |
| Story preview | Select one approved entry, inspect factual basis, rehearse one or two questions, edit and export a concise story |
| Settings | Language preference architecture, privacy/disclosure, data export/delete, configured AI route and quota state |

Use text-first structured forms with optional Markdown preview and a basic diff viewer. Avoid a full IDE/editor, WYSIWYG document suite or large graph canvas. Keyboard navigation, semantic labels, focus after errors and text status updates are baseline acceptance checks. Do not rely on color to convey assistance or approval. On narrow screens, context appears in a drawer rather than crushing the answer area.

## Complete learning-only journey

Experienced developer explores a topic → optionally reads material → chooses 3, 5 or 10 exercises → submits an objective answer or revealed flashcard self-review → server commits result/evidence → immediate authored feedback is shown → learner explicitly continues → summary/history and shared roadmap coverage update. Topic names are content data; C# fundamentals are the first test fixture only.

No employment or work entry is required. Optional reasoning is stored but ungraded in Phase 1. Self-review is labeled and subject to the owner/concept 72-hour gain cooldown. Missing AI cannot leave objective feedback pending. Scheduling and changed-scenario reminders begin in Phase 2; semantic free-answer assessment is later work.

Phase 4 travel journey: download a pack while connected → reopen PWA/native app in airplane mode → complete a lesson and changed scenario → use authored hints and reference/self-review → save attempts locally across restart → reconnect → sync once → optionally select attempts for later AI feedback. This is a future acceptance target, not implemented Phase 1 behavior. See [offline clients](08-offline-clients-and-expansion.md).

## Integrated work journey

Developer records a background-processing change with stated purpose and contribution → previews selected material and provider recipients → corrects the application's proposed interpretation → answers a few grounded questions → records an unverified lifetime assumption and asks for help → sees unresolved issues on completion → explicitly acknowledges the exact discussed version or leaves it incomplete → separately reviews a journal draft → approves a factual revision → practices a related concurrency scenario later → uses the entry to rehearse an interview account.

The app can suggest an exposure tag from the approved entry while retaining unresolved thread-safety understanding. There is no requirement to claim the code is safe. A one-line fix can have a rich investigation story, and non-code mentoring/design episodes use the same journal path. If the selected free endpoint is unsuitable for the material, keep the entry local and use a sanitized/general scenario; do not force source upload to complete the product loop.

## Presentation journey

Select an approved reliability entry → show context, personal contribution and known/unknown outcome → offer one concise interview-story draft → let the user rehearse why an alternative was rejected → edit the wording → approve the artifact revision → download Markdown or plain text. The reviewer note is the same lightweight preview/export component with a different template and deliberately selected content.

Numbers and role claims must originate in approved fields. If outcome is unknown, the story says so or omits impact. New substantive claims added in the artifact editor must be flagged for explicit confirmation and linked to a new source revision before export; stylistic edits alone do not change source facts. Do not attach private question history or links that expose the full session.

## Client/server boundary and recovery

For current learning use the [planned learning API](09-learning-system.md#8-proposed-api-surface): synchronous deterministic submission, server draft saves, strong If-Match, stable operation IDs and receipt replay. The table below concerns later reflection/AI/artifact workflows. Public version guards use ETags; internal numeric expected versions are not request-body contracts.

| Operation | Contract outline | Behavior |
| --- | --- | --- |
| Save context | `POST /episodes/{id}/snapshots` with episode `If-Match` | New immutable snapshot; old acknowledgement remains historical |
| Confirm interpretation | `POST /sessions/{id}/interpretation-confirmations` | Confirm exact revision, not any later model text |
| Submit reflection answer | `POST /sessions/{id}/attempts`, idempotency key and `If-Match` | Commit input first; return `202` with job ID only for queued optional assessment |
| Track work | `GET /jobs/{id}` and `GET /sessions/{id}` | Owner-checked status and durable state; polling is replaceable by SSE later |
| Dispute | `POST /feedback/{id}/disputes` | Retain original feedback, capture missing constraint and correction lineage |
| Acknowledge | `POST /sessions/{id}/acknowledgements` | Explicit user action, reviewed bundle and source version required |
| Approve journal | `POST /entries/{id}/revisions/{version}/approval` | Version guard; model cannot invoke it |
| Export story | `POST /artifacts/{id}/revisions/{version}/export` | Whitelisted projection of exact reviewed version |
| Delete/export account | Dedicated owner-authenticated requests | Reauthentication, status and clearly stated scope |

In Phase 1 the client owns form state and accessible presentation; the server owns durable drafts, results, evidence and authority. Show Saving, Saved, Save failed and Conflict. Phase 4 adds downloaded content, durable local attempts, pending operations and provisional scheduling; only then introduce `SavedOnDevice` and `Synced`. `AwaitingFeedback` and `QuotaPaused` belong to optional asynchronous AI workflows, not ordinary deterministic feedback.

Phase 1 autosaves learning drafts to the server after an idle debounce and before deliberate pause/navigation; failed edits remain in the open page. Refresh recovers the last acknowledged server draft, not unsaved disconnected changes. Phase 4 atomically saves local answers and pending operations to IndexedDB/SQLite before sync. Raw employer context is not silently cached in localStorage. Two tabs/devices cannot overwrite substantive edits without conflict handling. Later optional jobs may poll with backoff; provider errors never replace editor content.

English text comes from message keys from the first screen; the UI must not embed translated wording in domain enums. Separate scenario language from UI language so later Russian interfaces can display an older English attempt faithfully.

## Integration comparison

| Entry point | Timing and value | Recommendation / failure behavior |
| --- | --- | --- |
| Web manual input | Any time, including before commit; selected context only | Baseline. Enough for all required scenarios; user must resubmit changed context |
| Developer-triggered CLI | Before commit/push/PR; can compare selected local changes | Planned expansion after sync/import foundation; explicit selection, preview and resume; unavailable backend leaves a local draft |
| Git hook | `pre-commit` sees staged content; `pre-push` sees outgoing references | Defer mandatory use; optional reminder can check existing evidence without a slow LLM call |
| IDE action | In-editor contextual entry | Planned thin adapter over local core; first IDE selected by actual workflow, additional IDEs follow contract stability |
| PR integration | After commit and normally push; before merge if policy requires | Future employer-authorized capability; not a pre-commit implementation |

Git documents separate pre-commit and pre-push hooks and their bypass behavior. `git diff --cached` concerns staged changes; ordinary `git diff` concerns working tree changes. Do not label either as the whole repository. [Git hooks](https://git-scm.com/docs/githooks), [Git diff](https://git-scm.com/docs/git-diff).

## Future local component privacy contract

The product's local deployment today is distinct from a future developer-side repository helper. When the backend is hosted, that helper may inspect selected code locally and send **only a user-reviewed sanitized event**. IDE extensions should call its core, not implement their own incompatible evidence models.

Proposed `ProfessionalEventImportV1` contains client event UUID, schema version, setting, coarse date, user-approved role/actions/outcome, concept IDs, claim provenance, declared assistance and optional opaque local-source reference. No raw diff, transcript, class/path names, commit message, repository URL, SQL or endpoints. Short free-text fields still need preview and sanitization; a schema cannot prove that text is nonsensitive.

The authenticated caller supplies ownership; the imported payload cannot choose another owner. Idempotency is `(owner, client event UUID)`. Unknown fields are rejected. Imported understanding claims are labeled `client_reported_assessment`; arbitrary clients cannot mint trusted server assessments. Imported journal material starts as a draft until the user approves it in the product.

Local analysis can use human-authored prompts, deterministic checks or an explicitly selected model. **Sending repository context directly from the helper to OpenRouter still discloses it externally**, even if the Sensei++ backend never receives it. Employer-restricted code needs an authorized inference arrangement or a fully local/manual path. Free hosted inference does not remove that boundary.

## Optional Git fingerprint and recheck

Keep a local manifest containing source kind (`staged`, `unstaged`, `selected`), base commit/tree, exact selected path/blob identities including modes/deletions, and a hash of the serialized reviewed material. Disable external diff/text conversion helpers for inspection. Handle untracked files explicitly; binary files, submodules and unsupported content are listed as unreviewed rather than silently ignored. An opaque random local revision ID can be sent to the backend; keep hashes and proprietary identifiers local where possible.

Before acknowledgement and any optional hook check, recompute the relevant manifest. Any difference requires a new context review. After a rebase, changed base context also invalidates currency. In the web-only version, no automatic knowledge of later local edits is possible; label it “acknowledged supplied snapshot”, never “current repository approved”.

A CLI can later check equality locally; the server cannot verify an opaque local manifest or a client's honesty. If a team later wants a gate, separately define required evidence, policy version, override authority, expiry and outage behavior. The baseline hook, if added, is a bypassable advisory reminder with explicit stale/offline status and no implicit success. It never commits, pushes, changes code or publishes a note.
