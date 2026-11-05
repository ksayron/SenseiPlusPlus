# Sensei++ web client

React + TypeScript learning workbench for the Sensei++ modular-monolith API.

## What is implemented

- A responsive dashboard organized around **learn → reflect → prove**.
- Coastal / Coastal Night across the application, with a persistent appearance toggle, shared semantic tokens, local fonts and responsive layouts down to 320px.
- Shared Base UI buttons, accessible form/recovery dialogs and keyboard-operated topic tabs; appearance changes preserve task input.
- Concept library with create, edit, and deactivate flows.
- Work reflection timeline with create, edit, and archive flows.
- Context-preserving evidence profile with dispute and withdrawal actions.
- Revisioned experience journal with exact-revision approval.
- API health state, Problem Details messages, loading/empty states, and optimistic-concurrency versions.
- Complete Phase 1 topic/practice/session/goal/coverage flows with all six exercise formats, durable commands, acknowledged drafts and evidence history; see the [implementation record](../docs/13-learning-implementation.md).
- Local email/password registration and sign-in, an HttpOnly session cookie, antiforgery-protected mutations, and account-isolated workspace state.
- Optional Google/GitHub sign-in, connected-profile management, and password/provider confirmation. See [provider setup](../docs/22-external-authentication.md); providers default to disabled.

See [local authentication and recovery](../docs/16-local-authentication.md). Existing development profiles receive no default password; use the documented interactive recovery command to access their preserved data. Hosted sign-up and recovery policy remain separate work.

## Run locally

Use Node 24.21.0 (pinned in the root `.node-version`) and npm 11.19.0.
CI reads the same Node pin; `package.json` records the compatible engine versions
and npm version. Install the runtime with your usual Node version manager.

The shadcn-derived component sources are maintained locally and use Base UI at runtime.
The generator CLI was removed during the toolchain upgrade because its transitive
dependencies failed npm audit. Its exact 4.21.0 stylesheet is preserved in
`src/theme/shadcn-tailwind.css` with the existing MIT license notice. When generating additional
components, separately review the current CLI and its dependencies before invoking it.

Start the backend from the repository root:

```powershell
dotnet run --project backend/src/Sensei.Host
```

Then start the client:

```powershell
cd client
npm ci
npm run dev
```

Open `http://localhost:5173`. Vite proxies `/api` and `/health` to `http://localhost:5062`.

The **Build journal** navigation link opens [`/dev-roadmap`](http://localhost:5173/dev-roadmap), a public project roadmap with checkpoint details, searchable development notes and bundled source records. This route has no authentication or owner guard and works without the backend. It uses its own field-journal visual style.

Roadmap milestones and dated logs are curated in `src/roadmap/roadmap-data.ts`; update them when work is delivered. Historical verification counts are attributed to their source records rather than presented as live CI. `?milestone=learning` (or another milestone ID) links directly to a checkpoint. Repository source records are bundled at build time, so document updates appear on the next build.

## Environment

For a separately hosted API, create `.env.local`:

```text
VITE_API_BASE_URL=https://api.example.test
```

The default empty base URL is correct for the development proxy and for a same-origin deployment.

### Development dependency pins

The 4 November 2026 publication audit updates Vite to 8.3.5 and Vitest to 5.0.3.
Overrides pin `source-map-js` 1.2.2 and Zod 4.6.7, plus Minimatch 11.0.1 specifically
under `@typescript-eslint/typescript-estree`. That parser uses the public Minimatch function;
the override retains the pinned TypeScript/ESLint stack while avoiding the affected 10.x line.
Node 24 satisfies Minimatch's runtime requirement. Keep these pins until the parent packages
request patched versions themselves; CI audits all installed dependencies without exclusions.
See the [Minimatch advisory](https://github.com/advisories/GHSA-g3hv-75wg-m249),
[source-map-js advisory](https://github.com/advisories/GHSA-68fv-2mgg-jv7q),
[Zod advisory](https://github.com/advisories/GHSA-mrhx-c7xc-m3w5) and
[Vitest advisory](https://github.com/advisories/GHSA-xp26-3454-mjgp).

For local Google/GitHub sign-in, edit the root ignored `.env` and start each service from
the repository root with `pwsh -File scripts/Start-Local.ps1 -Service Backend` and
`pwsh -File scripts/Start-Local.ps1 -Service Client` in separate terminals. The launcher
loads the file, uses HTTPS and proxies provider callbacks to the backend. Trust the development
certificate once with `dotnet dev-certs https --trust`, then open `https://localhost:5173/`.
See [provider setup](../docs/22-external-authentication.md). Credentials use server environment
keys without the `VITE_` prefix, so Vite does not expose them to the browser bundle.

## Verify

```powershell
npm run lint
npm run build
npm test
npm run test:ui
```

`test:ui` uses deterministic intercepted responses in Edge at desktop, tablet and 320px phone widths; it needs only Vite. `test:e2e` verifies learning against the local backend and PostgreSQL. Set `PLAYWRIGHT_CHANNEL=msedge` to use installed Edge for that suite. `test:demo` checks the standalone component gallery.

The common theme layer lives in `src/theme`; both the SPA and demo consume it. See [design decisions](../docs/ui/Sensei-SPA-Design-Guidelines.md) and [implementation and verified scope](../docs/ui/Sensei-SPA-Design-Implementation.md).
