# Authenticated local evidence import

The Windows CLI can capture, annotate, preview, confirm, queue, manually synchronize, and open an imported Experience draft. This increment advances issues [33](https://github.com/ksayron/SenseiPlusPlus/issues/33) and [34](https://github.com/ksayron/SenseiPlusPlus/issues/34); neither is fully delivered. Collection remains independent of the server, OAuth, PostgreSQL and AI. Watching never sends evidence.

## Setup

Requires Windows, Git and the pinned .NET 10 SDK. Follow [local collection setup](20-local-evidence-agent.md) and existing backend/PostgreSQL setup. Restore and build from this checkout:

The standalone agent solution lives in [`agent-spp/AgentSpp.sln`](../agent-spp/AgentSpp.sln), with its own build settings, SDK pin, package versions and tests. Backend OAuth/import endpoints and web pages remain in their respective applications. Both applications use the dependency-free contracts under `shared/Sensei.Client.Contracts`; agent production code has no backend project references.

```powershell
dotnet restore agent-spp/AgentSpp.sln --locked-mode
dotnet restore backend/Sensei.sln --locked-mode
dotnet build agent-spp/AgentSpp.sln -c Release --no-restore
dotnet build backend/Sensei.sln -c Release --no-restore
dotnet ef database update --project backend/src/Sensei.Host --startup-project backend/src/Sensei.Host
function agent { dotnet '.\agent-spp\src\Sensei.LocalAgent.Cli\bin\Release\net10.0\sensei-agent.dll' @args }
```

The migration is additive: OpenIddict applications/authorizations/tokens, CLI grants, immutable import records, and assistance/uncertainty on journal revisions. Apply it deliberately to the intended database. No account passwords or existing evidence are replaced.

Configure `AgentOAuth:Issuer` to the externally reachable HTTPS root and `AgentOAuth:WebOrigin` to the SPA origin. Production requires an OAuth signing/encryption PFX through `AgentOAuth:CertificatePath` and `AgentOAuth:CertificatePassword`; provide the password through the deployment secret mechanism. Development certificates persist in the Windows certificate store so a backend restart preserves active sessions. Testing uses ephemeral keys. The public client is registered lazily on the first authorization request.

For the existing local Vite workflow only, set these variables in the backend terminal before starting it:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:AgentOAuth__Issuer = 'http://localhost:5173/'
$env:AgentOAuth__WebOrigin = 'http://localhost:5173/'
$env:AgentOAuth__AllowInsecureLoopback = 'true'
dotnet run --project backend/src/Sensei.Host --no-launch-profile --urls http://localhost:5062
```

Start Vite on port 5173 in a separate terminal. Its proxy forwards `/api`, `/.well-known` and `/connect/` to port 5062. Keep the configured issuer identical to the address passed to the CLI; direct backend URLs and Vite URLs are different profiles. The HTTP exception works only in Development on loopback. Never use it for a remote server.

## Consent and credentials

```powershell
agent auth login --server https://your-sensei-host.example/
# Explicit local Development exception:
agent auth login --server http://localhost:5173/ --allow-insecure-loopback
agent auth status
```

OpenIddict's client integration launches the system browser and listens on a dynamically allocated localhost port. Authorization code flow requires S256 PKCE and validates the correlated state and identity token. Sign-in has a five-minute deadline; Ctrl+C cancels it. The browser shows the signed-in account, CLI identity and requested permissions before a cookie-authenticated, CSRF-protected consent action. A restart during pending consent requires restarting login.

Permissions are limited to identity/profile, offline access, concept lookup, import and receipt lookup. CLI bearer credentials cannot use ordinary cookie APIs, approve experience or change evidence status. Five-minute access tokens and rotating refresh credentials are protected by DPAPI CurrentUser. Grants expire absolutely after thirty days; refresh does not extend that deadline. API calls check token/authorization status and the account security stamp, so password recovery invalidates the connection.

Use the web **Connections** page to inspect and revoke grants. Revocation requires recent password or linked-provider confirmation; see [external authentication](22-external-authentication.md). `agent auth logout` clears local credentials even if remote revocation fails; it reports that failure so the grant can be revoked on the web. Local logout does not delete queued submissions.

## Review and queue

Register and capture a selected repository using the collection guide. Inspect facts, then write your own title, context and contribution notes:

```powershell
agent inbox show CANDIDATE_ID
agent inbox edit CANDIDATE_ID --revision CURRENT_REVISION
agent concepts
agent submit CANDIDATE_ID --preview
agent submit CANDIDATE_ID --confirm EXACT_PREVIEW_TOKEN
agent outbox list
agent outbox show EVENT_UUID
agent sync
```

JSON notes may include `title` and `context` alongside the original role/actions/alternatives/outcome/uncertainty/assistance/conceptIds fields. Legacy encrypted candidate records retain their existing notes and read through the versioned reader; title and context must be added before import. Existing `SanitizedEventDraftV1` file exports remain unchanged and are not accepted by the import API.

Preview displays the exact sanitized payload, issuer, account, destination and working-context freshness. Confirmation binds the reviewed payload, candidate revision and manifest fingerprint to that account and issuer. Confirmation queues an encrypted item atomically; **only `sync` sends it**. Watching, capturing and confirming do not submit contributions.

Requests use strict `ProfessionalEventImportV1`: schema 1, stable event UUID, work setting, UTC capture calendar date, title, context, contribution fields, declared assistance, distinct concept UUIDs and `client_reported`. Unknown/duplicate properties, invalid UUIDs, unsupported versions and recognized credentials/source payloads are rejected. Maximum request is 64 KiB; title is 240 characters, other prose 4,096 characters, concept selections 100. Automated checks cannot prove arbitrary prose nonsensitive: review every field before confirmation. Logs omit tokens, authorization queries, source and contribution prose. Explicit inbox/outbox/preview inspection intentionally prints reviewed content.

## Recovery and destination binding

Backend downtime leaves the confirmed item queued for a later manual `sync`. Every item retains its original issuer and account. Signing into another account/server cannot reroute it; inspect the outbox and sign into its original destination to deliver it. Creating a different destination requires another preview and confirmation.

Synchronization uses a separate profile lock without holding the state lock during HTTP requests. Delivery is marked uncertain durably before POST; receipts are persisted afterward. On the next sync, receipt lookup reconciles accepted events before any resend. An identical event replay returns its original receipt even after web editing. A changed payload under an accepted UUID returns `409 event_payload_conflict`; the CLI never replaces an uncertain or imported item silently.

Changed notes, changed/incomplete/unavailable working context require recapture and renewed review. Expiring or purging optional source alone does not invalidate unchanged reviewed notes and manifests. HTTP failures and rate limits require a later manual invocation; there is no background retry service. Pending unsent payloads can be replaced only after renewed confirmation.

```powershell
agent outbox cancel EVENT_UUID
agent inbox delete CANDIDATE_ID --revision CURRENT_REVISION
```

Deleting a candidate cancels its pending local submissions. A canceled queue item remains an encrypted audit record and may reconcile an already accepted delivery; it sends no new import. It does not delete a server journal entry. Imported submissions are immutable locally; make corrections through ordinary web revisions and use the web lifecycle separately.

## Inspecting the draft and learning boundary

The receipt provides a direct `/experience?entry=...` link. The web journal shows **Client reported · Not assessed**, the coarse event date, immutable original imported revision, and current editable draft. Assistance and uncertainty remain available in subsequent revisions. Journal approval is a separate version-bound browser action.

Import creates one unscored `ProfessionalExposure` observation per selected active catalog concept, with a typed import source and structured assistance explicitly `Unspecified`. Declared assistance prose remains on the revision. Import creates no knowledge contributions, self-review allowances or trusted assessments, and cannot change learning scores or assessment eligibility. Ownership comes solely from the authenticated identity. Import record, journal revision and observations commit in one transaction with a unique owner/event key and canonical payload digest.

## Verification and limits

Tests cover disposable Git capture, DPAPI, stale review, exact confirmation, account binding, lost-response receipt reconciliation, concurrent import replay, strict rejection, PKCE/state/nonce rejection, callback cancellation/timeout, rotating/revoked credentials, password recovery, absolute grant expiry, browser CSRF, journal provenance and responsive inspection. A Windows integration journey uses the real OpenIddict client and native dynamic callback with a controlled browser transport, switches between two accounts, stops/restarts the backend, and inspects the resulting draft. It substitutes browser interaction for automation; OS browser launching and a human consent click remain manual checks.

Windows CI uses the runner's native PostgreSQL binaries in a disposable cluster. `SENSEI_TEST_POSTGRES` optionally supplies an explicitly disposable database for integration tests; the fixture applies migrations there and never drops that externally supplied database. Without it, tests use disposable PostgreSQL Testcontainers. Do not point it at a development or production database.

Locked restore and Release build passed with zero warnings. Validation covered 56 unit tests, four architecture checks, 48 agent tests and 55 PostgreSQL integration cases, using focused reruns after fixes. Client lint, seven unit tests, production build and 27 consent/journal/theme browser journeys passed. EF reports no pending model changes, and generated OpenAPI is consistent. Browser checks used the locally installed Node 22 runtime; the repository and CI remain pinned to Node 24. The new Windows CI workflow has not been run remotely because this delivery does not push changes.

On 4 October 2026, the complete implementation was brought into the primary project folder and the agent was separated into `agent-spp`, preserving the existing security edits. Locked restores passed for both solutions. The relocated agent's 48 tests, four architecture checks, 60 backend unit tests and 60 PostgreSQL integration cases passed; client lint, nine unit tests, production build and 15 responsive consent/journal/security browser journeys passed. The CLI process test now resolves its executable from the standalone agent solution. Windows CI builds and tests the new layout.

This is launch-controlled Windows CLI integration. There is no installer, AI analysis, IDE/MAUI integration, general learning offline sync, multi-device recovery or server-side source upload. Concept lookup currently returns at most the first 1,000 active catalog concepts. The browser and backend must be reachable for login and synchronization; collection continues with them stopped.
