# Sensei++ GitHub delivery runbook

Use this runbook whenever Codex creates, updates, or synchronizes GitHub work for Sensei++.

## Canonical context

- Repository: `ksayron/SenseiPlusPlus`
- Repository URL: `https://github.com/ksayron/SenseiPlusPlus`
- Local workspace: `D:\BSTU\Projects\TRWP_7SEM\Sensei++`
- Delivery board: `Sensei++ Delivery Board` (project `4`)
- Delivery board URL: `https://github.com/users/ksayron/projects/4`
- Default branch after repository publication: `main`

The current workspace is nested below a parent Git checkout whose remote is the unrelated
`ksayron/LitLang` repository. Until issue #3 establishes a standalone Sensei++ checkout,
never infer the target from `git remote`. Every `gh` command must explicitly include
`-R ksayron/SenseiPlusPlus` where the command supports it.

## Environment and authentication

Clear proxy variables for every `gh` invocation:

```powershell
$env:HTTPS_PROXY=''; $env:HTTP_PROXY=''; $env:ALL_PROXY=''; gh <command>
```

Verify repository and project access before mutations:

```powershell
$env:HTTPS_PROXY=''; $env:HTTP_PROXY=''; $env:ALL_PROXY=''; gh auth status
$env:HTTPS_PROXY=''; $env:HTTP_PROXY=''; $env:ALL_PROXY=''; gh repo view ksayron/SenseiPlusPlus
$env:HTTPS_PROXY=''; $env:HTTP_PROXY=''; $env:ALL_PROXY=''; gh project list --owner ksayron
```

Project operations require the `read:project` and `project` scopes.

## Canonical labels

- Status: `status:todo`, `status:in-progress`, `status:review`, `status:done`
- Priority: `priority:p0`, `priority:p1`, `priority:p2`
- Type: `type:feature`, `type:bug`, `type:tech-debt`, `type:docs`, `type:spike`
- Scope: `scope:backend`, `scope:frontend`, `scope:infrastructure`, `scope:ai`,
  `scope:offline`, `scope:security`, `scope:mobile`, `scope:cli`, `scope:ide`,
  `scope:organization`, `scope:devops`
- Flags: `risk:high`, `needs:manual-test`, `blocked`

Every issue has exactly one status, priority, and type label, at least one scope label, and
flags only where they communicate a real risk or acceptance requirement. Do not encode status
in issue titles.

## Roadmap milestones

1. `Foundation & Feasibility (M0)`
2. `Connected Learning Loop (M1)`
3. `Offline Learning & Sync (O1)`
4. `Work Reflection & Acknowledgement (M2)`
5. `Journal & Career Reuse (M3)`
6. `Privacy, Quality & Demo Hardening (M4)`
7. `Native Android & Windows (N1)`
8. `Local Data Collection & CLI (M5)`
9. `IDE Integration (I1)`
10. `Organizations (E1)`
11. `Enterprise Expansion (E2)`
12. `Hosted Demonstration (H1)`

Assign every planned issue to the milestone that owns its primary delivery outcome. Cross-cutting
dependencies belong in the issue body rather than duplicating the issue across milestones.

## Required issue body

Use this structure:

```markdown
## Summary

<Outcome and why it matters.>

## Current state

<Verified repository state, not an assumption.>

## Deliverables

- <Concrete deliverable>

## Acceptance criteria

- <Observable completion condition>

## Dependencies

- #<issue> — [<title>](<issue URL>)

## Relevant documentation

- `docs/<source>.md`

## Explicit exclusions

<What this issue intentionally does not authorize or implement.>
```

Dependencies must use actual issue links. Keep implementation decisions consistent with the
architecture documents: individual ownership, evidence provenance, explicit approval, free-only
inference, required offline learning, and a modular monolith remain non-negotiable constraints.

## Workflow and status synchronization

1. Read the issue, dependency issues, and cited documents before starting.
2. Create a short-lived branch from `main`:
   - `feat/<issue>-<slug>` for features
   - `fix/<issue>-<slug>` for defects
   - `docs/<issue>-<slug>` for documentation
3. Set `status:in-progress`; remove other status labels; set the board item to In Progress.
4. Keep issue scope and acceptance criteria current when implementation discoveries change them.
5. Open a pull request targeting `main` with `Closes #<issue>` in its body.
6. When implementation is ready, set `status:review`; keep the board item In Progress while review
   or manual validation is pending.
7. After accepted merge/validation, set `status:done`, close the issue, and move its board item to Done.

Do not commit directly to `main`. Do not close an issue only because code exists; the stated
acceptance criteria and required manual tests must be satisfied.

## Standard commands

```powershell
# List current work
$env:HTTPS_PROXY=''; $env:HTTP_PROXY=''; $env:ALL_PROXY=''; gh issue list -R ksayron/SenseiPlusPlus --state all --limit 100

# Inspect the delivery board
$env:HTTPS_PROXY=''; $env:HTTP_PROXY=''; $env:ALL_PROXY=''; gh project item-list 4 --owner ksayron --limit 100

# Start work
$env:HTTPS_PROXY=''; $env:HTTP_PROXY=''; $env:ALL_PROXY=''; gh issue edit -R ksayron/SenseiPlusPlus <number> --add-label 'status:in-progress' --remove-label 'status:todo' --remove-label 'status:review' --remove-label 'status:done'

# Mark ready for review
$env:HTTPS_PROXY=''; $env:HTTP_PROXY=''; $env:ALL_PROXY=''; gh issue edit -R ksayron/SenseiPlusPlus <number> --add-label 'status:review' --remove-label 'status:todo' --remove-label 'status:in-progress' --remove-label 'status:done'

# Complete accepted work
$env:HTTPS_PROXY=''; $env:HTTP_PROXY=''; $env:ALL_PROXY=''; gh issue edit -R ksayron/SenseiPlusPlus <number> --add-label 'status:done' --remove-label 'status:todo' --remove-label 'status:in-progress' --remove-label 'status:review'
$env:HTTPS_PROXY=''; $env:HTTP_PROXY=''; $env:ALL_PROXY=''; gh issue close -R ksayron/SenseiPlusPlus <number> --reason completed --comment '<verification evidence and commit/PR links>'
```

## Board rules

- Every roadmap issue must appear on `Sensei++ Delivery Board`.
- Closed baseline issues are Done; open planned issues are Todo.
- The board Priority field must match the issue `priority:*` label.
- Issue milestone is the delivery track; do not create duplicate tracking issues for board grouping.
- Review uses `status:review` while the board stays In Progress until acceptance.
- `blocked` requires a comment naming the blocking issue or external condition.
- Use project number `4` and owner `ksayron` in all `gh project` commands.
