# AI, learning and provider configuration

## Where AI belongs

Use models for contextual interpretation, focused questions, feedback on reasoning, optional teaching elaboration and grounded draft wording. Keep authentication, permissions, input persistence, content selection, deadlines, scheduling, provenance, approval, export, pricing policy and job state deterministic. No model receives tools for database writes, publication, source execution or filesystem/network access.

The prototype uses **free external models through OpenRouter**, with another provider selectable through configuration. Local deployment is not local inference. No paid call, purchased credit tier, automatic top-up, model download or GPU is a baseline dependency.

## Provider contract and selection

Expose domain task interfaces such as `InterpretContext`, `GenerateQuestions`, `AssessAttempt` and `DraftExperience`. Their implementation uses an `IModelGateway` returning typed content, safe error category, actual model/provider, finish reason and usage. Keep provider-specific routing JSON inside the adapter. One OpenRouter adapter plus a deterministic fixture adapter proves replaceability; a live second vendor is unnecessary initially.

Configure a versioned `AiRoute` on the server:

| Field group | Proposed values / semantics |
| --- | --- |
| Connection | Adapter kind; HTTPS base URL from an operator allowlist; secret reference, never a key in a database export or browser |
| Selection | Explicit model ID, allowed upstream endpoints, task bindings; save actual returned identity separately |
| Capabilities | Strict schema / JSON-only / validated-text mode, context limit, output limit, cancellation and usage support |
| Privacy | Public-demo or restricted-data route, policy URLs/check date, logging/training/retention terms, provider restrictions |
| Budget | `FreeOnly=true`, all billable unit prices zero, paid tools/plugins disabled, account quota and local safety reserve |
| Behavior | Timeout, retry ceiling, concurrency, schema/template version, default English answer language |

The operator can change provider/model without editing business logic; UI settings can select among approved routes but cannot supply arbitrary endpoints or override global free-only policy. Changing a route creates a new revision used by new jobs. Already queued requests pause for renewed disclosure if the provider/data terms materially change. Secrets can be supplied through environment/development secret storage; this is operator configuration, not a full bring-your-own-key marketplace.

**Selection recommendation:** pin an explicitly tested free route for a small public-example prototype. Use `nvidia/nemotron-3.5-lightning:free` as the first *feasibility candidate*, not a proven assessor. Its retrieved model page lists zero token pricing and states that the free endpoint logs information for security/improvement and does not enforce `response_format`. Those limitations are part of the decision. Use only nonconfidential, nonpersonal technical material with that endpoint. [Candidate endpoint](https://openrouter.ai/nvidia/nemotron-3.5-lightning:free).

If it fails the technical-quality or parsing checks, evaluate a second current zero-price route from the catalogue with better schema/privacy support. `qwen/qwen3-next-80b-a3b-instruct:free` has a published page, but the retrieved page did not establish a usable current endpoint/capability combination; do not silently promote it to the baseline. This is an explicit unresolved spike, not a benchmark comparison. [Qwen candidate listing](https://openrouter.ai/qwen/qwen3-next-80b-a3b-instruct:free).

Avoid `openrouter/free` for assessed sessions: it automatically chooses among available free models, which weakens repeatability and advance disclosure. It may be useful for synthetic exploration, with actual identities recorded. [Free router](https://openrouter.ai/docs/cookbook/get-started/free-models-router-playground).

OpenRouter supports JSON Schema output for compatible model/provider combinations. Capability discovery is followed by a contract probe; an “OpenAI-compatible” endpoint does not imply support for every feature. A strict route requests the schema and `require_parameters`; a non-strict route requests plain JSON and uses the same parser/semantic validator. Do not send unsupported flags and assume they were honored. [Structured outputs](https://openrouter.ai/docs/guides/features/structured-outputs).

For the prototype, configure explicit upstream selection, disable unapproved fallback, and apply `provider.max_price` ceilings of zero for prompt, completion and request pricing. Check all other relevant pricing fields in fresh model metadata; reject missing/nonzero values, paid plugins and unapproved modality charges. Restricted-data routes additionally require compatible data policies. If no route satisfies the constraints, pause; do not relax price/privacy automatically. These controls must be contract-tested against the chosen endpoint. [Provider routing](https://openrouter.ai/docs/guides/routing/provider-selection).

Most development uses reviewed fixtures. Required offline operation uses downloaded teaching/scenario packs, authored hints/rubrics, deterministic structured-exercise feedback, durable local answers and a provisional review schedule. It must work after restarting the device application without a network or running backend. New model feedback waits for connectivity and explicit quota-aware requests; syncing a trip's history must not automatically spend quota on every attempt. Manual journal drafts and downloaded sanitized entries remain available. A fixture response must never be attached to a real user's answer as a live assessment. See [offline learning and sync](08-offline-clients-and-expansion.md).

## Bounded pipeline

| Step | Inputs → output | Rules |
| --- | --- | --- |
| 0. Select and preview | User-selected context → immutable disclosure bundle | Size/secret checks; show both routing service and allowed inference provider; user can omit code or keep the entry manual |
| 1. Interpret | Intent, constraints, selected excerpts → proposed task summary and missing context | One call; user corrects/confirms; no business purpose inferred as fact solely from a diff |
| 2. Plan reflection | Confirmed context plus curated concepts → 2–3 focused questions | One call; every question ties to a supplied decision or is labeled general; hard cap of 5 questions including adaptive follow-ups |
| 3. Save and assess | One saved answer, exact question, rubric, relevant context → feedback | One call per answer; no entire journal/profile dump; answer persistence is independent |
| 4. Teach or adapt | Feedback, requested help and curated material → explanation / bounded follow-up | Curated hint first, optional model elaboration within the session call budget; help is recorded separately |
| 5. Close reflection | Saved answers/feedback/unknowns → deterministic review summary | User explicitly chooses acknowledgement or incomplete exit; no extra model call needed |
| 6. Reuse | Selected user facts and approved interpretation → draft experience wording | One optional call; substantive claims await user approval; reviewers and journal have separate artifact states |
| 7. Present | One approved entry revision → concise interview story | One optional call; source facts/unknown impact preserved; deterministic template alternative available |

Curated learning bypasses interpretation and question generation. The normal free-quota session uses one or two assessment calls, with teaching, variant choice and completion deterministic. Work reflection normally uses six calls (interpretation, question plan, three assessments, draft); optional extras stop at a visible session limit rather than unbounded conversation. Call budget includes retries.

Initial request limit: roughly 8,000 input tokens and 1,500 output tokens, with a 64 KiB text intake limit as a separate transport guard. Use the lower supported route limit; count prompt/rubric/context overhead. Reject or ask the user to select a smaller excerpt instead of silently truncating relevant constraints. Do not rely on a model's advertised maximum context as a sensible default. Application latency target is first useful feedback within 60 seconds on the selected route, with a 120-second attempt timeout and resumable pending state; these are spike targets, not measured guarantees.

## Assessment structure and validation

Each result includes `contextSufficiency`, criterion-level observations, technical judgment, communication advice, supported claims, missing assumptions, possible misconceptions, accepted alternatives, reference IDs and recommended next actions. Suggested technical judgments: `supported`, `partially_supported`, `misconception`, `insufficient_context`, `not_assessed`. No overall pass percentage is needed.

Validate required fields, enums, lengths, concept/rubric IDs, reference allowlist and cited answer spans. Reject unknown foreign IDs and claims that the model approved a change. Treat malformed JSON, refusals, truncation, empty output and unsupported schemas as provider failures, not incorrect answers. One bounded repair may fix structure; it cannot certify correctness. Store the schema/prompt/model/route revisions and the accepted output, avoiding extra raw-prompt copies.

Do not let generated explanations become the reference answer for their own assessment. Each curated rubric comes from independently reviewed technical material. For contextual questions outside that rubric, ask for missing assumptions or label feedback exploratory and exclude it from strong knowledge conclusions. User disputes preserve originals, ask for the missing constraint and optionally re-evaluate under a new feedback revision. A second model agreeing is supporting feedback, not an authoritative resolution.

Separate reasoning from presentation: imperfect English is not a technical error. Communication advice is an independent field. Do not claim detection of undisclosed AI help; store disclosed assistance and whether hints were revealed before submission.

## Curated core and reference grounding

Start with **six mechanisms across three groups**: DI lifetimes and scoped work in background services; cancellation and concurrency/shared state; message acknowledgement/redelivery and idempotent handling. Each mechanism gets a short English lesson, prerequisite notes, reviewed rubric, a recall/explanation task and two scenario variants. Start M1 with only one mechanism and add breadth after quality is acceptable.

Author concise original teaching material and link official references with version/date. Store a `ReferenceVersion` with source URL, relevant section, review date and short permitted excerpt or original summary. Curated content has explicit draft/reviewed/published status. Review is performed by the creator, with an experienced peer where available. A model can draft content but cannot publish the authoritative rubric itself.

Retrieve references by concept/version first, using small relational lookups. Offline packs include complete permitted teaching/reference material needed for the exercises, not only links that fail during travel. User source URLs are labels, not instructions to browse. No runtime arbitrary URL fetch, search plugin or paid retrieval tool is required. Version changes create new content; old attempts keep their original rubric. Initial technical reading should use supported .NET docs and official broker documentation when lessons are authored, not invented citations generated by the model.

Retrieval-practice research motivates giving users an opportunity to answer before revealing explanations; spacing research motivates later revisits. These findings do not validate this application's grading or an exact schedule for professional reasoning. The original studies concern narrower learning tasks. [Karpicke and Roediger, 2008](https://doi.org/10.1126/science.1152408), [Cepeda et al., 2006](https://pubmed.ncbi.nlm.nih.gov/16719566/).

## Scheduling open-ended practice

Use a transparent **heuristic v1**, not an inferred forgetting probability. FSRS is a credible later alternative for stable recall items, but applying card-level ratings directly to varied explanations requires validation; Anki describes FSRS around card review history and desired retention. [Anki scheduling](https://docs.ankiweb.net/deck-options).

| Observed state | Next action / provisional interval |
| --- | --- |
| Unfamiliar or prerequisite missing | Teach first; practice a simple example when ready |
| Misconception accepted after review | Offer teaching now; schedule a different short attempt in 1 day |
| Correct after hint or partial unaided explanation | Save assistance; follow up in 3 days |
| First supported unaided explanation | Follow up with application or changed scenario in 7 days |
| Supported unaided changed scenario | Advance to 14 then 30 days across later successes |
| Disputed / insufficient context / technical failure | No negative competency update; clarify/retry; preserve prior schedule until a usable observation exists |
| Skipped or irrelevant | Dismiss or select another task; no penalty |

Intervals are product defaults for experimentation, not scientifically calibrated estimates. Store `policyVersion`, input observation and schedule decision. At most one active review plan per concept/aspect/exercise family. Avoid treating same-session revisions as independent spaced successes. A learner may choose “needs refresh” or defer without changing the technical record.

Rank due suggestions by user relevance, unresolved need, capped lateness and variety. Present at most three short tasks at once with snooze/pause; no accumulating failure count or streak requirement. After a long absence, ask for a small fresh sample rather than enqueue every missed review. Store timestamps in UTC and present due dates in the user's timezone. Switching policy affects future scheduling explicitly, not historical performance.

## Quality evaluation and languages

Build 24 reviewed English fixtures: four each for sound reasoning, common misconception, valid alternative, missing constraints, assisted revision and hostile embedded instruction. Include two communication-quality variants with equivalent technical meaning. Keep at least six cases held out when changing prompts. Compare technical verdict, cited evidence, useful uncertainty and invented claims, not wording similarity.

Run most tests against deterministic fixture outputs, including malformed/refused/timeout variants. Initial live comparison: 12 cases on each of up to two free routes, within actual available quota; later run the held-out set on the chosen route and repeat a few ambiguous cases. Record denominators, errors and latency. Proposed release gate: zero false approvals/publications or invented impact promoted to fact; all structural failures safely handled; at least 10/12 pilot assessments judged acceptable by the reviewer. This small threshold is an engineering gate, not statistical proof of learning efficacy. Disable or narrow mechanisms that fail.

English is the only prototype UI/content/assessment target. Use external message catalogs from the first screen, stable locale-neutral concept IDs, `uiLocale`, `answerLanguage`, and scenario `locale`/`translationOfVersion` fields. Avoid concatenated UI sentences and English-only enum values as display text. Later Russian translations need reviewed rubrics and equivalent-answer evaluation, not only a translated system prompt. Date/plural/number formatting follows locale; historical answers and language labels remain unchanged.
