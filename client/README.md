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
- A development-only owner identity stored in `localStorage` and sent as `X-Owner-Id` to owner-scoped endpoints.

The owner header matches the current backend boundary; it is not authentication and must be replaced before non-local use.

## Run locally

Start the backend from the repository root:

```powershell
dotnet run --project backend/src/Sensei.Host
```

Then start the client:

```powershell
cd client
npm install
npm run dev
```

Open `http://localhost:5173`. Vite proxies `/api` and `/health` to `http://localhost:5062`.

## Environment

For a separately hosted API, create `.env.local`:

```text
VITE_API_BASE_URL=https://api.example.test
```

The default empty base URL is correct for the development proxy and for a same-origin deployment.

## Verify

```powershell
npm run lint
npm run build
npm test
npm run test:ui
```

`test:ui` uses deterministic intercepted responses in Edge at desktop, tablet and 320px phone widths; it needs only Vite. `test:e2e` verifies learning against the local backend and PostgreSQL. Set `PLAYWRIGHT_CHANNEL=msedge` to use installed Edge for that suite. `test:demo` checks the standalone component gallery.

The common theme layer lives in `src/theme`; both the SPA and demo consume it. See [design decisions](../docs/ui/Sensei-SPA-Design-Guidelines.md) and [implementation and verified scope](../docs/ui/Sensei-SPA-Design-Implementation.md).
