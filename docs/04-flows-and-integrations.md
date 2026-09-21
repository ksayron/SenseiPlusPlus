# User flows, interfaces and integration direction

## Navigation and interaction

Use five destinations: **Home, Learn, Work, Journal, Profile**, with Settings accessible separately. These are different workflows over shared records, not five independent products. Career reuse starts from Journal; there is no empty “Career platform” section in the prototype.

| Surface | Main action and visible information |
| --- | --- |
| Onboarding | Choose one goal and optional familiar topics; no job/Git link required; explain external AI route and free quota |
| Home | Resume a session, start a short lesson, record an episode; up to three practice suggestions |
| Learn | Pick a curated mechanism; “new to this” opens teaching; later reveal the reference after an initial attempt |
| Offline library | Download complete practice packs, verify ready status, see storage/last-sync state, and resume learning without backend access |
| Work editor | Intent, constraints, contribution and optional selected excerpt; explicit AI request preview; retain a manual-only path |
| Reflection workspace | One question at a time, exact context version, progress such as 2 of 3, saved-answer status, help/skip/pause/disagree |
| Completion | Checked topics, initial/assisted attempts, unresolved/disputed items and context version; acknowledgement is a separate button |
| Journal | Manual and reflection-derived entries, draft/approved states, search/filter, editable contribution/outcome, source links |
| Profile | Separate exposure, explanation/scenario observations, recency and gaps; each item opens its provenance |
| Story preview | Select one approved entry, inspect factual basis, rehearse one or two questions, edit and export a concise story |
| Settings | Language preference architecture, privacy/disclosure, data export/delete, configured AI route and quota state |

Use text-first structured forms with optional Markdown preview and a basic diff viewer. Avoid a full IDE/editor, WYSIWYG document suite or large graph canvas. Keyboard navigation, semantic labels, focus after errors and text status updates are baseline acceptance checks. Do not rely on color to convey assistance or approval. On narrow screens, context appears in a drawer rather than crushing the answer area.

## Complete learning-only journey

Student selects message redelivery → reads an authored explanation if needed → answers a scenario before seeing its reference solution → answer is saved → feedback is assessed or remains pending if the provider is unavailable → student can challenge it or request help → profile links the narrow observation → schedule offers a changed scenario later.

No employment or work entry is required. The learner can optionally record a personal-project experiment; its setting remains personal project. A code-free explanation task remains substantive even when no source excerpts are supplied. Manual self-comparison during outage is labeled self-review and does not create model-assessed evidence.

Travel journey: download a pack while connected → reopen PWA/native app in airplane mode → complete a lesson and changed scenario → use authored hints and reference/self-review → save attempts locally across restart → reconnect → sync once → select any attempts for later AI feedback. Offline completion is a supported learning outcome; the profile preserves the distinction between self-review and subsequent assessment. The implementation contract is in [offline clients](08-offline-clients-and-expansion.md).

## Integrated work journey

Developer records a background-processing change with stated purpose and contribution → previews selected material and provider recipients → corrects the application's proposed interpretation → answers a few grounded questions → records an unverified lifetime assumption and asks for help → sees unresolved issues on completion → explicitly acknowledges the exact discussed version or leaves it incomplete → separately reviews a journal draft → approves a factual revision → practices a related concurrency scenario later → uses the entry to rehearse an interview account.

The app can suggest an exposure tag from the approved entry while retaining unresolved thread-safety understanding. There is no requirement to claim the code is safe. A one-line fix can have a rich investigation story, and non-code mentoring/design episodes use the same journal path. If the selected free endpoint is unsuitable for the material, keep the entry local and use a sanitized/general scenario; do not force source upload to complete the product loop.

## Presentation journey

Select an approved reliability entry → show context, personal contribution and known/unknown outcome → offer one concise interview-story draft → let the user rehearse why an alternative was rejected → edit the wording → approve the artifact revision → download Markdown or plain text. The reviewer note is the same lightweight preview/export component with a different template and deliberately selected content.

Numbers and role claims must originate in approved fields. If outcome is unknown, the story says so or omits impact. New substantive claims added in the artifact editor must be flagged for explicit confirmation and linked to a new source revision before export; stylistic edits alone do not change source facts. Do not attach private question history or links that expose the full session.

## Client/server boundary and recovery

| Operation | Contract outline | Behavior |
| --- | --- | --- |
| Save context | `POST /episodes/{id}/snapshots` with expected episode version | New immutable snapshot; old acknowledgement remains historical |
| Confirm interpretation | `POST /sessions/{id}/interpretation-confirmations` | Confirm exact revision, not any later model text |
| Submit answer | `POST /sessions/{id}/attempts`, idempotency key and expected version | Commit input first; return `202` with job ID if feedback queued |
| Track work | `GET /jobs/{id}` and `GET /sessions/{id}` | Owner-checked status and durable state; polling is replaceable by SSE later |
| Dispute | `POST /feedback/{id}/disputes` | Retain original feedback, capture missing constraint and correction lineage |
| Acknowledge | `POST /sessions/{id}/acknowledgements` | Explicit user action, reviewed bundle and source version required |
| Approve journal | `POST /entries/{id}/revisions/{version}/approval` | Version guard; model cannot invoke it |
| Export story | `POST /artifacts/{id}/revisions/{version}/export` | Whitelisted projection of exact reviewed version |
| Delete/export account | Dedicated owner-authenticated requests | Reauthentication, status and clearly stated scope |

Client owns form state, accessible previews, downloaded content, durable offline attempts, pending operations and provisional scheduling; server owns synchronized authoritative versions, authorization, AI request assembly and confirmed evidence/approval. Show separate `SavedOnDevice`, `Synced`, `Conflict`, `AwaitingFeedback` and `QuotaPaused` states; “saved” must not obscure whether another device can access the work.

Autosave offline learning answers and pending operations atomically to IndexedDB/SQLite, then synchronize when possible. Raw work-source drafts remain in-page until saved to the backend unless the user explicitly enables appropriate device storage; employer context is not silently cached in browser localStorage. A refresh loads the account-scoped local or synchronized draft/session. Two tabs/devices cannot overwrite substantive edits without conflict handling. Poll every 2 seconds for active online jobs, backing off toward 10 seconds and stopping in hidden tabs or terminal states. Provider errors never replace the user's editor content.

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
