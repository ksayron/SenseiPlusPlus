# Initial architecture and implementation proposal

**Research date: 17 September 2026. Status: recommended design, awaiting implementation.**

Build an individual-first responsive web application around a C#/.NET modular monolith, PostgreSQL, and a **configurable AI provider, initially using free models through OpenRouter**. **The application and database run locally; inference is external and must incur no service spending before hosting.** A developer studies or reflects, supplies an explanation, receives contestable feedback, revisits related reasoning, and approves a reusable experience account. These activities contribute different kinds of evidence to one personal profile. Neither a model verdict nor session completion constitutes acknowledgement or achievement approval.

The most important design decision is to preserve **user input, model assessment, and explicit approval as separate versioned records**. This makes recovery, correction, privacy, and honest career reuse possible without a universal mastery score.

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

## Scope and assumptions

The directory was empty when inspected; no repository instructions, application, dependency manifests, or documentation conventions were found. Both supplied documents were read. The architecture direction strengthens the baseline: one primary backend/database, optional organizations, AI as infrastructure, shared evidence, and a future local tool able to keep repository context away from the backend. The optional earlier strategy document was not needed or used.

| Working assumption | Consequence | When to revisit |
| --- | --- | --- |
| Confirmed: creator reports proficiency from about a year on a large ASP.NET Core microservices/RabbitMQ/PostgreSQL project | Use professional module boundaries, transactions, observability and reliable messaging where justified; do not restrict the architecture on assumed lack of experience | Revisit topology when independent deployment/consumer requirements emerge |
| Confirmed: local deployment, free external models, configurable provider | Local application/database; server-side provider key; strict zero-price routing; no top-up or paid fallback | Hosting and any paid inference are separate future decisions |
| Initially use synthetic/public examples | No employer integration or private repository access assumed | Before real work material is accepted |
| Confirmed: useful offline learning; Android + Windows first; CLI/IDE and enterprise expansion intended | Design client storage/sync now; first checkpoint includes downloaded offline learning; native/tooling/organization tracks can accelerate | Additional platforms follow later priorities |
| Confirmed: English prototype, Russian and possibly other languages later | Externalize UI messages now; version content translations and evaluate new answer languages later | Before enabling each additional language |
| Low/moderate traffic means 10/100 monthly active users for budgeting | Small hosted deployment; these are scenarios, not forecasts | After measuring a real vertical slice |
| Confirmed: a few months to first minimal version, then additional months of development | Two delivery windows: protected complete core and planned expansion; pull expansion forward if demonstrated throughput permits | Exact dates, weekly availability, rubric and future hosting budget/region remain open |

The largest immediate uncertainty is **whether an available free endpoint combines useful English assessment, sufficient quota, structured output and acceptable data handling**. The provider/model spike resolves that combination rather than assuming that “free” guarantees availability or privacy. No GPU or local inference requirement remains. Use deterministic fixtures for most development, with a small quota-aware set of live calls; fixtures must be labeled and never represented as live model evaluations.

## Recommended graduation boundary

Include one complete learning path with downloaded offline lessons/scenarios, durable local attempts and later synchronization; manual work input with optional selected excerpts; context correction; focused reflection; explicit acknowledgement; approved journal revisions; one interview-story format and reviewer-note export; evidence-linked profile; bounded practice queue; failure recovery; privacy/export/deletion; and practical evaluation.

Plan native clients, CLI, an IDE entry point and a bounded organization feature set as expansion tracks in the same product roadmap. Their order depends on the first working slices; they can arrive before the first checkpoint if capacity permits, rather than being excluded from graduation scope. Semantic retrieval, voice, broad repository ingestion and comprehensive enterprise integrations need their own demonstrated use cases. Automatic publication and compulsory comprehension gates remain outside the approved product behavior.

The first slice is deliberately narrow: **one curated mechanism → saved answer → feedback → inspectable evidence → scheduled variant → manual personal-project entry → approved story**, followed immediately by the disconnected pack/save/sync path. Work reflection and additional clients reuse these foundations. A proposal for that slice is not a claim that it is already built.

## Evidence and limits

Links next to factual claims point to official technical/provider documentation or primary research. Prices and version support were retrieved on the research date; they can change. Numeric workloads, token envelopes, operational targets and scheduling intervals are design estimates, not measured results. No model evaluation, dependency installation, model download, paid call, integration installation or deployment was performed during this phase. Proposed spikes explicitly identify what still needs verification. The user's follow-up budget and language instructions override provisional assumptions in the original brief.
