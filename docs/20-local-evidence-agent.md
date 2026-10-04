# Local evidence agent

Local foundation of [issue #34](https://github.com/ksayron/SenseiPlusPlus/issues/34), implemented on 3 October 2026. The requirements come from that issue, [the integration/privacy contract](04-flows-and-integrations.md), and [the security/storage direction](05-security-and-privacy.md). User-selected scope adds opt-in watching to the original command-triggered CLI direction.

The agent runs separately from Sensei++ and works with the backend stopped. Collection and file export make no network or database calls. Browser sign-in and manually confirmed import are implemented separately in [the authenticated import guide](21-authenticated-evidence-import.md). AI, reflection sessions and IDE integration remain future work; #34 is partially delivered.

## Build and launch

Requires Windows, the repository's .NET 10 SDK and Git on PATH. Core collection/review logic is portable; the installed storage adapter is Windows-only. Run from the project root. The agent has its own solution under `agent-spp`, separate from `backend`:

```powershell
dotnet restore agent-spp/AgentSpp.sln --locked-mode
dotnet build agent-spp/AgentSpp.sln --configuration Release --no-restore
function agent { dotnet '.\agent-spp\src\Sensei.LocalAgent.Cli\bin\Release\net10.0\sensei-agent.dll' @args }
agent help
```

The default store is `%LOCALAPPDATA%\Sensei++\LocalAgent`, outside repositories. `--state-dir DIR` selects a separate test/device store and still uses DPAPI; directories within a Git repository are rejected. No repository is registered automatically. Starting the web application does not start the watcher.

## Register, watch and inspect

Register a repository with explicit relative files/directories, a work setting and a source choice. `metadata` is the normal default policy; `diffs` explicitly consents to retaining selected source locally. The command requires the choice so opt-in is reviewable.

```powershell
agent repo add --root 'D:\MyProjects\Example' --path src --path tests --setting PersonalProject --source metadata
agent repo list
agent watch
```

`watch` polls every five seconds until Ctrl+C. Working changes must match on two consecutive polls and pass a second capture check before saving. Its cadence is a five-second delay after collection; slow Git commands can make the effective interval longer. Polling survives missed filesystem events without depending on `FileSystemWatcher`.

Registration records the current HEAD and does not backfill history. New commits reachable since the checkpoint are reconciled on startup and each poll, separately from staged and unstaged changes. These may include commits brought in through branch changes; capture does not prove who authored a change. Merge candidates use the first parent and are labeled. New repositories without commits are supported.

Stop the watcher with Ctrl+C. It is not installed as a service or login task. `agent status` reports watcher-lock ownership and registration/candidate counts; it is not a process-health or repository-correctness verdict. Only one watcher can own a store.

```powershell
agent capture REPO_ID --kind Staged
agent capture REPO_ID --kind Unstaged
agent capture REPO_ID --kind Commit --commit FULL_OBJECT_ID
agent inbox list
agent inbox show CANDIDATE_ID
agent inbox show CANDIDATE_ID --source
```

IDs in these examples are the actual UUIDs returned by commands. `inbox show` lists the selection, source kind, modes/identities, capture time, completeness, source availability, freshness and reflection prompts. Paths and user text are escaped as JSON. Raw source is displayed only with `--source`, and only when retained. Metadata-only candidates cannot recover past source after it changes.

Staged, unstaged and commit manifests are distinct. SHA-256 canonicalization version 1 includes the source kind, selected paths, base identity, commit identity when present, file identities, modes, deletion/rename metadata and unreviewed markers. Index changes and HEAD changes invalidate affected working currency. Repeated manifests reuse stable candidate/event IDs; dismissal and deletion do not recreate the same candidate. Historical commit records retain their exact identity rather than claiming current repository approval.

## Boundaries and unsupported content

Only selected paths are read. Git commands use argument lists, literal pathspecs where supported, NUL-delimited filename output, bounded output/timeouts, and disabled external diff/text-conversion/filter/fsmonitor helpers. Ignore rules and credential-file exclusions are applied before reading source bodies. No automatic Git writes, hooks, commits, pushes or broad source upload occur.

Untracked files, binaries, submodules, symlink/reparse-point paths, unmerged entries, unsupported encoding and oversized content are listed as unreviewed. The capture reads at most 100 changed files and retains at most 2 MiB of UTF-8 patch text. Remaining paths stay listed with a file-limit marker. Limits and unsupported content make the candidate incomplete; they are never silently labeled complete. Unreviewed markers do not fingerprint the full contents of unsupported files and cannot establish their currency or correctness.

Git subprocess output is capped at 8 MiB (64 KiB for stderr) and each subprocess times out after 20 seconds; configuration reads add a separately bounded subprocess. Errors expose codes rather than source bodies, commands or provider payloads. Selected changes can still be too large to enumerate within the output bound; such captures fail visibly rather than producing misleading complete evidence.

At most 100 new commits are reconciled per pending range. A larger backlog or missing checkpoint object reports an error and does not advance the checkpoint. Review the gap and deliberately choose a new baseline if historical backfill is unnecessary:

```powershell
agent repo configure REPO_ID --reset-baseline
```

This skips the uncollected history and retains existing candidates. Source or path settings can be changed explicitly:

```powershell
agent repo configure REPO_ID --source diffs --path src
agent repo remove REPO_ID
```

Source retention applies to newly captured candidates; enabling it does not reconstruct source for an existing metadata-only, purged or expired candidate. Removing a registration stops future collection and leaves stored candidates intact. Working candidates from an unregistered/unavailable repository cannot be exported until recaptured through an available registration.

## Notes and reviewed export

Capture supplies narrow change facts and reflection prompts. Users supply role, actions, alternatives, outcome, uncertainty, assistance and optional concept UUIDs. The agent does not infer knowledge, contribution or achieved impact.

```powershell
agent inbox edit CANDIDATE_ID --revision 1
```

Interactive editing asks for contribution fields. Empty input keeps an existing field; `-` clears it. Optional concept IDs can be provided in a strict JSON notes file:

```json
{
  "role": "Developer",
  "actions": "Investigated duplicate delivery and compared retry strategies",
  "alternatives": "Immediate retry or delayed retry",
  "outcome": "Not measured",
  "uncertainty": "Production behavior has not been observed",
  "assistance": "Peer review",
  "conceptIds": []
}
```

```powershell
agent inbox edit CANDIDATE_ID --revision 1 --notes '.\my-notes.json'
agent export CANDIDATE_ID --preview
agent export CANDIDATE_ID --confirm EXACT_PREVIEW_TOKEN --output '.\sanitized-draft.json'
```

Alternatively omit `--confirm` while providing `--output`; the CLI displays the draft and asks for its exact confirmation token. Preview alone writes no export. The token binds the exact candidate revision, fingerprint and draft. A changed revision, changed/unavailable working context or recognized credential blocks export. Existing output files are never overwritten. Export files and user-created notes files are deliberately plaintext artifacts, outside the protected-store policy.

`SanitizedEventDraftV1` exports only: stable event UUID, schema version 1, work setting, UTC calendar date of capture, reviewed contribution fields, assistance, optional concept IDs and `claimProvenance: client_reported`. Repository URLs, paths, commits/messages, manifests and source bodies have no export fields. This is a local draft contract, distinct from authenticated `ProfessionalEventImportV1`; the import endpoint does not accept this legacy file-export contract.

Recognized credential detection is a guard, not proof that arbitrary prose is sanitized. Review the displayed prose before confirmation. Unknown outcomes should remain unknown. Incomplete working candidates cannot establish source currency and cannot be exported; narrow the selection to reviewed supported files and recapture. Historical commit candidates retain visible completeness limitations, and their exported notes remain user claims. Export cannot create trusted assessments, experience approval or learning scores.

## Protection, expiry and recovery

Persisted registrations, paths, manifests, notes, deletion tombstones and optional diffs use Windows DPAPI with CurrentUser scope and UI forbidden. The adapter calls the Windows API directly without adding a package or storing a separate encryption key. Another Windows user or a copied store on another device cannot normally decrypt it. This is not protection from the same signed-in user, an administrator or a compromised operating system.

Atomic file replacement and a store lock prevent competing CLI writes. CLI mutations report `state_busy` when another command owns the store; the watcher retries transient contention on its next poll. Commit checkpoints advance only after candidates are durably saved. A crash between candidate save and checkpoint save replays and deduplicates the candidate. Orphan encrypted temporary files from interrupted writes are removed under the store lock. Unreadable protected state is reported and never silently replaced; keep it for recovery or explicitly select a fresh store.

Retained source expires 30 days after capture. Cleanup runs on every CLI startup and during watching; an agent that is stopped purges on its next start. Metadata and contribution notes remain. Turning retention off prevents new retained diffs; existing ones are removed through expiry or explicit purge.

```powershell
agent source purge CANDIDATE_ID --revision CURRENT_REVISION
agent source purge-expired
agent inbox dismiss CANDIDATE_ID --revision CURRENT_REVISION
agent inbox delete CANDIDATE_ID --revision CURRENT_REVISION
```

Mutations require the displayed revision to prevent lost edits. Delete retains an encrypted ID tombstone for deduplication and removes the candidate, notes and source. Purge preserves notes and metadata; user-written notes may repeat sensitive facts and need separate review/deletion. Deletion is logical file removal, not a promise of forensic erasure or removal of user-created exports/backups.

## Verification

The agent test project covers disposable Git repositories, ownership-independent provenance, selected paths, staged/unstaged transitions, commit/merge history, branch changes/rewrites, linked worktrees, unusual filenames, disabled helpers, ignored/credential paths, unsupported content and capture limits. Workflow checks cover deduplication, failed writes/checkpoints, restart, locks, revision conflicts, expiry, protected files, stale export and exact confirmation.

Windows tests exercise actual DPAPI round trips/tamper rejection and the installed CLI process: register → watch → abrupt stop → restart → annotate → preview → confirmed export, with no backend. Portable test hosts use a test-only encryption adapter; they do not validate Windows protection. A dedicated Windows CI workflow runs the real Windows checks. GitHub CI remains unverified until authorized delivery.

Local validation on 3 October 2026 passed locked solution restore, the full Release solution build with zero warnings/errors, 56 existing backend unit tests and four architecture checks. All 35 distinct agent cases passed: the full 34-case suite before the final watcher-contention adjustment, followed by all four CLI cases (including its new contention/cancellation test) on the final implementation. The generated OpenAPI contract has no semantic changes. Reports are under `.tmp/agent-validation/`; the latest agent report contains the four focused final CLI cases. PostgreSQL integration and browser checks were not rerun for this independent CLI addition.
