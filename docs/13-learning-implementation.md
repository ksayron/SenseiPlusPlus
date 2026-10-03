# Phase 1 learning implementation and verification

Local implementation record, 29 September 2026. The behavior specification remains [09](09-learning-system.md), [10](10-learning-experience.md), and [11](11-learning-delivery-plan.md). Their earlier “implementation pending” statements describe the documentation baseline, not this working tree. No remote push or deployment is part of this delivery.

## Implemented behavior

- Canonical concepts and transaction-validated containment, prerequisite, and symmetric related edges. Existing concept IDs, observations, and experience revisions survive the additive migrations.
- Immutable material/exercise versions, exercise families, relational sessions/items/attempts/results/events, owner-scoped operation receipts, goals, roadmap versions, enrollment and traversal.
- Safe declarative material and all six exercise formats. Objective evaluation is deterministic and binary; invalid answers create no attempt. Notes are retained without grading. Learner responses omit evaluator keys and feedback before submission/reveal.
- Topic URLs, query-preserving navigation, count/type setup, truthful shortages, explicit Continue, pause/resume, skips, early summaries, assistance and linked answer-aware retries. Goals can be created, edited and archived.
- Pinned sessions, a database-enforced single Active/Paused session per owner, strong ETags and distinct 400/404/409/412/428 responses. Receipt replay precedes stale-version checking and returns the current session token. Operation reuse with changed semantics fails.
- Shared Learning/Evidence transactions, rollback, sorted owner/concept locks, immutable trusted provenance, original self-review admissions, status history and rebuildable projections. Manual observations remain visible and unscored.
- Latest-per-family objective evidence (20 families; weights 1/.5), one admitted positive review per rolling 72 hours, weight .1 and uplift cap .05. Self-review alone stays unknown; withdrawals do not reset cooldown or revive an older admitted gain.
- Deterministic selection with weights 4/3/2/1/-3, family uniqueness and concept/type diversity. Roadmap traversal is independent of shared evidence coverage; default requirements are three unassisted objective families, two sessions and .8 correctness. No overall mastery percentage is exposed.
- A serialized client command queue, 750 ms generation-aware server autosave, deliberate-navigation flush, explicit conflict recovery, same-operation lost-response retry and owner-change cleanup.

## Local setup

From the repository root, with Docker Desktop running:

```powershell
docker compose up -d postgres
dotnet tool restore
dotnet restore backend/Sensei.sln --locked-mode -m:1
dotnet build backend/Sensei.sln -c Release --no-restore -m:1
dotnet tool run dotnet-ef database update --project backend/src/Sensei.Host --configuration Release --no-build
dotnet run --project backend/src/Sensei.Host --configuration Release --no-build
```

In another terminal:

```powershell
cd client
npm ci
npm run dev
```

Open `http://localhost:5173/learn`. The default development owner convention is unchanged. The Development seeder adds a small, repeatable, explicitly labeled C# and synthetic orbital library. It does not delete existing application data. Two C# roadmaps share value-semantics coverage; OOP deliberately has insufficient families. The fixtures include all formats, accepted ordering/matching alternatives, multiple correct choices, a correct “No issue”, same-family variants, superseded versions, reading-only/practice-only topics and an intentionally unavailable image with alternative text. Malformed definitions are tested as rejection cases rather than stored as usable content.

Migrations are explicit; startup never silently migrates. `LearningFoundation` adds the runtime; `LearningProvenanceHardening` adds owner/concept-consistent observation references and compatibility metadata. Do not reset the database volume to install learning.

## Verification record

Verified locally against PostgreSQL 18 and the then-current .NET 9 target.
The current .NET 10 / Node 24 baseline and its separate verification are recorded in
[the toolchain upgrade](15-toolchain-upgrade.md); the following counts describe the original delivery:

| Check | Result |
| --- | --- |
| Locked backend restore | Passed |
| Client `npm ci` | Passed; audit reported zero vulnerabilities |
| Release build | Passed, zero warnings/errors |
| Domain/unit tests | 18 passed |
| Architecture tests | 3 passed; module boundaries retained |
| PostgreSQL integration tests | 25 passed |
| Client lint, unit tests, production build | Passed; 7 unit tests |
| Browser journeys | 34 passed on desktop and narrow layouts; final expanded run recorded below |
| Visual inspection | Desktop saved feedback and narrow material page inspected; text/code remain readable and missing asset is explicit |

The browser configuration starts the real Development backend and Vite, using a migrated local database. It uses isolated owners, not mocked API results. The local run uses installed Edge (`$env:PLAYWRIGHT_CHANNEL='msedge'`); CI installs Chromium. The new CI browser job and artifact upload are configured but have not been run remotely.

```powershell
dotnet test backend/Sensei.sln -c Release --no-build -m:1
cd client
npm run lint
npm test
npm run build
$env:PLAYWRIGHT_CHANNEL = 'msedge' # optional local installed browser
npm run test:e2e
```

For a machine affected by the local MSBuild worker/pipe issue, use `DOTNET_PROCESSOR_COUNT=2`, `-m:1 -nr:false -p:UseSharedCompilation=false`, and run EF with `--no-build` after the serial Release build. This is a tooling workaround, not an application setting.

## Acceptance evidence map

The IDs below refer to the governing [delivery matrix](11-learning-delivery-plan.md). Tests use behavior-oriented names rather than implementation snapshots.

| Scenarios | Evidence |
| --- | --- |
| D01–D02 | Unit graph rejection and synchronized PostgreSQL cycle race |
| D03–D05 | All objective evaluators, exact sets, alternatives and malformed answers; API format loop verifies rejection without token consumption and ungraded notes |
| D06–D10, D16 | Projection window, weights, latest family, answer-aware exclusion, unknown and uplift tests |
| D11–D15 | Exact boundary unit checks; injected-clock API reviews before/at cooldown, Again/Partly history, withdrawal, original admission preservation and restart rebuild |
| D17–D19 | Assisted/retry exclusion; six free-practice families over two sessions satisfy shared coverage; insufficient OOP content remains explicit |
| D20 | Deferred feedback rejected by the API |
| P01 | Disposable database migrated from the original schema with legacy owner/concept/observation/experience rows; identities/content retained and manual observations unscored |
| P02–P03 | Owner-isolated session/nested/goal/enrollment requests and manual Attempt source unable to create a scored contribution |
| P04–P08 | Concurrent duplicate submission, changed-payload conflict, stale receipt replay, distinct preconditions, injected post-staging rollback and browser lost-response retry |
| P09–P11 | Synchronized starts and overlapping multi-concept review transactions with reversed concept order; exactly one first allowance per concept |
| P12–P16 | Two-tab stale saves, disabled content remaining pinned, learner-key omission, owner-change cleanup and restart rebuild |
| U01–U05 | Cold start/goals, topic URLs/material, six formats, accessible ordering/matching, failed assistance retry |
| U06–U10 | Delayed autosave generation, pause/refresh/resume, navigation-save failure, final/early summary and linked answer-aware retry |
| U11–U15 | Honest shortage, shared coverage on two C# roadmaps, subject-independent fixtures, no model dependency, desktop/narrow browser runs and visual inspection |

## Diagnostics and limits

The `Sensei.Learning` meter records request duration, operation replay/conflict counts and projection failures. Host logs include route/status/latency and trace identity, not answers or notes. Development client diagnostics record bounded acknowledgement/replay/conflict/failure categories and elapsed time. Browser tests attach these records to their artifacts.

This is local Phase 1 with fixture content. Authentication, deployment, production authoring, educational-content validation, AI, spaced repetition/decay, exams, offline execution/sync, native clients and integrations remain outside this implementation. Only server-acknowledged work survives refresh; the UI retains unacknowledged input while the page remains open and offers explicit recovery rather than claiming offline durability. Browser emulation and local inspection do not constitute testing on a physical phone or a screen reader.
