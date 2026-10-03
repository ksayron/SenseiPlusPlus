# Sensei++ backend

This is the first backend scaffold for the modular monolith described in `../docs`.
There are no client projects in this folder.

## Shape

`Sensei.Host` is the single deployable composition root. Each business module has four projects:

- `Domain` contains entities, value rules, and lifecycle transitions.
- `Application` contains use cases, transport-neutral contracts, and repository ports.
- `Infrastructure` supplies EF Core repositories and module-owned PostgreSQL mappings.
- `Api` owns the module's `/api/v1` HTTP endpoints.

The dependency direction is:

```text
Host -> Api -> Application -> Domain
  |                  ^
  +-> Infrastructure+
```

The initial modules are:

- Identity and privacy (`UserAccount`)
- Learning and content (`Concept`)
- Work reflection (`WorkEpisode`)
- Evidence and profile (`EvidenceObservation`)
- Experience and presentation (`ExperienceEntry` with immutable revisions)

## PostgreSQL and migrations

Copy `.env.example` to an ignored `.env` only when you need to override the safe local
defaults, then start PostgreSQL 18:

```powershell
docker compose up -d
docker compose ps
```

Restore the repository-pinned EF tool and apply migrations explicitly. The host never applies
migrations automatically, so production startup cannot silently alter a database:

```powershell
dotnet tool restore
$env:ConnectionStrings__Sensei = 'Host=localhost;Port=5432;Database=sensei;Username=sensei;Password=sensei_local_dev'
dotnet tool run dotnet-ef database update --project backend/src/Sensei.Host --startup-project backend/src/Sensei.Host
```

Useful local database commands:

```powershell
docker compose exec postgres psql -U sensei -d sensei
docker compose logs -f postgres
docker compose down
docker compose down -v # destructive: removes the local database volume
```

PostgreSQL uses one database with module schemas: `identity`, `learning`,
`work_reflection`, `evidence`, and `experience`. In Development only, the host creates the
configured default owner if it is missing. Other environments receive no demo data.

## Build and run

The backend and its tests target .NET 10. `global.json` pins SDK 10.0.401
(with patch roll-forward); CI reads the same file. Central package management pins
ASP.NET Core and EF Core 10.0.12 with the Npgsql EF provider 10.0.3.
The repository tool manifest pins `dotnet-ef` 10.0.12.

```powershell
dotnet restore backend/Sensei.sln --locked-mode -m:1
dotnet build backend/Sensei.sln --no-restore -m:1
dotnet run --project backend/src/Sensei.Host
```

The single-node restore/build switch works around a parallel MSBuild issue observed with the
installed SDK on this machine; it does not change the application architecture.

The API exposes `/health` plus these initial CRUD roots:

- `/api/v1/identity/users`
- `/api/v1/learning/concepts`
- `/api/v1/work/episodes`
- `/api/v1/evidence/observations`
- `/api/v1/experience/entries`

Owner-scoped endpoints currently require an `X-Owner-Id` header. This is only a development
boundary used to exercise owner isolation; it is not authentication. A real authenticated
owner context must replace it before any non-local use.

Updates require a strong `If-Match` header using the resource's ETag. Resources also expose
an opaque `versionToken`. Missing preconditions return `428`, malformed tokens `400`, and
stale versions `412 Precondition Failed`; `409` remains for domain/resource conflicts.
Lists use `{ items, nextCursor }`, and errors use Problem Details with `code` and `traceId`.
Numeric `expectedVersion` values remain internal application/domain details, not request-body fields.
Delete operations
preserve the documented lifecycles: concepts are deactivated, work/experience entries are
archived, and evidence observations are withdrawn.

`/health` includes PostgreSQL readiness. The root endpoint reports `postgresql` as the active
persistence provider.

The planned learning expansion is documented in [the learning system contract](../docs/09-learning-system.md).
Sessions, exercises and scored knowledge projections described there are not implemented by that documentation change.
