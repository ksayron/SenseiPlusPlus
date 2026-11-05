# Local delivery branch organization

Organized on 4 October 2026. All branches and commits are local; main and remote refs were preserved. No stash was used.

## Branches and dependency order

| Branch | Tip | Base / purpose |
| --- | --- | --- |
| `feat/9-local-authentication` | `bdbb716` | Existing independent authentication change from main; preserved |
| `feat/7-free-only-ai-gateway` | `3505792` | Existing independent AI gateway change from main; preserved |
| `feat/12-durable-assessment-jobs` | `a61a2e9` | Existing authentication + gateway + immutable attempts/assessment stack; preserved |
| `feat/19-context-snapshots` | `11a40a7` | Existing sibling context branch; preserved separately, not integrated here |
| `fix/27-private-diagnostics` | `0786666b` | Security hardening and auditing on the assessment stack |
| `feat/33-professional-event-imports` | `1b13d1f2` | Security + shared contract + scoped PKCE/bearer imports; no agent test dependency |
| `feat/34-local-evidence-agent` | `37e0a61c` | Import branch + standalone Windows capture/queue/sync and native integration tests |
| `integration/local-review-20261004` | See Git HEAD | Combined local workspace for #27/#33/#34 and this record |
| `safety/mixed-work-20261004` | `c8b6ce14` | Original unreviewed recovery snapshot, including the unrelated coursework document; never a delivery branch |

Review #27 against `a61a2e9`, #33 against `fix/27-private-diagnostics`, and #34 against `feat/33-professional-event-imports`. Review the older independent #9/#7 branches against main. The assessment/context branches contain cherry-picked dependency commits; these originals were not rebased or rewritten.

## Verification during organization

Each of the security, isolated import and combined agent branch tips passed locked backend restore, Release build and backend unit/architecture/PostgreSQL integration tests. Security and import frontend trees passed client lint/unit/build and the relevant automated browser checks; the agent branch has the identical import frontend tree, so those checks are reused. The agent branch additionally passed locked standalone restore/build and the Windows capture, DPAPI, CLI and queue suite. Tests used disposable Testcontainers PostgreSQL and the pinned local Node 24 runtime.

- `agent-Sensei.Api.IntegrationTests.trx`: 60/60 passed; 0 failed.
- `agent-Sensei.ArchitectureTests.trx`: 4/4 passed; 0 failed.
- `agent-Sensei.UnitTests.trx`: 60/60 passed; 0 failed.
- `agent.trx`: 48/48 passed; 0 failed.
- `import-Sensei.Api.IntegrationTests.trx`: 55/55 passed; 0 failed.
- `import-Sensei.ArchitectureTests.trx`: 3/3 passed; 0 failed.
- `import-Sensei.UnitTests.trx`: 60/60 passed; 0 failed.
- `security-Sensei.Api.IntegrationTests.trx`: 52/52 passed; 0 failed.
- `security-Sensei.ArchitectureTests.trx`: 3/3 passed; 0 failed.
- `security-Sensei.UnitTests.trx`: 60/60 passed; 0 failed.

Logs and TRX evidence are in `.tmp/git-organization/`; the pre-organization bundle is `.tmp/git-organization/mixed-work-20261004.bundle`. Test-generated browser screenshots are retained under `.tmp/git-organization/browser-generated/`; authored documentation screenshots remain unchanged. The only intentional source fix during separation removed duplicate Uncertainty/Assistance controls from the experience form. An intermediate import lock file was regenerated for its backend-only test graph; the combined agent tree retains its full lock file.

## Pending acceptance

These commits establish reviewable technical increments, not full issue completion. GitHub CI/PR review have not run. Live AI configuration, provider quality and rubric review remain deferred. Human system-browser launch/consent and physical-device/accessibility checks remain unverified. #27/#33/#34 retain their broader dependency and milestone gates; no issues were closed or relabeled. No mobile code was changed.

`docs/Лист_Задания_Кучерук.docx` remains an unchanged, untracked personal document in the primary workspace and is excluded from feature commits.

## Remote delivery — 4 November 2026

The owner authorized publishing the tested local application stack and closing completed
issues after reporting that configured external sign-in works. The integration delivery adds
Google/GitHub sign-in, connected profiles, literal root `.env` launch/migration support and the
return-navigation regression fixes to the existing authentication, gateway, assessment,
security, import and agent commits. The separate context-snapshot branch remains independent.

The delivery closes #9 (authenticated ownership), #11 (immutable/retry-safe attempts) and
#33 (sanitized idempotent event imports). Wider AI qualification, reviewed rubrics, security
work for future dispute/export surfaces and full reflection/IDE/native workflows remain in
their original issues. This does not claim those tracks complete.

Current local evidence is 60 backend unit, 4 architecture, 90 PostgreSQL integration, 24 client
and 52 desktop/narrow browser checks, plus a zero-warning Release build and locked restore.
See `22-external-authentication.md` for the manual acceptance boundary and exact artifacts.
