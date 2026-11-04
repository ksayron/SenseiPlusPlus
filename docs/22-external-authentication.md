# Google/GitHub sign-in and connected profiles

Google and GitHub are optional sign-in methods alongside local email/password accounts.
They use the same stable account UUID, owner-scoped records and eight-hour HttpOnly session.
Provider sign-in requires internet access; local password sign-in does not.

## Account behavior

First-time provider sign-in creates a passwordless account only in Development/Testing,
and requires a verified Google email or verified primary GitHub email. Hosted registration
remains disabled. Existing connected identities can sign in in other environments.
Identities use Google `sub` and GitHub numeric ID, never email or username. Returning users
keep their original Sensei++ email, display name, settings and saved work.

Matching an existing email never claims that account. Sign into the existing account first,
then use **Connections → Connect Google/GitHub**. Linking requires recent confirmation and
successful provider authorization. A provider identity belongs to one account; an account
can connect one identity per provider. Replacement requires disconnecting the old identity.
Profiles refresh during successful provider flows; there is no background synchronization.

Connections supports password confirmation or a fresh authorization through a linked provider.
Choose the same provider identity when confirming. This proves control through the provider's
current session; GitHub may not ask for a new password. After provider confirmation, select
the intended connection action again. Sensitive mutations are not replayed automatically.
Agent revocation uses this confirmation policy for passwordless accounts too.

Disconnect requires confirmation within five minutes and cannot remove the last usable sign-in
method. A disabled provider is not a usable alternative. Identity changes the security stamp
when a provider is removed, invalidating other cookies and stamp-bound agent grants; the
confirmed browser receives a refreshed session. Saved work stays attached to its original owner.
The [local recovery command](16-local-authentication.md#existing-development-profiles-and-recovery)
can provision a password for a passwordless profile without deleting its history.

## Configuration and provider registrations

For this local prototype, keep Google/GitHub credentials in the root `.env`, which is ignored
by Git. Copy `.env.example` only if `.env` does not exist, then fill each provider's `ClientId`
and `ClientSecret` and set its `Enabled=true`. The committed example contains empty credentials.
Edit the file directly whenever a credential changes. Do not use the browser-exposed `VITE_`
prefix for credentials. Incomplete enabled configurations fail startup.

Run `pwsh -File scripts/Start-Local.ps1 -Service Backend` and `-Service Client` from the
repository root in separate terminals. The launcher loads the root `.env` into each child
process; file values override inherited environment and .NET user-secrets values. Direct
`dotnet run` and `npm run dev` do not load this root file. Values are literal, may be enclosed
in single/double quotes, and may contain `#`, `$` or `=`. Put comments on their own lines;
there is no shell interpolation, escape processing or multiline-value syntax.

Use `pwsh -File scripts/Start-Local.ps1 -Service Check` to validate assignments and enabled
credentials without starting servers or displaying credential values. The launcher derives
the database connection from `SENSEI_POSTGRES_*`, unless `ConnectionStrings__Sensei` is already
set, and derives the agent issuer/web origin from `Authentication__External__PublicOrigin`.
`-Service Migrate` applies migrations with the same `.env` settings using the existing Release
build. The launcher was checked with literal `$`, `#`, `=`, quoted and Unicode values, safe
database connection-string quoting, incomplete provider rejection and frontend HTTPS/proxy settings.
Restart the backend after changing provider credentials, enablement or public origin. Availability
uses the validated startup configuration so live file reload cannot enable an unregistered provider
or make it count as an alternative when disconnecting another sign-in method.

| Configuration | Value |
| --- | --- |
| `Authentication__External__PublicOrigin` | Exact browser origin, e.g. `https://localhost:5173/` |
| `Authentication__External__Google__Enabled` | `true` to enable Google |
| `Authentication__External__Google__ClientId` / `ClientSecret` | Google web client credentials |
| `Authentication__External__GitHub__Enabled` | `true` to enable GitHub |
| `Authentication__External__GitHub__ClientId` / `ClientSecret` | GitHub OAuth App credentials |
| `Authentication__External__AllowInsecureLoopback` | Default `false`; optional Development-only HTTP loopback exception |

Create a Google OAuth **web application**, configure its consent screen/test users, and register
**`https://localhost:5173/signin-google`** as an authorized redirect URI. Google requests only
`openid email profile`. Its handler validates token signature, issuer, audience, nonce and expiry.

Create a GitHub **OAuth App**, with homepage **`https://localhost:5173/`** and callback
**`https://localhost:5173/signin-github`**. Request only `read:user user:email`.
Use separate registrations for development and deployment, substituting the real browser origin.
No repository, organization, write or offline-access scopes are requested. Access tokens exist
only during backchannel exchange/profile lookup; access/refresh tokens are not persisted in
Identity token tables, session cookies, profile metadata, logs or browser storage.

## Migration and local HTTPS

Apply the additive migration without resetting PostgreSQL:

```powershell
dotnet ef database update --project backend/src/Sensei.Host --startup-project backend/src/Sensei.Host
```

`ExternalSignInProfiles` adds profile metadata and short-lived flow transactions, and enforces
one provider login per account. Existing users, passwords and work remain intact. If manually
inserted provider logins violate that uniqueness, migration fails rather than merging/deleting
identities; resolve duplicates before applying it.

Trust the development HTTPS certificate once:

```powershell
dotnet dev-certs https --trust
```

The client launcher exports the development certificate to `%LOCALAPPDATA%/Sensei/certs`
if the PEM/key files are missing. It uses `SENSEI_BACKEND_URL` for Vite's backend proxy and
the public origin for the frontend host/port. The default backend is `https://localhost:7082`.
Node 24 is selected from the existing pinned local cache when available; otherwise use the
repository-pinned Node version on PATH. Install client dependencies with `npm ci` first.

Run the backend with the settings from `.env`:

```powershell
pwsh -File scripts/Start-Local.ps1 -Service Backend
```

In a second terminal, run Vite with HTTPS and an HTTPS backend proxy:

```powershell
pwsh -File scripts/Start-Local.ps1 -Service Client
```

Open **`https://localhost:5173/`** consistently. Vite proxies APIs, provider callbacks and CLI
authorization while preserving the browser Host. Its development proxy accepts the local
certificate specifically for HTTPS localhost. Incoming scheme/host must match `PublicOrigin`.
Hosted proxies must preserve the approved host and securely forward HTTPS information before
authentication; a general hosting/trusted-proxy configuration remains separate work.

Existing HTTP development still works with providers disabled. An explicit loopback exception
requires `AllowInsecureLoopback=true` in Development, an exact HTTP `PublicOrigin`, and matching
provider callback registrations. Prefer HTTPS for live checks.

## HTTP and trust boundary

| Method and route | Behavior |
| --- | --- |
| `GET /api/v1/auth/providers` | Enabled provider names/display names; anonymous |
| `GET /api/v1/auth/methods` | Owned `hasPassword` and `profiles`; no tokens/provider keys |
| `POST /api/v1/auth/external/{provider}/start` | `{ purpose, returnPath, timeZone }`; returns `{ redirect }` |
| `GET /api/v1/auth/external/challenge?flow=…` | Single-use middleware challenge |
| `GET /signin-google`, `GET /signin-github` | Middleware GET callbacks |
| `GET /api/v1/auth/external/complete` | Consumes the validated flow/ticket, redirects to `/auth/result` |
| `POST /api/v1/auth/external/{provider}/disconnect` | Recent-confirmed removal of the owned connection; 204 |

Purposes are `signin`, `link`, `refresh`, `reauthenticate`; defaults are `signin`, `/`, `UTC`.
Sign-in starts anonymously; other purposes require the current account. Send `X-CSRF-Token`
on mutations and `X-Expected-Owner` for account-bound actions. Five-minute transactions bind
purpose, browser, account/security stamp and safe return path. State/correlation, PKCE and
Google nonce protect callbacks. Challenge/completion are single-use; expired rows are pruned
on subsequent starts. Restarting requires the same data-protection keys for pending cookies.

Session/external/correlation cookies use SameSite=Lax for GET callbacks. Antiforgery cookies
remain Strict, API mutations remain protected, production cookies require HTTPS, and callbacks
are not cached. Failures use fixed UI messages and allowlisted codes without raw provider data.
On return, the SPA reloads the account, obtains fresh CSRF, broadcasts the account change and
restores the safe route, including pending agent consent. Existing stale-owner checks remain.

GitHub linking is profile metadata only. It does not establish authorship or create observations,
learning scores or approved experience. Future work imports need separate consent, provenance,
repository-selection and token-retention decisions.

## Verification

Integration tests exercise real middleware with fake GitHub backchannels and signed fake Google
tokens against isolated PostgreSQL. Browser provider tests use UI/API doubles; existing account
and learning journeys use the real local backend. These do not prove live registrations work.

Local validation on 2026-10-04:

- Release backend build: zero warnings/errors; locked dependency restore and migration/model check passed.
- Full backend run: 60 unit, 4 architecture and 80 PostgreSQL integration tests passed. After the final
  startup-availability safeguard, all 35 authentication/security/import/migration regressions passed,
  including 21 external-provider cases.
- Client lint, production build and all 9 unit tests passed.
- All 50 distinct desktop/narrow browser journeys passed across the full run and focused reruns,
  including both Coastal themes, Connections, passwordless confirmation, agent-consent return,
  current-account broadcasting and fresh antiforgery retrieval.

The existing owner-switch browser assertions were updated to the stable `resource_not_found` API code;
the prior account's private editor was still removed. One concurrent agent-import run returned 500s;
the isolated three import tests and the subsequent complete 35-test regression run passed. Its import
implementation was unchanged. Provider UI tests simulate authorization redirects; middleware tests
validate the actual OAuth/OIDC handlers with fake remote servers.

### Return navigation and dependency review — 4 November 2026

Return navigation preserves percent-encoded query values, including a learning search in
Cyrillic. Both the SPA and API still reject absolute/protocol-relative destinations,
encoded path segments, control characters, backslashes, dot segments and authentication/API
routes. The provider flow stores the original query and returns it without decoding it into
a different destination. Regression checks cover both provider callbacks and the sign-in UI.

The integration-test graph explicitly pins `System.Security.Cryptography.Xml` 10.0.13,
which also brings `System.Security.Cryptography.Pkcs` 10.0.13. OpenIddict's transitive 10.0.11
floor otherwise fails the repository's current NuGet audit. The web host uses the shared
framework for these assemblies; this test-package override does not update the installed
ASP.NET Core runtime. See Microsoft's [XML advisory](https://github.com/advisories/GHSA-r955-8q5j-3h83)
and [PKCS advisory](https://github.com/advisories/GHSA-846f-f764-76mx).

Current local verification:

- Locked backend restore and Release build passed, with zero build warnings/errors.
- All 60 unit, 4 architecture and 90 PostgreSQL integration tests passed in one complete run.
- Client lint, all 24 unit cases and the production build passed on the pinned Node 24 runtime.
- All 52 desktop/narrow authentication and learning browser journeys passed in one complete run.
- Migrations applied to a disposable browser database; EF reports no pending model changes.

Evidence is retained in `.tmp/auth-review-tests/`, `.tmp/auth-review-tests.log`,
`.tmp/auth-review-build.log`, `.tmp/auth-review-browser.log` and
`.tmp/auth-review-browser-results/`. The browser database was separate from local application
data and removed after the run. Provider handlers use fake backchannels in integration tests,
and browser provider flows use UI doubles.

After this automated run, the owner configured both registrations in the ignored root `.env`
and reported that local sign-in works on 4 November 2026. The HTTPS frontend, backend health,
PostgreSQL readiness and provider enablement were also checked locally. This is owner-reported
manual acceptance; the complete per-provider smoke matrix below was not independently recorded.

Before enabling real credentials, smoke-test each provider: first/returning sign-in, existing
account linking, cancellation, profile refresh, passwordless confirmation/agent revocation,
disconnect restrictions, and both callbacks through the configured origin. Use disposable
development accounts. No live credentials are included in this implementation.

References: [Google OpenID Connect and registration](https://developers.google.com/identity/openid-connect/openid-connect),
[Creating a GitHub OAuth App](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/creating-an-oauth-app),
[GitHub authorization](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps),
[GitHub scopes](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/scopes-for-oauth-apps),
[ASP.NET Core SameSite](https://learn.microsoft.com/en-us/aspnet/core/security/samesite?view=aspnetcore-10.0).
