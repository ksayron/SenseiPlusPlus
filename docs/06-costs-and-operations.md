# Cost and operating model

**Research date: 17 September 2026. Currency: USD. Prototype requirement: no service spending before hosting.** Prices below are reference inputs retrieved from official sources. Workloads, token counts and operating targets are estimates. No services were purchased or provisioned.

## Prototype: zero mandatory service bill

Run ASP.NET Core and PostgreSQL locally, build the frontend locally, use a server-side key for a selected zero-price OpenRouter route, and run deterministic AI fixtures for routine development. No local model/GPU, cloud database, paid IDE component, external analytics, SMTP subscription, commercial queue feature, paid API fallback or domain purchase is required.

Existing hardware, disk, electricity and network access are assumed; this is zero new mandatory service spending, not zero resource consumption. Model keys/accounts and relevant service terms still need setup in the implementation phase. No automatic account creation, credit purchase or paid trial conversion is part of this proposal.

The expanded scope retains this constraint. Offline packs and on-device practice incur no inference calls; only explicitly requested later assessment consumes free quota. Native/CLI builds must use existing equipment and available local tooling. Android and Windows are the confirmed first targets; app-store distribution, signing, later iOS build hardware and any store-account fees require a separate verified budget/access plan, not an assumption that “native” is cost-free on every platform. Initial private device testing can precede store release. No public store account or paid build service is provisioned by this design.

OpenRouter's official guidance describes an unpaid free-model allowance of **50 requests/day and 20/minute**; a higher allowance involves purchasing credits and is therefore outside this prototype budget. Treat the current account's reported quota as authoritative, and expect endpoint-specific capacity restrictions. [Official free-tier guidance](https://openrouter.ai/blog/tutorials/any-coding-agent/). The limits API documentation describes account-level remaining free requests, quota errors and reset/retry information. [Limits and quota status](https://openrouter.ai/docs/api_reference/limits).

Reserve up to 40 of the nominal 50 daily calls for normal prototype activity, leaving ten for repair/evaluation if available; other applications sharing the account can consume that reserve. At six calls per work reflection, 40 calls support at most six such sessions plus a few extra calls, not 40 users. Queue requests, show remaining allowance when known, and keep manual/curated flows available after exhaustion. Never create additional accounts/keys as a quota workaround or automatically purchase a larger allowance.

The catalogue and candidate endpoint list $0 input/output pricing. Availability is conditional and can change; the application must check its zero-price policy before inference and stop if a route becomes paid or unknown. [Free models](https://openrouter.ai/collections/free-models), [candidate price](https://openrouter.ai/nvidia/nemotron-3.5-lightning:free).

## Explicit workload scenarios

Token envelopes are **totals across all calls in that workflow**, not per-call values; repeated context and rubric text are included in input estimates. Output must include billed reasoning tokens where applicable, not just visible text.

| Workflow | Calls | Total input tokens | Total output tokens | Included behavior |
| --- | ---: | ---: | ---: | --- |
| Learning session | 2 | 6,000 | 1,500 | Two assessed attempts; curated lesson/variant selection |
| Work reflection | 6 | 24,000 | 6,000 | Interpretation, question plan, three assessments, journal draft |
| Story generation | 1 | 2,000 | 600 | One approved entry to concise account; deterministic export |

| Monthly case | Learning / work / stories | Base calls | Calls with 20% retry/extra allowance | Input / output with allowance |
| --- | ---: | ---: | ---: | ---: |
| Low: 10 active users, each 8 learning + 2 work + 1 story | 80 / 20 / 10 | 290 | 348 | 1.176M / 0.2952M |
| Moderate: 100 active users, each 12 learning + 4 work + 1 story | 1,200 / 400 / 100 | 4,900 | 5,880 | 20.4M / 5.112M |

Low use averages about 12 calls/day over 30 days but can exceed quota during a group demo. Moderate use averages 196/day and **does not fit a shared unpaid 50/day allowance**, even before burst/capacity problems. A theoretical 1,500 calls/month assumes every day is usable and does not let unused daily quota carry forward. Under free-only policy, excess work waits or uses manual/curated paths; it does not generate an invoice by silently switching providers.

These are online-use scenarios, not forecasts for the expanded ecosystem. Returning travelers can create a sync burst: save their input first, bound batch size and queue only selected AI assessments. Offline authored practice can increase useful usage without proportional model calls. Additional native clients, organization content and integration workers change storage/support load; revise hosted sizing after the first real multi-client measurements.

A second independently permitted free service could improve resilience, but its quota/privacy/quality must be researched and configured explicitly. Do not promise that enough free capacity will always exist. If free access disappears, development and deterministic demonstration remain possible, while live AI functionality is openly unavailable until another acceptable free route exists.

## Later hosting reference, not a purchase recommendation

For the first hosted demo, budget a conventional CPU VM for the app and database; inference still runs externally. DigitalOcean is a transparent price benchmark, not an assertion about the user's account, region or purchasing eligibility. Its Basic regular 2 GiB/1 vCPU plan lists $12/month and 4 GiB/2 vCPU lists $24/month. Start the hosted estimate with the latter for app/database headroom. [Droplet prices](https://www.digitalocean.com/pricing/droplets).

Encrypted off-host backups could use an object-store service; DigitalOcean Spaces lists $5/month including 250 GiB. This is unnecessary spending during the local-only phase; use existing separate storage if available. [Spaces pricing](https://docs.digitalocean.com/products/spaces/details/pricing/).

Thus the reference hosted base is **$24 + $5 = $29/month**, before domain, taxes, additional transfer and optional email. The VM's included disk hosts PostgreSQL, so do not add a managed database charge to the same baseline. Prefer managed PostgreSQL when the operator cannot maintain backups/upgrades; price the actual region and storage then. A single VM is not high availability.

For comparison only, if paid inference is explicitly approved in the future, OpenAI's retrieved Standard short-context rates for GPT-5.6 Terra are $2/M input and $12/M output; GPT-5.6 Luna lists $0.20/M and $1.20/M. These establish arithmetic examples, not equivalent educational quality or authorized access. [OpenAI pricing](https://developers.openai.com/api/docs/pricing).

Formula: `monthly AI cost = input millions × input rate + output millions × output rate`. Using Terra without cache/batch discounts:

| Case | Reference paid AI cost | Hosted base + paid AI | Hosted base + strictly free AI |
| --- | ---: | ---: | --- |
| Low | $5.89 | $34.89/month | $29/month, subject to daily quota and endpoint suitability |
| Moderate | $102.14 | $131.14/month | $29/month infrastructure, but workload exceeds stated unpaid allowance |

These estimates exclude tax, domain, email, independent evaluation runs, cache-write charges, tool fees and long-context pricing. No such paid tools or cache-write feature is enabled in the prototype. Provider/deployment access must be checked before using any hosted comparison. For example, Belarus is absent from the retrieved [OpenAI supported-country list](https://developers.openai.com/api/docs/supported-countries); timezone alone does not establish eligibility. Do not assume that hosting elsewhere solves service eligibility.

## Sensitivity and controls

At the paid comparison rate, one reflection costs `0.024 × 2 + 0.006 × 12 = $0.12` before retry allowance. Doubling its input increases it to $0.168; doubling both input and output increases it to $0.24. Each extra assessment at 4,000 input/1,000 output adds $0.02 and one free-quota call. Free pricing eliminates the token bill, not the latency, quota or context-quality penalty of excess prompts.

Save usage/finish status and route/model identity for each invocation. Treat unknown usage as unknown, not zero. Count retries, repair calls and timeouts conservatively against local quota reservations; reconcile with provider counters. Start with one in-flight provider request and fair per-user queuing. Budget each session's calls before starting optional generation, and do not spend the last allowance on a background summary while an answer awaits feedback.

Server config locks `FreeOnly=true`. Check allowlisted model ID and all relevant zero-price fields, disable paid plugins/tools/fallbacks and fail closed on incompatible pricing data. A paid route requires an explicit configuration/policy change in a later authorized phase; changing a model name in the browser cannot enable it. Credentials remain server-side. No provider SDK's hidden retry policy should multiply the application's retry ceiling.

## Development and local demo operations

Use local .NET/Node tooling with either native PostgreSQL or a database container. Keep the backend on the host initially to simplify loopback networking. Vite's development proxy forwards API requests; the packaged demo serves built assets from ASP.NET. A setup guide should document versions, database creation/migrations, secret setup, fixture mode, live free mode and reset of disposable demo data.

The machine can sleep or close the application; durable jobs remain in PostgreSQL. On startup, recover expired leases, process retention/delete requests and recompute due practice. No notifications are assumed while the app is off. Export works without a model. Demo fixtures are explicitly labeled and use separate sample accounts/database from private material.

Downloaded learning works even while that backend machine is off: the PWA/native client runs lessons, stores attempts and maintains provisional scheduling on the device. Synchronization waits until the configured backend is reachable. The local-only phone demo can download/sync on a trusted LAN using a deliberately configured development endpoint, then operate offline elsewhere; the database remains private and no public tunnel is required. Remote sync away from that LAN eventually requires reachable hosting or another explicitly designed connectivity arrangement.

Configuration groups: database connection, encryption/data-protection storage, provider route/secret, `FreeOnly`, maximum calls/tokens/concurrency, retention settings and UI/content locale. Commit only example configuration. Do not bundle production keys into client assets or logs.

## Monitoring and recovery

| Concern | Minimal measure / procedure |
| --- | --- |
| Availability | App health and database readiness; provider status separate so AI outage does not make saved data unavailable |
| Job recovery | Pending age, running lease age, retry/failure counts, obsolete/cancelled counts; test process termination mid-job |
| Free quota | Local reservations, provider remaining allowance if available, 429/402/no-endpoint errors; safe stop, no auto-top-up |
| Feedback quality | Schema failures, disputed/revised feedback, invented-claim incidents; inspect only voluntarily shared redacted examples |
| Responsiveness | Save-input latency and time to useful feedback, median/p95; target save under 500 ms locally excluding pathological load |
| Storage | Database growth, backup age, free disk; rotate logs and purge redundant payloads |
| Backups | Daily encrypted `pg_dump` to existing separate storage, seven copies; credentials/keys backed up separately and protected |
| Recovery drill | Restore to a fresh database, replay deletion ledger, verify two-user isolation, approved revisions, pending-job recovery and source removal; reconnect a stale device without resurrecting deleted content |
| Client sync | Pending-operation age, per-operation conflicts/rejections, pack completeness, cursor resets and safe local schema upgrades; no raw content in telemetry |

Local demo recovery target: at most one day's data loss after a successful daily backup, restore within two hours by following the guide. These are intended RPO/RTO targets to test, not guarantees. A backup on the same failing disk is not off-device protection. At hosting, use encrypted off-host copies, alert on a missed daily backup, rehearse restoration and adopt point-in-time recovery if tighter data-loss needs justify it.

Deploy migrations as a controlled step with backup and rollback/forward-fix notes. Persist data-protection keys across restarts. Keep application/OS/database patches current, with a small recurring maintenance allowance; estimate 1–2 hours/month after stabilization plus variable incident/content-review time, not a promise. No Kubernetes, service mesh or observability cluster is needed.
