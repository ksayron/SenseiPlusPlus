# Offline learning, native clients and product expansion

**Added 17 September 2026 following scope clarification.** The creator reports about one year of work on a large ASP.NET Core microservices project using RabbitMQ and PostgreSQL and considers themselves proficient. Architecture should accommodate that capability. The first checkpoint is in a few months, followed by several more months of development. Native apps, CLI/IDE integration and enterprise capabilities are planned expansion tracks, with early delivery possible when the core is progressing well. Useful offline learning is a requirement now, not an optional future optimization.

## Offline product contract

A developer downloads a learning pack before a trip, closes the app, later reopens it without network or a running backend, studies and practices, sees appropriate feedback, and retains progress across restarts. On reconnect, work synchronizes without duplicate attempts or lost edits. This must run on the client device; a locally deployed ASP.NET server on a laptop is not a mobile offline solution.

| Capability | Disconnected behavior | After reconnect |
| --- | --- | --- |
| Lessons and reference material | Read downloaded original lessons, permitted reference excerpts and examples | Receive versioned pack updates; external links still require network |
| Recall / explain / compare | Record answer before revealing authored hints, reference answer and self-review rubric | Optionally request AI feedback for selected saved attempts |
| Apply / diagnose / transfer | Use downloaded scenario variants; deterministic feedback for structured choices; authored reasoning guidance for open answers | Add contextual feedback while retaining original assistance and self-review |
| Practice queue | Compute a provisional local schedule from available observations and local policy | Reconcile server schedule without rewriting history or duplicating due tasks |
| Experience | Read explicitly downloaded sanitized entries; capture manual private drafts and rehearse authored prompts | Resolve edits; request optional story generation |
| AI conversation | Previously saved feedback readable; new provider calls unavailable | Resume explicit pending requests under current free quota and disclosure policy |
| Work acknowledgement / factual approval | Record drafts of decisions; do not claim global approval of an unseen current version | Server checks current versions/rights and requires review of conflicts before confirmation |
| Team/organization administration | Previously permitted learning packs only, subject to offline-access policy | Membership/grant changes applied; admin writes require online authorization |

Offline feedback is useful without pretending to be an LLM: authored reference explanations, decision-specific hints, checklists and structured exercises support a complete learning session. Open-ended self-review is labeled `self_reviewed`; objective keyed feedback is `deterministic_check`; neither becomes `model_assessed`. Never score free-form reasoning with keyword matching or require live AI to finish a downloaded lesson. A cached answer from another scenario is not new assessment.

No embedded local model is required. External free models remain the configured AI baseline. Downloaded practice packs and deterministic logic, rather than a background connection, provide the offline experience.

## Client architecture and recommendation

Keep React/TypeScript for the web application and add a PWA offline learning surface using a service worker for the application shell and IndexedDB for structured local state. Native clients use SQLite with explicit migrations. Neither client connects directly to PostgreSQL. Service workers can support offline cached resources; browser storage is subject to quotas/eviction, so a PWA's durable-storage request and user-visible sync/export status matter. [Service workers](https://developer.mozilla.org/en-US/docs/Web/API/Service_Worker_API/Using_Service_Workers), [browser storage limits](https://developer.mozilla.org/en-US/docs/Web/API/Storage_API/Storage_quotas_and_eviction_criteria).

**Native recommendation: .NET MAUI; confirmed initial platforms: Android and Windows.** Deliver Android travel learning first, then Windows using the same native application core. Framework suitability still needs the real-device spike. This uses C# for a native client application core and the CLI, while retaining the web's independent interface. MAUI supports Android, iOS, Windows and macOS through Mac Catalyst; Microsoft's SQLite guidance supports local data storage. iOS is a later scope decision and its documented Windows development setup requires a networked Mac. [MAUI platforms](https://learn.microsoft.com/en-us/dotnet/maui/supported-platforms?view=net-maui-10.0), [MAUI local database](https://learn.microsoft.com/en-us/dotnet/maui/data-cloud/database-sqlite?view=net-maui-10.0).

| Choice | Strength for Sensei++ | Trade-off / decision |
| --- | --- | --- |
| MAUI with native controls | C# application logic, SQLite, mobile plus Windows client; shares appropriate client libraries with CLI | Separate web/native UI implementation; validate keyboard, scrolling, offline database, packaging and target-device behavior in a real-device spike |
| React Native | Shares TypeScript utilities and concepts with React web | Web components are not automatically native UI; platform-specific code and native integration remain. Prefer if React/mobile productivity or platform tests clearly favor it. [Platform-specific code](https://reactnative.dev/docs/platform-specific-code) |
| PWA only | Earliest offline coverage and one web delivery path | Browser storage/background limits and platform distribution differences; useful first milestone, not a substitute for the requested native expansion |

Use current supported MAUI workloads when the native milestone starts. The retrieved MAUI 10 table lists support through 11 May 2027, unlike the longer .NET backend LTS lifecycle. Include mobile workload/SDK upgrades in the multi-month plan; do not freeze it to the backend's support horizon. [MAUI support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/maui).

Share OpenAPI-generated transport contracts, locale-neutral identifiers and sync/learning-policy specifications across clients. C# clients may share a pure `ClientCore` library; TypeScript uses equivalent logic checked against the same policy test vectors. Do not ship backend entities, authorization implementation or private service credentials in shared client packages. UI workflows differ intentionally: phone emphasizes training/rehearsal; web emphasizes reflection/journal management; CLI/IDE emphasizes selected work context.

Native authentication is an explicit N1 prerequisite. Keep ASP.NET Core Identity for user accounts and add an OpenIddict-based authorization server when native/CLI sign-in is implemented; Identity alone is not an OAuth/OIDC server. Use authorization code with PKCE in the system browser, registered redirect URIs, public clients without embedded secrets, short-lived access tokens and revocable refresh sessions. Validate the flow on Android and Windows, store refresh credentials in platform-protected storage and require reauthentication before sync when necessary. OpenIddict provides server components and PKCE configuration, but application consent/login/session policy still needs implementation. [OpenIddict server integration](https://documentation.openiddict.com/guides/getting-started/creating-your-own-server-instance), [PKCE configuration](https://documentation.openiddict.com/configuration/proof-key-for-code-exchange).

## Download packs and local data

Define `LearningPackManifest` with pack ID/version, locale, schema version, minimum client version, policy version, concept/scenario/rubric versions, permitted offline lesson/hint/reference assets, size and cryptographic checksums. Distribution is through authenticated API calls where necessary. Content is authored/reviewed before publication; model-generated variants must pass review before they become offline reference material.

Download to staging, verify all required assets and hashes, then atomically activate the pack. A dropped download leaves the previous version usable. Never update a rubric halfway through an attempt. Retain old pack versions referenced by unsynced attempts; garbage-collect after confirmed sync and a local retention decision. The UI exposes pack completeness, disk usage, language, last sync and “ready for offline use”. Include enough authored variants to make a trip useful without generating them online.

Local stores: packs/assets, session/attempt records, hint-reveal events, self-review, provisional review queue, optional downloaded journal revisions, local drafts, pending operations, sync receipts, cursor and deletion tombstones. Persist an answer and its pending operation in one client database transaction. Ordinary page refresh/app termination must not erase an offline answer. On a shared device, offline data is opt-in and account-scoped; raw employer source is not downloaded by default.

## Synchronization protocol

Use HTTPS pull/push with foreground sync on launch/resume and explicit “sync now”; opportunistic background sync is an enhancement, not the delivery guarantee. A message broker is a backend concern and is not the mobile sync protocol.

1. After online sign-in, register an opaque device ID. Every operation has a client-generated UUID, schema version, aggregate ID, base server version, local sequence and recorded device timestamp. Device identifiers/timestamps are provenance, never proof of identity or accurate time.
2. `POST /api/v1/sync/push` sends a bounded batch. Authenticate afresh, derive owner from the principal and validate operation types/size/versions. Deduplicate each operation by `(owner, operationId)` in a durable receipt store; commit domain input plus receipt together. Same ID/different content is a conflict. Return per-operation accepted, rejected, conflict or deferred receipts. Dependency failures defer dependent operations, not discard them.
3. `GET /api/v1/sync/pull?cursor=...` returns an owner-scoped ordered change feed, tombstones and next opaque cursor. Use a committed change-log ordering with no lost entries from transaction commit races; an owner-specific sequence allocated under lock is sufficient initially. Apply a page and cursor atomically locally. Retrying either request is safe.
4. On reset/expired cursor, fetch a consistent owner snapshot and watermark, preserve unsynced local operations, then rebase them. Never wipe local unsynced answers just to rebuild the read cache. A deleted/revoked owner must not receive a snapshot or have their queued data recreated.
5. Syncing answers does not automatically request AI for every uploaded attempt. Store inputs first; explicit selected feedback requests use the current consent/route and free-quota queue. Server projections distinguish client observations from validated assessments.

First supported offline writes: create attempt, reveal hint, self-review, create learning session, edit local draft and snooze/dismiss practice. Acknowledgement, factual approval, access grants and organization policy changes remain online authoritative commands. Broaden offline commands only with a specified conflict rule.

| Conflict | Resolution |
| --- | --- |
| Same attempt uploaded twice | Operation receipt and unique attempt ID produce one record |
| Different attempts on two devices | Preserve both; do not merge answer text or count same-session revisions as spaced successes |
| Concurrent journal edits | Compare base version, preserve both drafts, ask user to resolve; no last-write-wins loss of contribution/impact |
| Old scenario/rubric | Preserve exact version and label superseded content; require pack refresh for new sessions if incompatible |
| Schedule changes across devices | Server derives canonical queue from accepted observations; local proposals remain visibly provisional; uncertain device time does not advance spaced-success stage automatically |
| Source/account deletion versus pending update | Tombstone wins; stale operation rejected with a safe receipt; never resurrect deleted source from a reconnect |
| Revoked team membership | Reject new organization operations and stop downloading team material; clear applicable cached data on reconnect according to policy |
| Expired token while offline | Existing unlocked personal packs remain usable; reauthenticate before sync; never repeatedly send stale credentials |

A server-issued offline-access lease can bound future team-content access in ordinary clients, but immediate revocation cannot be guaranteed on a disconnected or compromised device. Personal downloaded learning remains usable during trips; organizational restrictions must not silently lock the personal profile. Logout/account switching offers explicit sync/export of unsynced personal data before local removal; private caches must not leak between accounts. Background tasks use the same owner namespace and cannot send a prior user's data after switching.

## Enterprise track with explicit scope

The first enterprise slice is organizations/teams, invitations, role-scoped content management, curated learning paths and voluntarily shared approved artifact revisions. Separate personal and organization API/query scopes. Roles: member, content curator and organization administrator; a lead can view only artifacts explicitly shared with them or their authorized team. Seat funding never expands personal-data access.

Start with published content and explicit sharing workflows, not population analytics. Record organization admin/share actions in an audit trail excluding private answers. Later enterprise work can add OIDC/SAML federation through an appropriate identity solution, provisioning, policy configuration, data-region controls and suppression-tested aggregate trends. Each has a separate definition of done; “enterprise features” is not a commitment to every compliance program, manager dashboard or deployment topology.

Server queries and sync feeds must enforce owner/grant/membership checks. Organization-owned content has an organization scope; personal attempts on that content remain personal unless separately shared. Learning-path completion can be disclosed as an explicit scoped event without disclosing failed answers. Deleting membership must not delete portable personal learning, while restricted content/source removal follows its own rights.

## Professional backend evolution

Retain a modular monolith as the first deployment choice because it matches the supplied architecture direction and shared transactional rules. Make boundaries enforceable with module assemblies, public application contracts, table/schema ownership, architecture tests and observable jobs. This is not a limit on the developer's ability to build distributed services.

An AI worker can run in the API host for local development or as a separately launched worker sharing the same durable job protocol. Separate it when batch sync traffic or resource/release isolation justifies it. Add RabbitMQ when independently deployed consumers need reliable fan-out/backpressure, such as notifications, integration processing and organization projections. Publish through a transactional outbox, track consumer inbox/deduplication, acknowledge after durable processing, and monitor retry/dead-letter paths. Broker confirmation alone does not make downstream domain work exactly once; RabbitMQ's reliability guide describes acknowledgement and redelivery/duplication concerns. [RabbitMQ reliability](https://www.rabbitmq.com/docs/reliability).

Do not expose RabbitMQ credentials to clients or put raw private answers into broadcast events. Events carry scoped IDs and minimal approved facts; consumers authorize any follow-up reads. With a separate consumer, update source plus outbox in one transaction and accept explicitly observable projection lag; do not pretend the former in-process cross-module transaction spans services.

## Required verification

Airplane-mode acceptance: download a pack, terminate/relaunch, finish teaching and two different exercises, reveal a hint, save self-review/draft, restart again, reconnect and see one synchronized history with correct assistance and provisional/final schedule labels. Run on a real target device with the backend stopped, not only with provider requests mocked.

Also test interrupted pack updates, exhausted storage, app/database schema upgrades with pending operations, revoked credentials/membership, simultaneous device edits, cursor reset, clock skew, deletion while a device is absent, account switching and quota-limited replay. Browser eviction remains a disclosed risk; request persistent storage where available and offer export/sync status. Native storage also requires protection and recovery; SQLite by itself is not encrypted.
