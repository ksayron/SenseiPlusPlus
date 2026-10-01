# Sensei++ architecture and implementation design

**Initial research: 17 September 2026. Learning decisions updated: 24 September 2026. Status: implemented foundations plus planned learning and expansion work.**

Build an individual-first responsive web application around a C#/.NET modular monolith and PostgreSQL. **Core learning is AI-independent. The application and database run locally with no mandatory service spending before hosting.** An optional configurable AI layer may later use approved free external routes; no provider is required to run the first learning milestone. Learning, reflection and approved experience contribute different kinds of evidence to one personal profile. Neither a model verdict nor session completion constitutes acknowledgement or achievement approval.

Preserve **user input, deterministic results, self-review, optional model assessment, and explicit approval as separate records**. Phase 1 displays inspectable evidence and uses only a basic internal estimate. Phase 2 adds per-concept/dimension estimates; no universal technology mastery percentage is planned.

## Start with the current learning system

Phase 1 is specified in [system architecture](09-learning-system.md), [learner experience](10-learning-experience.md), and [delivery/verification](11-learning-delivery-plan.md). These documents govern learning scope where older proposals differ. The [implementation record](13-learning-implementation.md) documents the delivered local runtime and its verification; earlier pending statements in the design documents describe their original baseline.

The current repository has a modular backend, PostgreSQL persistence, concept/evidence/work/experience lifecycles, a responsive React workbench, six exercise formats, durable learning sessions, goals, shared roadmaps and qualified evidence projections. Existing HTTP resources use ETags, `versionToken`, `If-Match`, cursor envelopes and Problem Details; older numeric-version transport descriptions have been superseded.

## Read the package

| Document | Decisions and deliverables |
| --- | --- |
| [Architecture and alternatives](01-architecture.md) | Components, module boundaries, stack versions, persistence, events, deployment, licensing |
| [Domain and lifecycles](02-domain-and-lifecycles.md) | Entities, evidence, provenance, approval invariants, context fingerprints, durable jobs |
| [AI and learning](03-ai-and-learning.md) | Curated content, assessment pipeline, providers, correction, scheduling and evaluation |
| [User flows and integrations](04-flows-and-integrations.md) | Complete learning/work/presentation paths, client/server contracts, planned local tooling |
| [Security and privacy](05-security-and-privacy.md) | Trust boundaries, external disclosures, isolation, retention, deletion, export, organizations |
| [Costs and operations](06-costs-and-operations.md) | Verified price inputs, calculated scenarios, cost sensitivity, backup/recovery and monitoring |
| [Roadmap and open decisions](07-roadmap.md) | Dependency-based milestones, completion criteria, spikes, scope cuts and requirement mapping |
| [Offline clients and expansion](08-offline-clients-and-expansion.md) | Required disconnected learning, native client options, synchronization, CLI/enterprise expansion and backend evolution |
| [Learning system](09-learning-system.md) | Agreed phases, technology-independent model, module ownership, evidence/cooldown policy, transaction and API contracts |
| [Learning experience](10-learning-experience.md) | Topic workspace, all six exercise interactions, fixed-count sessions, feedback, roadmap navigation and recovery |
| [Learning delivery plan](11-learning-delivery-plan.md) | Ordered implementation increments, fixture requirements, acceptance matrices and verification evidence |
| [Learning implementation record](13-learning-implementation.md) | Delivered Phase 1 behavior, setup, verification and remaining boundaries |
| [UI design documentation](ui/README.md) | Selected Coastal/cobalt SPA guidelines, visual identity, component handoff, and working demo reference |
| [Original learning specification](inputs/learning-system-specification.md) | Unchanged user-supplied input; the discussion refinements are recorded in the learning system decision table |

## Scope and assumptions

The initial proposal was written for an empty directory on 17 September. Source now exists; use the current code and the learning baseline inventory rather than treating that historical observation as current. The original [product brief](inputs/product-brief.md), [architecture direction](inputs/architecture-direction.md), and new learning input remain preserved sources. The core direction remains one backend/database, optional organizations, shared evidence, and future tools that can keep proprietary source away from the backend.

| Working assumption | Consequence | When to revisit |
| --- | --- | --- |
| Confirmed: creator reports proficiency from about a year on a large ASP.NET Core microservices/RabbitMQ/PostgreSQL project | Use professional module boundaries, transactions, observability and reliable messaging where justified; do not restrict the architecture on assumed lack of experience | Revisit topology when independent deployment/consumer requirements emerge |
| Confirmed: local deployment and no paid dependency | Core learning needs no model; optional later inference has strict free-only routing and server-side secrets | Hosting and any paid inference are separate future decisions |
| Initially use synthetic/public examples | No employer integration or private repository access assumed | Before real work material is accepted |
| Confirmed: web first; useful offline learning and native/tooling expansion remain planned | Phase 1 pins content/fact identities; working offline packs/storage/sync arrive in Phase 4; Android then Windows remain later platform priorities | Native feasibility and later milestone details |
| Confirmed: experienced developers and subject-independent engine | Topic workspace and 3/5/10-item practice; C# fundamentals are test data only | Production content supply is a separate concern |
| Confirmed: English prototype, Russian and possibly other languages later | Externalize UI messages now; version content translations and evaluate new answer languages later | Before enabling each additional language |
| Low/moderate traffic means 10/100 monthly active users for budgeting | Small hosted deployment; these are scenarios, not forecasts | After measuring a real vertical slice |
| Confirmed: a few months to first minimal version, then additional months of development | Two delivery windows: protected complete core and planned expansion; pull expansion forward if demonstrated throughput permits | Exact dates, weekly availability, rubric and future hosting budget/region remain open |

For the first learning milestone, the critical checks are exercise interaction, durable submissions, trustworthy evidence, retries and owner isolation. Provider quality/privacy/quota is a later optional-AI spike and cannot block deterministic learning. No GPU or local inference is required. Test fixtures must never be represented as live assessments of real answers.

## Learning phases and broader product boundary

Learning phases are: **1 foundation; 2 adaptation; 3 intensive/exam; 4 offline; 5 integrations; 6 optional AI**. Phase 1 includes a topic workspace, six exercise types, immediate feedback, goals, mixed/scoped practice, evidence summaries and one test roadmap. Scheduled review, decay and dimensions wait for Phase 2. Deferred exam feedback waits for Phase 3.

The broader product still includes reflection, explicit acknowledgement, factual approval, experience reuse, privacy/recovery and native/tooling/organization expansion. Their historical milestones are in [the broader roadmap](07-roadmap.md). They are not additional prerequisites for the first learning release. Automatic publication and compulsory comprehension gates remain outside the approved behavior.

The immediate learning loop is **topic → optional material → fixed-count session → saved answer → deterministic feedback or labeled self-review → evidence/history → shared roadmap coverage**. Later schedules, integrations and clients reuse those facts. Content sourcing and production authoring are separate work; the runtime consumes versioned declarative content.

## Evidence and limits

Links next to factual claims point to official technical/provider documentation or primary research. Prices and version support were retrieved on the research date; they can change. Numeric workloads, token envelopes, operational targets and scheduling intervals are design estimates, not measured results. No model evaluation, dependency installation, model download, paid call, integration installation or deployment was performed during this phase. Proposed spikes explicitly identify what still needs verification. The user's follow-up budget and language instructions override provisional assumptions in the original brief.
