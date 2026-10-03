# .NET 10 and Node 24 toolchain upgrade

Issue: [#3 — Publish repository baseline, adopt .NET 10, and establish CI](https://github.com/ksayron/SenseiPlusPlus/issues/3).

## Baseline

The repository is a standalone Sensei++ checkout. The SDK installation that previously
prevented .NET 10 adoption is available as of 3 October 2026.

| Component | Repository pin |
| --- | --- |
| .NET SDK | 10.0.401, `backend/global.json`, patch roll-forward |
| Backend and test target | `net10.0` |
| ASP.NET Core / EF Core packages and EF CLI | 10.0.12 |
| Npgsql EF provider | 10.0.3 |
| PostgreSQL | 18, existing local Compose service |
| Node | 24.21.0, root `.node-version` |
| npm | 11.19.0, `client/package.json` |

CI installs the SDK and Node versions from these repository files. NuGet dependencies
use committed lockfiles and `--locked-mode`; the client uses `npm ci`.

## Compatibility

The host's OpenAPI transformers use the updated Microsoft.OpenApi namespace,
schema types, interfaces and schema-reference objects. The document explicitly remains
OpenAPI 3.0, preserving the existing contract format while regenerating its schemas with
the new toolchain. The migration follows the [ASP.NET Core upgrade guide](https://learn.microsoft.com/en-us/aspnet/core/migration/90-to-100?view=aspnetcore-10.0)
and the [Npgsql EF 10 release notes](https://www.npgsql.org/efcore/release-notes/10.0.html).

.NET 10 audits transitive NuGet packages by default. The old Testcontainers dependency
resolved SSH.NET 2024.2.0 and failed restore on
[GHSA-mggc-4xg6-vcxf](https://github.com/advisories/GHSA-mggc-4xg6-vcxf) and
[GHSA-q939-rpr3-3284](https://github.com/advisories/GHSA-q939-rpr3-3284).
The upgraded test harness uses patched SSH.NET 2026.0.0; warnings remain errors.

The clean npm install also found an advisory chain through the unused shadcn generator
CLI (`braces` → `micromatch` → `fast-glob`). The CLI is removed from build dependencies;
its exact 4.21.0 stylesheet is preserved locally with its MIT license. The maintained
shadcn-derived components, license notices, registry configuration
and Base UI runtime remain in use.

Historical EF migration files retain their original generator versions. Database
compatibility is checked against PostgreSQL, including the existing legacy-data upgrade
test. Apply migrations explicitly; do not reset the local database volume.

## Delivery

The [GitHub runbook](../github.md#main-branch-protection) records verified main-branch
protection and required checks. Issue #3 is ready to close only after its pull request
passes all four CI jobs and is accepted. Issue #7 has a dependency on #3, but its
AI gateway and reviewed provider evaluation remain separate deliverables.

The Phase 1 results in [the learning implementation record](13-learning-implementation.md)
remain dated results from .NET 9. Current upgrade verification is recorded below.

| Local check on 3 October 2026 | Result |
| --- | --- |
| Locked NuGet restore and Release solution build | Passed; zero build warnings or errors |
| Unit / architecture / PostgreSQL integration | 18 / 3 / 25 passed |
| EF model and existing local PostgreSQL database | No pending model changes; all migrations already applied |
| OpenAPI 3.0 regeneration | Stable; 62 operations, required parameters and ETags, and 332 references checked |
| Node 24 clean install, npm audit, lint, client tests and build | Passed; zero audit findings, 7 client tests |
| Browser UI / gallery / real-backend learning journeys in Edge | 27 / 21 / 36 passed |

CI results are tracked on the issue #3 pull request; this local record alone is not a
clean Ubuntu checkout or a substitute for branch protection checks.
