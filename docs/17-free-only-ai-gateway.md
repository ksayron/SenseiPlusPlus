# Free-only model gateway and deferred provider pilot

Issue [#7](https://github.com/ksayron/SenseiPlusPlus/issues/7), local implementation on 3 October 2026.
The user requested a separate branch and deferred AI configuration and live testing.

## Delivery boundary

`feat/7-free-only-ai-gateway` starts from `main`, independently of the local authentication
branch. It adds a provider-neutral `IModelGateway`, an OpenRouter adapter, an exact-input
fixture adapter, offline contract tests and a standalone pilot/review tool. It does not configure
a key, select a live route or enable AI in the web application. Core Phase 1 learning remains
AI-independent. Durable jobs and application assessment integration belong to #12.

The twelve-case live pilot, provider/privacy decision and human quality review remain pending.
The issue is open; synthetic replay is not proof of model quality or free-endpoint availability.

## Contract and authority

The shared Application assembly defines immutable request/route/result values. Provider-specific
HTTP/JSON lives in `Sensei.BuildingBlocks.Ai`; modules do not depend on it. A trusted application
supplies `IModelOutputContract`: the exact JSON Schema, its version and a semantic validator.
Both output modes require validation. The gateway also rejects non-object output and duplicate
property names. A schema-mode flag never substitutes for application validation.

Routes pin a model ID ending in `:free`, one endpoint tag, the expected provider name, output
mode, privacy-policy URL/check date, input/output bounds and timeout. Route and request digests
supplement human-readable revision IDs so reused labels cannot conceal changed snapshots.
Future jobs must retain the route/input snapshot rather than resolve mutable current settings.

Results retain known actual model/provider identities, finish reason, bounded token/cost metadata,
elapsed time, route/request digests, contract version and Provider/Fixture origin. Error results
carry a safe category without raw provider error bodies. Request/result `ToString()` omits prompt
and output bodies. API keys come from a server-side callback and never enter route/report values.

Requests default to Restricted. The adapter accepts explicitly labeled public synthetic inputs only. Restricted/private material is rejected before
HTTP. It has no database, filesystem, tool, publication or approval privileges. An assessment
result cannot itself approve facts or create trusted evidence.

## Price, privacy and transport guards

Before **each** completion, the adapter reads the pinned model's current endpoint metadata.
It requires an exact live endpoint identity, token bounds and supported parameters. Prompt,
completion and request prices must all be explicitly present and exactly zero. Every additional
advertised pricing field must also be parseable zero; missing/null, duplicate, unknown, negative
or nonzero pricing fails closed. Nonzero amounts too small for decimal precision are still rejected.

Requests pin `provider.only`/`order`, disable fallback, require parameter support and set zero
prompt/completion/request price ceilings. They request text only, omit tools/plugins, and apply
`data_collection=deny` plus `zdr=true`. A provider without that intersection fails rather than
silently relaxing privacy. The local privacy check expires after 30 days. A policy URL/date does
not prove a provider's legal terms; an operator must review router and upstream policies before
configuration.

The base URL is fixed to OpenRouter's HTTPS API; operators cannot supply arbitrary endpoints.
The default HTTP transport disables redirects. There are no application retries, paid fallbacks,
model routers, JSON repair calls or automatic top-ups. The maximum timeout is 60 seconds,
including waiting for the shared concurrency limit (default two, maximum four).

StrictJsonSchema requires both `response_format` and `structured_outputs` capability metadata.
ValidatedJson sends the schema as an instruction, omits unsupported format flags and requires
the same parser/semantic validator. `ProbeAsync` checks a public contract example through the
same guarded path; passing it proves structure only, not assessment quality.

Input uses a conservative UTF-8 byte upper bound, including instructions/schema and envelope
reserve, capped at 8,000 bytes. This is deliberately not a claim of exact tokenizer counting.
Output is capped at 1,500 tokens and response bodies at 256 KiB. Refusal, truncation, malformed
JSON, wrong identity, unsupported capability, timeout, cancellation, quota/rate failure and
nonzero reported cost return failure without feedback or inferred negative learning evidence.

Read-only metadata research on 3 October found that the historical NVIDIA candidate omitted
an explicit request price and advertised neither schema capability. This implementation would
block it rather than assume omitted prices are zero. No inference or key configuration was
performed. Configuration must establish a currently usable price/privacy/capability intersection.
See the [retrieved endpoint metadata](https://openrouter.ai/api/v1/models/nvidia/nemotron-3.5-lightning:free/endpoints).

## Offline pilot and later review

The pilot has 24 draft public examples: four each for sound reasoning, misconception, valid
alternative, missing constraints, assisted revision and hostile instruction. The first twelve
use two cases per category; twelve remain held out. Source rules link Microsoft's value-type,
reference-type and cooperative-cancellation documentation. These are evaluation drafts, not
a reviewed/published learning pack or human-quality sign-off.

```powershell
dotnet restore backend/Sensei.sln --locked-mode
dotnet build backend/Sensei.sln -c Release --no-restore
dotnet run --project backend/tools/Sensei.Ai.Pilot -c Release --no-build -- --fixtures .tmp/ai-fixture-pilot.json
dotnet test backend/tests/Sensei.UnitTests -c Release --no-build
```

Fixture output is bound to the complete input/schema digest, explicitly marked Fixture and
never used as a provider fallback. Unknown inputs fail. The fixture report verifies plumbing;
it cannot pass live-route review.

When configuration/live testing is explicitly resumed, an operator can supply a route snapshot
file and `OPENROUTER_API_KEY` in the local environment, then run:

```powershell
dotnet run --project backend/tools/Sensei.Ai.Pilot -c Release --no-build -- --live <route-file> <report-file>
dotnet run --project backend/tools/Sensei.Ai.Pilot -c Release --no-build -- --review-template <report-file> <reviews-file>
dotnet run --project backend/tools/Sensei.Ai.Pilot -c Release --no-build -- --review <report-file> <reviews-file> <reviewed-file>
```

No route file or key is created by this delivery. Route fields are the `ModelRoute` constructor
fields in the shared contract; JSON rejects unknown fields, missing required constructor fields
and integer enums. One run evaluates at most twelve public cases on one route. Compare at most
two separately approved routes. The tool stops early for price, configuration, capability,
transport or quota failure and makes no repair/retry calls.

Review JSON identifies `reviewer`, `reviewedAt` and all twelve `cases`, each with `caseId`,
`acceptable`, `unsafeApproval`, `inventedClaim` and explanatory `notes`. Structural/provider
failures cannot be accepted as technical feedback. Selection eligibility requires at least 10/12
acceptable judgments and zero unsafe approvals/invented claims. It records the judgment and
does not install/configure a route. Record endpoint privacy, parsing, latency and limitations
alongside quality judgments before any application integration.
The review template leaves every judgment undecided (`null`); enter explicit decisions and notes
after inspecting each case. An unfinished template cannot be accepted as a review.

## Verification

Local verification on 3 October 2026 passed locked restore, the Release solution build (zero
warnings/errors), 55 unit tests, 3 architecture tests and 25 PostgreSQL integration tests.
The standalone fixture run recorded 24/24 successful synthetic replays with `humanReviewed=false`.
The generated OpenAPI contract has no content changes. Browser/client behavior was not changed
by this branch; its new behavior is exercised through the library and console contract tests.
Live inference, endpoint qualification, human quality review and GitHub CI remain unverified.

Offline tests cover price
changes between calls, underflow amounts, endpoint and identity pinning, schema negotiation,
malformed/refused/truncated output, reported costs, bounded responses, concurrency, cancellation,
fixture input binding, pilot holdout separation and human-review gates.

## Primary sources

- [OpenRouter model metadata](https://openrouter.ai/docs/api/api-reference/models/list-all-models-and-their-properties)
- [Provider routing and price/privacy controls](https://openrouter.ai/docs/guides/routing/provider-selection)
- [Structured outputs](https://openrouter.ai/docs/guides/features/structured-outputs)
- [Chat completion contract](https://openrouter.ai/docs/api/api-reference/chat/create-a-chat-completion)
- [C# value types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/value-types)
- [C# reference types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/reference-types)
- [Cooperative cancellation](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads)
