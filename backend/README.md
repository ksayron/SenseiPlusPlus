# Sensei++ backend

This is the first backend scaffold for the modular monolith described in `../docs`.
There are no client projects in this folder.

## Shape

`Sensei.Host` is the single deployable composition root. Each business module has four projects:

- `Domain` contains entities, value rules, and lifecycle transitions.
- `Application` contains use cases, transport-neutral contracts, and repository ports.
- `Infrastructure` supplies adapters. The current adapters are in-memory placeholders.
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

## Run

The repository design targets .NET 10. This scaffold temporarily targets .NET 9 because
9.0.300 is the SDK currently installed on the development machine. The target is centralized
by the backend project files and can be upgraded when the .NET 10 SDK is installed.

```powershell
dotnet restore backend/Sensei.sln -m:1
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

Updates require an `expectedVersion`. Stale updates return `409 Conflict`. Delete operations
preserve the documented lifecycles: concepts are deactivated, work/experience entries are
archived, and evidence observations are withdrawn.

## Next infrastructure increment

Replace the in-memory repositories with module-owned EF Core mappings in one PostgreSQL unit
of work, then add migrations and PostgreSQL integration tests for ownership, foreign keys, and
optimistic concurrency. The application and domain projects should not change for that swap.
