# Local authentication and owner isolation

Issue [#9](https://github.com/ksayron/SenseiPlusPlus/issues/9), implementation on 3 October 2026.

## Account behavior

The web workspace uses local email/password self-registration and ASP.NET Core Identity.
Optional Google/GitHub sign-in and connected profiles are documented in [external authentication](22-external-authentication.md).
Registration is available only in Development and Testing. Hosted registration, email
verification, email recovery and organization roles remain separate work. Scoped native OpenID Connect/PKCE is implemented separately in [authenticated evidence import](21-authenticated-evidence-import.md); browser cookies remain the default.
There is no SMTP or paid identity-service dependency.

Credentials live in additive `identity.login_*` tables. The login account shares the existing
profile's stable UUID; owner-scoped records retain their current foreign keys. Passwords use
Identity hashing, a minimum of 12 characters with the standard composition rules, and a
maximum of 256 characters. Five incorrect passwords lock sign-in for 15 minutes. Login,
registration and password confirmation have an IP-scoped request limit (200/minute locally,
20/minute outside Development/Testing). Passwords and request bodies are not logged.

The browser receives an eight-hour, nonpersistent HttpOnly `sensei.session` cookie with
SameSite=Lax for external-provider GET callbacks (changed on 4 October 2026). HTTPS requests receive Secure cookies; outside Development/Testing, Secure is
mandatory. Identity security stamps are checked on every request, so local credential recovery
revokes existing cookies. Cookie renewal does not extend the original session expiry.

Every module API requires authentication. `ICurrentOwner` obtains the owner from the
authenticated principal, never a supplied UUID. The shipped host has no header-authentication
handler. The old `X-Owner-Id` scheme exists only in the integration-test assembly and requires
the explicit Testing environment/configuration. Cookie authentication tests disable it.

Profiles are readable/editable only by their account. Listing profiles returns only that
profile. The old arbitrary profile-creation endpoint is unavailable to authenticated users;
registration owns account creation. The old physical DELETE cannot bypass the future
cross-module account-deletion workflow (#26).

## Browser and HTTP contract

- `GET /api/v1/auth/csrf` creates the antiforgery cookie and returns `{ token }`.
- `POST /api/v1/auth/register` accepts email, password, display name and optional language/timezone settings; it signs in the new account.
- `POST /api/v1/auth/login` signs in with email/password; failed and locked-out accounts use the same failure response.
- `GET /api/v1/auth/me` returns the current profile, without credentials.
- `POST /api/v1/auth/logout` clears the browser session.
- `POST /api/v1/auth/reauthenticate` checks the current account password and refreshes its signed authentication timestamp.
- `GET /api/v1/auth/reauthentication` requires sign-in/password/provider confirmation from the preceding five minutes. Future export/deletion endpoints must apply the `RecentAuthentication` policy.

Send `X-CSRF-Token` on every state-changing API request, including login and registration.
Fetch a new token after the sign-in identity changes. An invalid token returns 400
`antiforgery_failed`; the client preserves its form input and offers retry. Never automatically
retry a private mutation after an account change. Unauthorized requests return 401 Problem
Details rather than a login-page redirect. Existing ETag/If-Match and receipt semantics remain.

The frontend keeps only the current account UUID in memory. It sends cookies with requests,
remounts private views on account change, coordinates tabs with BroadcastChannel and checks
the session on window focus. `X-Expected-Owner` is an optional account precondition: if it differs
from the signed-in account, the server rejects the request with 409 `account_changed`. It
cannot select an owner. This also prevents a stale tab from saving account A's editor into
account B after another tab changes the cookie. Sign-out navigation passes through the
learning player's existing save/blocker flow before the confirmation screen.

The public `/dev-roadmap` route and standalone UI demo remain accessible without the backend.

## Existing development profiles and recovery

The migration preserves existing profiles, attempts, evidence and experience entries. It
creates no default passwords. A new registration cannot claim an existing profile by naming
its email. To access an existing development profile or reset a lost local password, use this
interactive command from a trusted local terminal with database access:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project backend/src/Sensei.Host --no-launch-profile --configuration Release --no-build -- --recover-local-account
```

The command asks for the existing profile UUID, displays the associated email, requires the
same UUID again as confirmation, then asks twice for a new password without echoing it. It
refuses redirected input and non-Development environments. Passwords are never command-line
arguments. It provisions credentials for the same UUID or resets an existing password,
clears sign-in lockout and revokes old cookies. It does not delete or transfer records. This
is local administrator recovery, not a remotely exposed password-reset endpoint.

Apply `LocalAuthentication` explicitly using the existing EF migration command. Do not reset
the PostgreSQL volume. Start the browser through Vite's same-origin proxy and use one hostname
consistently (`localhost` and `127.0.0.1` have different cookie scopes).

## Isolation and remaining scope

Implemented source references receive server-side access checks. A reference to another
owner's real learning attempt is rejected. Approved-experience references require an owned,
approved entry. Manual declaration/legacy attempt labels remain untrusted and never create
assessed knowledge contributions. AI-feedback source references are unavailable until that
source model exists. These checks live in a host adapter so module dependency boundaries remain.

AI jobs, account exports and orchestrated deletion do not exist yet. Their routes remain
unavailable; this delivery does not claim authorization testing of their future implementations.
Their issues must apply the current-owner and recent-authentication contracts when introduced.
There are no background jobs in the present runtime; application services continue taking
explicit owner identities, while HTTP obtains them only through `ICurrentOwner`.

Production deployment still requires the separate hosting work: HTTPS/TLS, trusted proxy and
host configuration, persistent protected data-protection keys, registration/recovery policy,
backup/deletion handling and deployment verification. This is a local account boundary.

## Verification

Local verification on 3 October 2026 used .NET SDK 10.0.401, Node 24.21.0/npm 11.19.0,
PostgreSQL in Docker and Playwright with Microsoft Edge:

- Locked NuGet restore and Release backend build passed; EF reported no pending model changes.
- 18 unit, 3 architecture and 31 PostgreSQL integration tests passed.
- Client lint, 7 transport/command tests and the production build passed.
- 38 learning and 4 authentication browser checks passed on desktop and narrow viewports.
- 27 workspace/journal UI checks and 21 standalone demo checks passed across desktop, tablet and phone.
- Desktop and narrow registration screenshots were inspected for layout. This does not establish screen-reader or formal WCAG compliance.

Tests cover real cookie registration,
sign-in/out, antiforgery rejection, password hashing/lockout, owner-header spoofing, cross-account
IDs/lists/source references/approval/session nesting, stale account preconditions, credential
recovery/revocation and the existing legacy-data migration. Browser journeys use real registered
accounts for learning and include desktop/narrow sign-in, refresh, retry and appearance checks.
They also verify that a failed session check preserves the current editor and that the public
journal remains available with the API offline. Missing, expired and future password-confirmation
timestamps fail the recent-authentication policy. These are local results; hosted deployment
and GitHub CI require their own verification.

The implementation follows Microsoft's [Identity model documentation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0)
and [antiforgery guidance](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).
