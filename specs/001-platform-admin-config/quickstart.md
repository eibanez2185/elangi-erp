# Quickstart: Validation Guide

This guide covers the initial application foundation and the later acceptance scenarios for the
feature. Start with the local-only setup and foundation checks below; acceptance scenarios that
depend on company and document modules become runnable as those stories are implemented.

## Prerequisites

- .NET 10 SDK.
- Node.js LTS and npm.
- Supabase CLI and Docker for local Supabase services.
- Local-only Supabase Auth and test-database values; never commit secrets.
- A local `elangi_runtime` database password configured after migrations run.

## Start local services

From the repository root, start the local Supabase services and apply versioned migrations:

```powershell
npx supabase start
npx supabase db reset
```

Expected: PostgreSQL, Auth, and the local API are healthy. The local project uses the version
configured in `supabase/config.toml`. `db reset` targets the local project by default and applies
migrations in order; do not add `--remote`. Do not create schema changes manually in Supabase.

The API connects with the non-owner `elangi_runtime` role. Set its local password through the
database container's interactive `psql` prompt:

```powershell
docker exec -it supabase_db_elangi-erp psql -U postgres -d postgres
```

At the prompt run `\password elangi_runtime`; do not use the database owner role for ordinary
API access.

Dot-source the local environment loader in the PowerShell process used for tests:

```powershell
. .\supabase\scripts\load-local-environment.ps1
```

It reads local Supabase status without printing generated keys, and sets JWT validation values
and `ELANGI_TEST_DATABASE` for the integration tests. Configure
`ConnectionStrings__ApplicationDatabase` separately with the runtime-role login and a local-only
password before starting the API.

## Run automated tests

```powershell
dotnet test .\Elangi.sln
npm --prefix .\frontend test
npm --prefix .\frontend run build
```

The PostgreSQL integration tests require local Supabase to be running and
`ELANGI_TEST_DATABASE` to be set by the loader. Expected: unit, API health-contract, tenant-RLS,
and React shell tests pass.

## Acceptance scenarios

### Tenant authorization and RLS

1. Create two companies, an active membership in each, and a non-member identity.
2. As Company A's administrator, read and mutate Company A memberships, branches, configuration,
   third parties, document series, grants, logos, and audit records.
3. Attempt the same operations against Company B by changing route/body identifiers.
4. Repeat with no membership, Pending, Suspended, and Revoked memberships.
5. Attempt direct database access using the runtime role and transaction context.

Expected: only authorized Company A operations succeed; foreign and inactive-scope operations
are denied without disclosing foreign records. RLS denies cross-company rows even if an API query
omits its tenant predicate. Platform Owner does not gain operational access without a separate
active company membership. Logo operations cannot read or mutate another company's storage path.

### Company logo storage

1. Create a company without a logo and verify its `LogoStoragePath` is null.
2. As an authorized user with an Active membership, upload valid PNG, JPG/JPEG, and WebP files,
   including a file exactly 2 MB; verify each stored object is under that company's prefix and
   its path is returned by company reads.
3. Attempt a file over 2 MB, an unsupported format, and a file whose content does not match its
   claimed media type; verify rejection and that the current reference is unchanged.
4. Replace an existing logo; verify the company references the new object, the previous object
   is removed, and the change is audited.
5. Delete the logo; verify its object is removed, `LogoStoragePath` is null, and the deletion is
   audited. Verify an unauthenticated public read of a valid logo object succeeds.
6. Attempt upload, replacement, and deletion without the required permission, with an inactive
   membership, and by substituting another company's ID or storage path.

Expected: valid logos are publicly readable; only backend-authorized company operations can
write or delete them. All rejected requests leave company references and other tenants' objects
unchanged. Successful changes are audited; storage/database partial failures are surfaced and
never reported as a fully successful operation.

### Invitations, roles, and company configuration

1. Invite a user; accept with the matching authenticated identity before expiry.
2. Revoke a pending invitation, then attempt acceptance; issue a new invitation and accept it.
3. Assign the last Company Administrator an attempted self-demotion and verify rejection.
4. Set one active membership as default, then suspend it and verify default selection is cleared.
5. Save regional settings for two companies with independent per-category precision and
   per-currency monetary precision.

Expected: state transitions and authorization match `spec.md`; one company's settings and
memberships do not alter another's; invalid settings persist no partial changes.

### Transactional document numbering

1. Configure one Branch, multiple DocumentSeries on that Branch, and multiple enabled
   DocumentSources per series.
2. Grant one membership permission for one source/series pair only.
3. Issue parallel finalizations for the same tuple and for different tuples.
4. Attempt an unauthorized type in an otherwise authorized series and a cross-company source,
   series, branch, or membership combination.
5. Force a finalization rollback, then successfully finalize and cancel another document.

Expected: same-tuple concurrent allocations are unique and monotonic; other tuples have
independent counters; rejected requests and rolled-back finalizations consume no number; the
first committed value is 1; prefix and zero-padding follow tuple configuration; cancellation
retains the assigned number and never makes it reusable.

## Detailed contracts

- API surface and HTTP behavior: [`contracts/api.md`](./contracts/api.md)
- Number allocation and transaction guarantees: [`contracts/document-numbering.md`](./contracts/document-numbering.md)
- Entity constraints and state transitions: [`data-model.md`](./data-model.md)
