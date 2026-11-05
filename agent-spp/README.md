# Sensei++ local agent

Windows CLI for selected Git evidence, contribution notes, protected local storage and manually confirmed Experience imports. This is a separate solution from the backend; collection works with the backend stopped.

Open `AgentSpp.sln` in your IDE. From the repository root:

```powershell
dotnet restore agent-spp/AgentSpp.sln --locked-mode
dotnet build agent-spp/AgentSpp.sln -c Release --no-restore
dotnet test agent-spp/AgentSpp.sln -c Release --no-build
dotnet run --project agent-spp/src/Sensei.LocalAgent.Cli -c Release --no-build -- help
```

| Project | Responsibility |
| --- | --- |
| `src/Sensei.LocalAgent.Cli` | Commands, inspection and explicit confirmation |
| `src/Sensei.LocalAgent.Core` | Portable Git collection, manifests, notes and local workflow |
| `src/Sensei.LocalAgent.Windows` | Windows DPAPI storage protection |
| `src/Sensei.LocalAgent.Sync` | OpenIddict browser login, protected queue and manual synchronization |
| `tests/Sensei.LocalAgent.Tests` | Capture, storage, CLI and queue verification |

The dependency-free transport contracts live in `../shared/Sensei.Client.Contracts` and are shared with the backend. The agent does not reference backend projects, database libraries or AI components. Backend import and native end-to-end tests live in `../backend/tests/Sensei.Api.IntegrationTests`.

The pinned SDK is in `global.json`; package versions and build policy are owned by this solution. Git must be on PATH. Production CLI storage requires Windows. No repositories are registered automatically, and watching never submits contributions.

See [collection and retention](../docs/20-local-evidence-agent.md) and [authentication, consent, queue and import setup](../docs/21-authenticated-evidence-import.md).
