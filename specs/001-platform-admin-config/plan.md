# Implementation Plan: Administración de plataforma y configuración empresarial

**Branch**: `001-platform-admin-config` | **Date**: 2026-11-02 | **Spec**:
[`spec.md`](./spec.md)

**Input**: Feature specification from
[`specs/001-platform-admin-config/spec.md`](./spec.md)

## Summary

Deliver the foundational company administration and security capabilities as a modular .NET 10
Web API and React + TypeScript application backed by Supabase PostgreSQL, Supabase Storage, and
Supabase Auth. Company membership and backend authorization define access; PostgreSQL Row Level
Security adds a transaction-scoped, least-privilege tenant boundary. Versioned SQL migrations
establish the company-scoped model. Optional company logos use company-specific paths in Storage.
Document types, branch-bound document series, per-membership issue grants, and atomic per-company/
type/series numbering form a shared capability for later operational modules, without implementing
their document workflows.

## Technical Context

**Language/Version**: C# / .NET 10 Web API; TypeScript with React.

**Primary Dependencies**: Supabase Auth for identity; Supabase Storage for company logo objects;
PostgreSQL 17 through Supabase; ASP.NET Core JWT bearer authentication; Npgsql for PostgreSQL
access in Infrastructure; React and TypeScript for the frontend. These are planned dependencies;
application manifests do not exist yet.

**Storage**: Supabase PostgreSQL 17 for structured data and Supabase Storage for company logo
objects in the public-read `company-logos` bucket. Structural database changes are ordered SQL
migrations in `supabase/migrations/`.

**Testing**: .NET unit and API/database integration tests; React component tests and browser
acceptance tests. Select xUnit, Vitest with React Testing Library, and Playwright as the proposed
test toolchain when bootstrapping application projects. Database tests must cover RLS,
authorization, cross-company access, transactional numbering and concurrent allocation, and logo
authorization, file validation, storage lifecycle, and audit behavior.

**Target Platform**: Server-hosted .NET 10 Web API, browser-based React frontend, and Supabase
PostgreSQL/Storage/Auth.

**Project Type**: Modular web application with separate frontend and backend.

**Performance Goals**: No latency, throughput, availability, or tenant-count SLA is defined.
Acceptance targets for usability and correctness are in the specification; production service
targets require a separate product decision.

**Constraints**: The backend is authoritative for business rules and authorization. Company-owned
records are isolated by company; PostgreSQL RLS is defense in depth. Runtime database access must
use a non-owner, non-`BYPASSRLS` role and transaction-local identity/company context set only from
verified authentication and membership. All schema changes are versioned migrations. Document
number allocation and document finalization must share one database transaction; canceled numbers
are retained and never reused. Company logos are optional, limited to PNG, JPG/JPEG, or WebP up to
2 MB, and stored only in the public-read `company-logos` bucket under company-specific object
paths. The Company record stores only a nullable storage path, not binary content or a fixed URL.
The backend validates and authorizes every upload, replacement, and deletion; each successful
change is audited, and replacement/deletion manages the previous object without cross-company
access.

**Scale/Scope**: Foundation for multiple companies sharing infrastructure, with explicit module
boundaries for later commercial, inventory, purchasing, sales, receivables, treasury, and
accounting capabilities. Tenant count, user count, and production volume are not yet specified.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle / constraint | Plan compliance |
|---|---|
| I. Business Rules and Data Integrity | Business rules stay in .NET/domain and database integrity boundaries; related changes use transactions. |
| II. Multi-Company Isolation and Authorization | Every company request checks active membership and operation permission; RLS is an additional database boundary. Platform Owner has no implicit operational access. |
| III. Identity, Security, and Auditability | Supabase Auth supplies identity; application profiles store no passwords; sensitive changes are audited; secrets stay server-side. |
| IV. Modular Architecture and Separation of Responsibilities | Proposed business modules and inward dependency direction separate domain, application, infrastructure, API, and presentation. |
| V. Specification, Quality, and Maintainability | Design follows the approved specification; tests cover behavior and tenant/security boundaries; scope excludes operational modules. |
| Technical and Data Constraints | PostgreSQL UUID keys, English identifiers, required schemas, Supabase permissions/RLS, and versioned SQL migrations are preserved. |

**Gate result (pre-research)**: PASS. No constitution exception or scope expansion is required.

## Project Structure

### Documentation (this feature)

```text
specs/001-platform-admin-config/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── api.md
│   └── document-numbering.md
└── tasks.md                 # Created by /speckit-tasks, not by this command
```

### Source Code (repository root)

The repository currently has no application source projects. The following is the proposed
structure for implementation; directories are not created by this planning phase.

```text
src/
├── Elangi.Api/
├── Modules/
│   ├── Platform/
│   ├── Security/
│   ├── Organizations/
│   ├── Configuration/
│   ├── ThirdParties/
│   └── Audit/
└── Shared/

frontend/
└── src/
    ├── app/
    ├── features/
    └── shared/

tests/
├── Elangi.UnitTests/
├── Elangi.IntegrationTests/
└── Elangi.ContractTests/

frontend/tests/
supabase/migrations/
```

**Structure Decision**: Use one deployable API organized by business module and responsibility,
with a separate React application. Domain rules remain independent of persistence; application
use cases coordinate work; Infrastructure owns PostgreSQL access and migrations; API owns HTTP
and verified authentication context. A module may gain a project boundary where it enforces a
real dependency constraint, but separate deployments and speculative abstractions are not
required.

## Complexity Tracking

No constitution violations. Complexity tracking is not applicable.

## Phase 0: Outline & Research

Completed technology research for modular .NET boundaries, Supabase Auth and tenant security,
React/API responsibilities, and PostgreSQL transactional numbering. Decisions and alternatives
are recorded in [`research.md`](./research.md). There are no unresolved specification
clarifications blocking design. Production scale and service-level targets were not supplied;
this plan sets no invented SLA.

## Phase 1: Design & Contracts

- [`data-model.md`](./data-model.md) defines tenant-scoped entities, relationships, invariants,
  state transitions, and number-allocation semantics.
- [`contracts/api.md`](./contracts/api.md) defines the planned user-facing API surface and
  authorization/error expectations.
- [`contracts/document-numbering.md`](./contracts/document-numbering.md) defines the internal
  transaction-bound allocation contract consumed by future document modules.
- [`quickstart.md`](./quickstart.md) defines implementation-stage validation scenarios and
  expected outcomes.

**Gate result (post-design)**: PASS. Proposed model and contracts preserve all constitution
requirements. Operational document modules remain out of scope; only shared document catalog,
series, grants, numbering, and cancellation traceability are designed here. Company logo storage
is limited to optional identity assets; it does not introduce document-management workflows.
