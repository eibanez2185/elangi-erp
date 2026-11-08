# Research: Administración de plataforma y configuración empresarial

**Date**: 2026-11-02

## Architecture: .NET modular application

**Decision**: Start with one deployable .NET 10 Web API organized by business capability and
inward dependency boundaries. Keep domain rules independent of persistence, use Application for
use cases and persistence abstractions, Infrastructure for PostgreSQL, and API for HTTP/auth
boundaries. Avoid exposing domain entities as API DTOs. Split modules into separate projects only
when doing so enforces a real dependency boundary.

**Rationale**: This supports module ownership and future extension without imposing distributed
services or extra projects before there is a deployment need. Business invariants stay in the
backend and data integrity is enforced at the database boundary as well.

**Alternatives considered**:
- A flat CRUD API: less initial structure, but weakens the stated module boundaries.
- Microservices or separate deployments per module: unnecessary operational complexity for the
  foundational release.
- Apply full DDD patterns everywhere: use domain modeling where business invariants justify it;
  keep simple catalog maintenance straightforward.

**Sources**:
- [Microsoft: Common web application architectures](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures)
- [Microsoft: DDD-oriented microservice design](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/ddd-oriented-microservice)

## Identity, company authorization, and PostgreSQL RLS

**Decision**: Validate Supabase access tokens in the .NET API; derive the actor from the verified
token subject, never a request-supplied user ID. For every company operation, check the selected
company, its active state, the actor's active membership, and the required permission using
current persisted membership state. Keep global Platform Owner authority separate from company
membership. The API is the primary authorization layer.

Use a private PostgreSQL runtime role that is not a table owner and does not have
`BYPASSRLS`. The API starts a short database transaction and sets transaction-local actor and
company context only after authentication and membership verification. Tenant policies use that
trusted context as defense in depth; all tenant-owned relations also carry `company_id`, with
same-company composite foreign keys where practical. Restrict direct table access and expose only
intended Supabase API objects. Do not send elevated Supabase secret credentials to React or use
them for ordinary user requests.

**Rationale**: A valid Supabase identity is not proof of company authorization. A direct
PostgreSQL connection does not automatically carry the user's Supabase JWT claims into RLS, so
the application must establish a trusted transaction-scoped context or use a user-token-aware
Supabase request path. This plan chooses the former to support .NET-owned use cases and
transactional numbering. RLS is not treated as a replacement for backend authorization.

**Alternatives considered**:
- Rely solely on the frontend's company selection or JWT role claims: client input is untrusted,
  and mutable membership claims may be stale.
- Rely exclusively on Supabase Data API with the user's JWT: naturally supplies Supabase RLS
  claims, but cross-request work must use a database function for atomic operations and requires
  exposing/configuring API schemas deliberately.
- Use a Supabase service/secret role for every request: rejected because elevated keys bypass
  RLS and erase useful defense in depth.
- Separate database/schema per company: not required by the approved shared-infrastructure
  product; revisit only if compliance or operational requirements demand it.

**Sources**:
- [Microsoft: JWT bearer authentication in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0)
- [Microsoft: Resource-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resource-based?view=aspnetcore-10.0)
- [Supabase: JWTs](https://supabase.com/docs/guides/auth/jwts)
- [Supabase: Row Level Security](https://supabase.com/docs/guides/database/postgres/row-level-security)
- [Supabase: API keys](https://supabase.com/docs/guides/getting-started/api-keys)
- [Supabase: Securing your API](https://supabase.com/docs/guides/api/securing-your-api)
- [PostgreSQL: Row security policies](https://www.postgresql.org/docs/current/ddl-rowsecurity.html)
- [PostgreSQL: Explicit locking](https://www.postgresql.org/docs/current/explicit-locking.html#LOCKING-ROWS)

## Supabase PostgreSQL connectivity and transactions

**Decision**: Keep connection details and the PostgreSQL provider in Infrastructure; use Npgsql
as the planned .NET PostgreSQL driver. Prefer a persistent direct or session-pooled PostgreSQL
connection for a persistent .NET API; select the Supabase pooler mode based on deployment
networking. Use short transactions around database-only work. Do not hold a database transaction
while calling email or other external systems.

**Rationale**: The design needs short atomic operations for tenant changes and number allocation.
Supabase connection mode depends on the eventual hosting environment; the repository does not yet
identify that environment, so no particular endpoint is fixed in this plan.

**Alternatives considered**:
- Transaction pooling for all traffic: may be appropriate for serverless workloads, but session
  assumptions and transaction-local context must be verified with the chosen driver.
- Direct DB use in Domain or API: rejected because it couples business rules to infrastructure.

**Sources**:
- [Supabase: Connecting to Postgres](https://supabase.com/docs/guides/database/connecting-to-postgres)
- [Supabase: Connection pooling and limits](https://supabase.com/docs/guides/database/connecting-to-postgres/pooling-and-limits)
- [Microsoft: EF Core transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions)

## Company logo storage and lifecycle

**Decision**: Store an optional `LogoStoragePath` on Company and keep logo bytes in the
`company-logos` Supabase Storage bucket. The bucket is publicly readable, but all upload,
replacement, and deletion requests go through the authenticated .NET API after checking an Active
company membership and the appropriate company permission. The API alone performs storage
mutations; no storage credential is sent to the browser. Accept PNG, JPG/JPEG, and WebP files up
to 2 MB, with backend validation of both format and size. Use a company-specific prefix and an
opaque unique object name; never use a client-supplied path as authorization. Company stores
neither the binary nor a fixed public URL.

Replacement uploads the new object before committing the new path and audit record in the
database; the prior object is removed only after the new reference commits. If upload or the
database/audit update fails, retain the previous reference and remove the staged object. If
cleanup of the previous object fails after commit, keep the new reference and explicitly surface
the cleanup failure rather than claiming the entire operation completed. Deletion clears the
Company reference and records the audit event while removing the associated object; storage or
database failures must be surfaced, not silently treated as success. Storage and PostgreSQL do
not share a transaction, so the implementation must handle partial failures explicitly.

**Rationale**: A separate object bucket avoids storing binary media in PostgreSQL and public
reads allow the React client to display an identity asset without adding a logo-download
workflow. Server-owned writes preserve the approved API authorization boundary despite public
reads. Per-company keys prevent accidental cross-tenant references, and a nullable path keeps
logo setup optional.

**Alternatives considered**:
- Store the image binary in the Company row: rejected because the model stores a storage path and
  the object is media, not relational data.
- Store a fixed public URL: rejected because the URL is derived from the bucket and object path
  and should not be duplicated as entity state.
- Allow direct browser uploads to the public bucket: rejected because public read does not grant
  upload/delete authorization and the backend must validate and authorize mutations.

**Source**: Approved company-logo requirements for feature `001-platform-admin-config`.

## Transactional document number allocation

**Decision**: Store a counter row per `(CompanyId, DocumentSourceId, DocumentSeriesId)` in
`DocumentNumberSequences`. Initialize `LastValue` to zero. After all authorization and
compatibility checks pass, increment that row atomically and return the new value; store the
assigned value and formatted number on the finalized document in the same transaction. Commit
the counter and finalized document together. If the operation fails before commit, roll back the
counter increment. Once committed, a later cancellation retains the number and never decrements or
reuses it. Prefix and zero-padding are formatting settings on the company/source/series
configuration, not part of the numeric counter.

**Rationale**: PostgreSQL row updates serialize concurrent changes to the same counter row while
allowing distinct numbering scopes to proceed independently. Transaction rollback restores a
failed pre-commit increment. A committed cancellation leaves an auditable number history.

**Alternatives considered**:
- PostgreSQL `SEQUENCE`/`nextval`: efficient and unique, but sequence increments are not rolled
  back, so failures can consume values and conflict with the approved no-consumption-on-failed
  finalization rule.
- Allocate then persist the document in a separate transaction: rejected because it can create
  gaps and orphaned number assignments on failure.
- Decrement a sequence on cancellation: rejected because reuse would violate the approved rule
  and can duplicate numbers under concurrency.

**Sources**:
- [PostgreSQL: Transactions](https://www.postgresql.org/docs/current/tutorial-transactions.html)
- [PostgreSQL: UPDATE](https://www.postgresql.org/docs/current/sql-update.html)
- [PostgreSQL: Row-level locks](https://www.postgresql.org/docs/current/explicit-locking.html#LOCKING-ROWS)
- [PostgreSQL: Sequence manipulation functions](https://www.postgresql.org/docs/current/functions-sequence.html)
- [PostgreSQL: CREATE SEQUENCE](https://www.postgresql.org/docs/current/sql-createsequence.html)

## React client and API boundary

**Decision**: Organize frontend code by business feature. The React client handles presentation,
interaction state, and immediate non-authoritative input feedback; it calls the .NET API for
business decisions. UI permission checks may hide unavailable actions but never grant access.
Company changes invalidate or reload company-scoped client data. API errors distinguish
authentication, authorization, validation, conflict, and server failures.

**Rationale**: This respects the constitution's presentation-only frontend rule and prevents
duplicated business policy. Server-side checks use loaded resources and verified membership, not
just a company identifier sent by the browser.

**Alternatives considered**:
- Put authorization or business rules in client state: rejected because clients can be modified.
- A flat global component/API layer: initially simple but can couple future ERP modules.

**Sources**:
- [React: Thinking in React](https://react.dev/learn/thinking-in-react)
- [React: Sharing state between components](https://react.dev/learn/sharing-state-between-components)
- [TypeScript: Everyday types](https://www.typescriptlang.org/docs/handbook/2/everyday-types.html)
- [Microsoft: Resource-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resource-based?view=aspnetcore-10.0)
- [Microsoft: ASP.NET Core API error handling](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0)
- [Microsoft: ASP.NET Core integration tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)

## Testing strategy and repository baseline

**Decision**: Use unit tests for domain invariants, API/database integration tests for auth,
membership, RLS and migrations, contract checks for HTTP behavior, React component tests for
presentation states, and a small end-to-end suite for critical company-admin flows. Add explicit
concurrent allocation tests that verify uniqueness, scope independence, rollback behavior, and
cancellation non-reuse.

**Rationale**: Security and transaction guarantees must be tested at the actual enforcement
boundaries. A UI test alone cannot establish database isolation.

**Alternatives considered**:
- UI-only tests: do not exercise API/database isolation.
- Exhaustive browser tests for all validation permutations: slower and weaker than targeted
  unit/integration coverage.

**Repository baseline**: At planning time the repository contains Supabase CLI configuration but
no .NET solution, React application, migrations, or application test projects. The plan proposes
future locations. Proposed test tooling is xUnit for .NET, Vitest and React Testing Library for
frontend components, and Playwright for a small browser acceptance suite. These packages are
planning choices only; they are not installed or present in project manifests.

**Sources**:
- [Microsoft: ASP.NET Core integration tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
- [Supabase: RLS testing and indexing](https://supabase.com/docs/guides/database/postgres/row-level-security)
