# Durable optional assessment of saved attempts

Implementation branch: `feat/12-durable-assessment-jobs`. Issues #11 and #12. The user selected a separate optional feedback request **after an attempt is saved**. Issue #8 is skipped; AI configuration and live qualification are deferred. The branch contains the local authentication and gateway dependencies and is reviewed against `c0675bd` until those dependencies are accepted.

## Learner behavior and delivery boundary

Ordinary submission still commits the immutable attempt, deterministic result or labeled self-review, assistance history and operation receipt atomically. An optional assessment creates a new logical job in a later transaction, after the existing attempt is durably available. Submitting an answer, opening a session, reconnecting and replaying a receipt never initiate inference automatically.

The saved feedback panel has an **Optional AI feedback** action. It loads the saved job history and a fresh disclosure preview. The shipped `DeferredAssessmentRoutePolicy` grants no route to any profile: the panel explains that provider configuration is deferred, a request returns `503 assessment_unavailable`, and no job or invocation is created. There is no API-key setting, active route, provider call or fallback fixture in the web app.

The trusted route-policy interface exists for later composition and isolated synthetic tests. The gateway currently permits only explicitly public synthetic input. Adding a key does not authorize private learner answers or notes. A later policy must deliberately approve eligible synthetic profiles and a qualified immutable route. Private-input eligibility, a real provider pilot and any expansion of that boundary require their own review before enabling them. Test fixtures are used only in the disposable integration database with explicitly synthetic test profiles; they are never substituted for feedback on real learner answers.

There is no reviewed criterion rubric in the current fixture library. The output separates exploratory technical reasoning from communication advice, includes context sufficiency, missing assumptions, quoted answer spans, allowlisted exercise references and possible next actions. Its scope is **Exploratory**. It has no overall score, cannot replace an original deterministic result or self-review, and stages no knowledge observations. Curated rubric publication and grounded knowledge contributions remain future work.

## Durable records and transactions

`AssessmentJob` pins owner, attempt, exercise version, serialized input and digest, exact gateway request digest, template/contract versions, route revision/digest, origin and accepted disclosure digest. PostgreSQL prevents input changes after insertion. One owner/attempt can have only one active job; deliberately reassessing after a terminal job creates a new job and feedback revision.

Request receipts share the learning operation namespace. The same operation/attempt/disclosure replays its original job before route availability is checked; changed use conflicts. The response describes that original job's current state. Cancellation also has a replay receipt, requires the current job's strong `If-Match`, increments its generation and fence, and cannot cancel an already terminal job.

The monolith runs a scoped `BackgroundService`; it does not require RabbitMQ or a second service. Every worker persistence step uses a fresh DI scope. [Microsoft scoped background-service guidance](https://learn.microsoft.com/en-us/dotnet/core/extensions/scoped-service).

1. A short PostgreSQL transaction recovers expired leases, serializes local dispatch and claims one due row with `FOR UPDATE SKIP LOCKED`. One unexpired running job is allowed across the local database, including multiple host processes. Owners who have not recently received an invocation are selected first, then older jobs. [PostgreSQL queue locking](https://www.postgresql.org/docs/18/sql-select.html).
2. After that transaction and scope are disposed, the worker verifies the exact saved source, template/schema, route and both privacy reviews against the accepted disclosure. Changed or withdrawn inputs/recipients become obsolete without invoking a model.
3. A second short transaction reserves the invocation, increments its bounded attempt count and commits before the gateway is called. A reservation counts conservatively even when a crash makes its outcome unknown.
4. The gateway call runs without a database transaction. A 90-second lease is renewed in independent transactions every 15 seconds. The processor also enforces a maximum 60-second call deadline. An uncertain or lost lease prevents final writes.
5. A short completion transaction locks the job and checks state, unexpired lease, fence, generation, invocation identity, source availability and exact input digest. Provider identity and provenance must match the pinned request and route. It validates schema semantics again even if the adapter claimed success. Feedback and job success commit atomically, with unique job/result and owner/attempt/revision keys. PostgreSQL rejects updates to feedback revisions.

Duplicate completion does not rewrite feedback or invocation audit. A delayed result after cancellation, expiration or a newer lease is discarded. A restart can repeat inference after an unknown crash outcome, but cannot repeat the logical feedback effect. Original attempts remain separately readable throughout failure and recovery. Content availability locks protect the final source check; no delete or purge workflow is added by this issue (#26 must invalidate jobs before removing their inputs).

## Failure and local budget policy

States are `Queued`, `Running`, `Retry`, `Succeeded`, `Failed`, `Cancelled`, `Obsolete`. HTTP disconnect or navigation does not cancel the job. Explicit cancellation invalidates its database authority; stopping the backend leaves its lease for bounded restart recovery. Processing cannot run while the local app is closed or the computer is asleep.

There is at most one transient retry after a provider/metadata availability failure or timeout, and one clean regeneration after malformed, truncated or semantically invalid output. Regeneration reuses the pinned prompt/input/schema; it never feeds raw provider output or diagnostics into another prompt. The maximum is three conservatively reserved invocations per job, including retries, rate-limited attempts and unknown crash outcomes. Refusal, identity/configuration/price failures and permanent errors do not become incorrect-answer evidence.

Local reservations allow at most 40 invocations in a rolling 24 hours and 20 in a rolling minute across the database. They include all owners, routes, timeouts and unknown outcomes. Hitting the local budget pauses the job before reserving a call, without consuming a transient or regeneration allowance. Provider rate/quota failures likewise pause without consuming those allowances and honor a later `Retry-After`, while still respecting the three-invocation cap. Shared keys used by other applications and provider-side quota reconciliation remain part of deferred live configuration; these local reservations cannot know external usage or promise an available provider quota.

Audit records contain pinned request/route/template/schema identity, known actual model/provider, origin, safe finish reason, usage, elapsed time and bounded failure categories. Raw response bodies, secrets and exception text are excluded. A gateway failure is operational status; no failure stages a knowledge observation.

## HTTP contract

All routes are authenticated, owner-derived and use the existing cookie/CSRF contract. Clients cannot choose model, provider, origin, prompt or schema.

| Route under `/api/v1/learning` | Behavior |
| --- | --- |
| `POST /attempts/{attempt}/assessment-preview` | Read-only disclosure computation; CSRF still required. Unavailable previews contain no recipient/input disclosure. |
| `POST /attempts/{attempt}/assessments` | Operation ID and accepted disclosure digest; returns `202`, job location and current ETag. No parent session mutation or `If-Match` is needed for this independent create. |
| `GET /attempts/{attempt}/assessments` | Owner/attempt-bound cursor envelope with current jobs and attributed feedback. |
| `GET /assessments/{job}` | Safe job status, opaque version token and strong ETag. No full input, raw provider body or secret is returned. |
| `POST /assessments/{job}/cancel` | Operation ID, CSRF and current job `If-Match`; current status with replay marker. |

A preview discloses the complete system prompt, saved prompt/choices/reference, answer, note, self-rating and assistance flags, output schema, model/provider, route digest and privacy URLs/review dates for **both OpenRouter and its upstream provider**. It is permission to request only that exact job, not a reusable consent to future input or recipient changes. Privacy reviews older than 30 days block new requests and running-job finalization. A changed disclosure requires a fresh preview and explicit acceptance. No browser text can grant the trusted synthetic-profile capability.

The client polls only while an opened history contains an active job, preserves request/cancel operation IDs after transport errors, separates original feedback and exploratory revisions, labels fixtures and uncertainty, and provides an explicit cancel action. Text is rendered as text. The unavailable path is the only shipped web path until provider qualification and configuration are deliberately implemented.

## Verification and remaining work

Verification uses disposable PostgreSQL, HTTP authentication, the pinned .NET/Node toolchains and the existing learning browser matrix. The new integration tests cover request-body conflicts/replay, duplicate completion, restart and expired fencing, concurrent claims/fairness, cancellation during a blocked gateway call, source withdrawal, input/feedback database immutability, route changes, retry/regeneration bounds, semantic grounding, local and provider quota pauses, real cookies, owner isolation, CSRF, ETags and client-origin spoofing. A browser case saves an answer, opens the unavailable assessment panel, verifies there is no job and reloads the same attempt.

The final validation record is 56 unit and 3 architecture tests, 47 PostgreSQL integration tests (16 dedicated assessment cases), 7 client tests, 44 authenticated learning browser cases and 27 shared SPA browser cases. Locked restore, a zero-warning/error Release solution build, client lint/build, the additive local migration and the EF pending-model check passed. Desktop and narrow screenshots of the unavailable assessment panel were inspected; the shared SPA checks exercise both themes, tablet/320px layouts, enlarged text, keyboard behavior and reduced motion. Browser captures are isolated in `.tmp/assessment-browser-final` and `.tmp/assessment-spa-regression`; PostgreSQL results are in `.tmp/assessment-final-postgres`.

Live provider qualification, provider/private-input policy, real model quality, reviewed rubric evidence, screen-reader/full WCAG testing, physical-device testing and GitHub CI remain unverified or deferred. #8 stays skipped. Issue #12 should remain open for review and deferred live acceptance. The document does not claim real inference has been tested.
