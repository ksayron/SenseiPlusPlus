# Immutable work context and separate interpretations

Local implementation of [#19](https://github.com/ksayron/SenseiPlusPlus/issues/19), 3 October 2026.

## Delivery and dependencies

`feat/19-context-snapshots` starts from main and carries the local #9 authentication and #7
gateway commits as dependencies. Their original branches remain available. Review the #19
change against its dependency commit `c0675bd`; resolve the final PR base after the dependency
PRs are accepted. No branches have been pushed and no issues are closed by this local delivery.

The Reflect collection now opens `/reflect/<episode-id>`. The workspace supports manually
selected source excerpts, disclosure review, immutable server snapshots, attributed interpretation
revisions, correction lineage, and explicit confirmation of an exact source/interpretation pair.
Private local capture works without AI. No provider route, API key or inference call is configured.

Live interpretation jobs belong to #12/#20. Full reflection acknowledgement, attempts and
feedback bundles belong to #22. Context confirmation is an owner record of what was reviewed;
it does not certify a repository, code correctness, technical achievement or an entire reflection.

## Source identity

Source kind is currently `ManualWeb`. A source contains one to eight **ordered** excerpts,
each with a label, optional source-version label and exact selected text, plus declared intent
and constraints. Labels and intent are required. Limits are 240 characters per label, 16,000
per excerpt, 4,000 for intent and 8,000 for constraints, with a combined canonical envelope
limit of 32,000 UTF-8 bytes. Oversize or malformed Unicode input fails; nothing is truncated.
These local-storage limits are independent of the smaller provider request limit and prompt overhead.

Canonicalization version `sensei.context-source.v1`:

1. Normalize CRLF and CR to LF in every source string. Preserve all other whitespace,
   comments, excerpt order, case and Unicode code points; perform no Unicode normalization.
2. Encode strings with strict UTF-8, without a BOM. Prefix every string with its byte length
   as a four-byte signed big-endian integer.
3. Write the version tag, source-kind name, four-byte excerpt count, each excerpt's label,
   version label and text in order, then declared intent and constraints.
4. Compute SHA-256 over the envelope and render 64 lowercase hexadecimal characters.

The unit suite contains a golden vector independently encoded with Node's `Buffer.writeInt32BE`
and `node:crypto`. It also checks transport line endings, Unicode, whitespace/comments,
field boundaries, source labels, constraints and excerpt order.

Owner, episode, snapshot UUID, sequential snapshot number and server receipt time are retained
separately. The fingerprint identifies supplied text, not repository truth or completeness.
Data classification and disclosure are separate metadata and do not change the source hash.
Episode title/role/summary remain mutable journal metadata. Editing them cannot silently change
an existing snapshot; selected text changes only by creating another source snapshot.

## Disclosure preview

Before saving a snapshot the client previews the **exact normalized selected fields**, classification
and recipients. It requires a separate review checkbox and save action. Source/classification edits
invalidate that preview. The preview token hashes the typed, serialized disclosure manifest;
the server recomputes it before saving so a changed source or route cannot reuse an old preview.
This binds the submitted choice; it cannot prove that a person physically read every field.

The installed policy is LocalOnly: recipient is the local Sensei++ server and external recipients
are empty. Restricted is the default classification. Choosing PublicSynthetic does not enable AI
or transmit anything. Browser drafts are not stored in localStorage or sent during typing.

The policy also supports a route supplied by trusted host code, exercised only with a synthetic
route in disposable tests. Its manifest lists both OpenRouter and the upstream provider, policy
links/check dates, exact route snapshot/digest and selected fields. A caller cannot supply a model,
endpoint, provider or policy URL; it can only request a known route revision. Restricted input,
missing routes and expired upstream privacy checks fail. This is a **preview**, not route
qualification or permission to invoke a model. Router policy review remains explicit and may be
unverified in the manifest. No such route is installed in the application.

Future inference must preview the complete prompt/template/output contract as well as selected
context, obtain explicit acceptance, and recheck price, privacy, capability and source availability
at invocation time. A saved context manifest alone cannot authorize that later request.

## Interpretations and owner confirmation

Interpretations append records containing intent, constraints, missing information, correction
notes, origin, optional provenance, superseded revision and server timestamp. The browser/API
currently authors User revisions. Fixture/Provider origins require provenance in the domain and
database; clients cannot assign them. Future trusted job integration must supply validated provenance.

Saving an interpretation never edits the source or confirms itself. A correction clears the
current accepted interpretation while keeping previous revisions and confirmations inspectable.
The user reviews the exact source fingerprint, interpretation revision and missing information in
a confirmation dialog. Its separate checkbox and action append a confirmation record. Missing
information is allowed but remains visible. Selecting a new source snapshot clears the current
interpretation selection. Historical sources remain readable; current confirmation commands
cannot confirm them as though they were current.

PostgreSQL composite foreign keys bind source, interpretation, correction and confirmation to
the same owner, episode and snapshot. Unique sequence/operation constraints prevent duplicate
versions. Database triggers reject UPDATE on the three historical record tables. Privacy deletion
is future #26 work; the immutability trigger does not prevent a deliberate dependency-ordered purge.

## API and concurrency

All routes are under `/api/v1/work/episodes/{episode}/context`, require authenticated owner
context and use the existing CSRF/expected-account protections. Foreign resources return 404.

| Route | Meaning |
| --- | --- |
| GET `/` | Parent workspace, current source/interpretation IDs and strong episode ETag |
| POST `/preview` | Read-only disclosure preview, with CSRF protection |
| GET `/snapshots` | Owner/episode-bound paginated source history |
| GET `/snapshots/{snapshot}` | Exact immutable source |
| POST `/snapshots` | Save a new source after matching disclosure review |
| GET `/snapshots/{snapshot}/interpretations` | Paginated attributed interpretation history |
| POST `/snapshots/{snapshot}/interpretations` | Append a User interpretation or correction |
| GET `/snapshots/{snapshot}/confirmations` | Paginated owner confirmation history |
| POST `/snapshots/{snapshot}/confirmation` | Explicit exact source/interpretation confirmation |

All three persisted commands require an operation UUID and the **parent episode** ETag in
If-Match, including source creation. Missing/malformed/stale headers return 428/400/412;
changed disclosure, operation-body reuse or a no-longer-current source returns 409. Updates to
episode metadata share the same database concurrency token. Commands acquire an owner/episode
transaction lock, check the current version and commit the record and episode pointer together.
No external HTTP request occurs in this transaction.

Within each command kind, owner and episode, repeated operation IDs with the same body return
the original record before checking an otherwise stale parent version. Different bodies conflict.
The response includes the current parent version/selection, so an old replay cannot make a
historical record current. The client retains operation IDs across failed saves and retries.
List cursors bind owner, episode, history kind and snapshot where applicable.

## Browser recovery and verification

The Coastal workspace preserves code whitespace and renders source/interpretation as plain text.
Unsaved source and interpretation edits remain in tab memory across route/appearance changes and
failed operations. A browser reload discards unsaved edits; account change clears the memory cache.
Stale saves retain edits and provide Reload saved state before retry. Pending actions prevent
duplicate submission. Source, interpretation, missing information, current selection and historical
confirmation have separate labels; successful storage never implies technical approval.

Verified locally: locked dependency restore; Release compilation; 59 unit tests, 39 PostgreSQL
integration tests and 3 architecture tests; client lint, 7 client tests and production build;
six context browser checks across 1440px desktop, 768px tablet and 320px phone, plus 27 existing
SPA browser regressions. The context checks cover
failed saves, retries, competing-tab changes, source/interpretation history, explicit confirmation,
plain-text script rendering, route/theme draft preservation and reflow at 200% root text size.
The new database migration was applied additively to local PostgreSQL, preserving existing rows.

The complete authenticated learning/account/context browser suite also passed all 46 checks.
Historical interpretation/confirmation replay and concurrent source-change/confirmation are covered
by the PostgreSQL suite. The generated OpenAPI document describes the new authenticated routes
and their parent-episode ETags; EF reports no pending model changes.

Screenshots are isolated under `.tmp/context-browser`. Both selected appearances and enlarged
phone content were inspected. Real mobile keyboards, full screen-reader/WCAG assessment,
live provider behavior and GitHub CI remain unverified. The live provider configuration/testing
deferral from #7 still applies.

Run the targeted browser matrix with:

```powershell
cd client
$env:PLAYWRIGHT_CHANNEL='msedge' # omit/use chromium in CI
npm run test:context -- --workers 1
```

The normal `npm run test:e2e` also discovers the context tests for its existing desktop/narrow
projects. The dedicated configuration adds the 320px/tablet matrix without changing other tests.
The learning-browser CI job runs both configurations and retains the context screenshots.
