# Sensei++ web client

React + TypeScript learning workbench for the Sensei++ modular-monolith API.

## What is implemented

- A responsive dashboard organized around **learn → reflect → prove**.
- Concept library with create, edit, and deactivate flows.
- Work reflection timeline with create, edit, and archive flows.
- Context-preserving evidence profile with dispute and withdrawal actions.
- Revisioned experience journal with exact-revision approval.
- API health state, Problem Details messages, loading/empty states, and optimistic-concurrency versions.
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
```
