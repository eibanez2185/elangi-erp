# Tasks: Administración de plataforma y configuración empresarial

**Input**: Design documents from `specs/001-platform-admin-config/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`,
`contracts/api.md`, `contracts/document-numbering.md`, `quickstart.md`

**Tests**: Included because acceptance scenarios and the project constitution require unit,
integration, authorization, tenant-isolation, concurrency, and end-to-end validation.

**Organization**: Tasks are grouped by the nine prioritized user stories. Implementation follows
the plan's modular .NET 10 API, React + TypeScript frontend, PostgreSQL/Supabase Auth, and
Supabase Storage boundaries. Supabase Free compatibility and local Supabase CLI/Docker execution
are constraints for this delivery.

## Format

- **[P]** marks work that can proceed in parallel after its stated prerequisites.
- **[USn]** associates a task with the corresponding numbered user story in `spec.md`.
- Each task includes its target file path; migration filenames below are planned paths, not
  generated migrations.

## Phase 1: Setup

**Purpose**: Establish the planned application, test, and local development foundations without
introducing paid-plan dependencies.

- [X] T001 Create the .NET 10 solution and planned API, module, shared, and test project skeletons in `Elangi.sln`, `src/Elangi.Api/`, `src/Modules/`, `src/Shared/`, and `tests/`.
- [X] T002 [P] Initialize the React + TypeScript application and frontend test structure in `frontend/package.json`, `frontend/src/`, and `frontend/tests/`.
- [X] T003 [P] Configure the local Supabase CLI project and documented Docker-backed services in `supabase/config.toml` and `README.md`; keep local development independent of paid Supabase features.
- [X] T004 Configure xUnit, Vitest/React Testing Library, and contract/integration test projects in `tests/Elangi.UnitTests/`, `tests/Elangi.IntegrationTests/`, `tests/Elangi.ContractTests/`, and `frontend/package.json`.

---

## Phase 2: Foundational

**Purpose**: Complete shared prerequisites that block company features: database/migration setup,
verified identity, tenant context, authorization, consistent failures, and durable audit support.

- [X] T005 Configure ordered PostgreSQL migration execution and local reset/apply workflow in `supabase/migrations/` and `supabase/config.toml`; require versioned SQL migrations and prohibit manual schema changes.
- [X] T006 Add the shared platform, security, organizations, configuration, third_parties, and audit schemas plus UUID, runtime-role, and RLS defaults in `supabase/migrations/202611070001_foundation_schemas.sql`.
- [X] T007 Implement Supabase Auth JWT validation and derive the actor only from the verified subject in `src/Elangi.Api/Authentication/SupabaseJwtSettings.cs`, `src/Elangi.Api/Authentication/SupabaseJwtBearerOptions.cs`, and `src/Elangi.Api/Authentication/CurrentActor.cs`.
- [X] T008 Implement transaction-scoped actor/company context with a non-owner, non-BYPASSRLS PostgreSQL runtime role in `src/Shared/Infrastructure/Postgres/TenantTransaction.cs` and `supabase/migrations/202611070002_runtime_rls_grants.sql`.
- [X] T009 Implement persisted company-state, Active-membership, and action-permission checks using the verified actor subject and Npgsql in `src/Modules/Security/Infrastructure/PostgresCompanyAccessReader.cs` and `src/Elangi.Api/Authorization/CompanyPermissionPolicy.cs`; register the reader and handler in `src/Elangi.Api/Program.cs`, and add the definitive company, profile, membership, role, permission, and platform-role schema with restricted RLS in `supabase/migrations/202611070004_company_authorization_core.sql`. Integration tests verify persisted authorization, inactive states, Platform Owner separation, cross-company denial, and least-privilege grants in `tests/Elangi.IntegrationTests/Security/PostgresCompanyAccessReaderTests.cs`.
- [X] T010 Implement shared problem responses for 401, 403, 404, 409, 413, 422, and 5xx cases in `src/Elangi.Api/Errors/ProblemDetailsExceptionHandler.cs` and `src/Elangi.Api/Errors/ProblemDetailsResponseWriter.cs`.
- [X] T011 Establish the transactional, append-only audit writer needed by every sensitive story and add initial RLS/tenant-boundary integration tests in `supabase/migrations/202611070003_audit_records.sql`, `src/Modules/Audit/Application/AuditWriter.cs`, `src/Modules/Audit/Infrastructure/PostgresAuditRepository.cs`, and `tests/Elangi.IntegrationTests/Security/TenantBoundaryTests.cs`.

**Checkpoint**: Story work may begin after the shared migration, authentication, authorization,
transaction, and error-handling foundations are complete.

---

## Phase 3: User Story 1 - Registrar y administrar empresas (Priority: P1)

**Goal**: Let a Platform Owner create, read, update, activate/suspend companies, manage
non-billing subscription metadata, and initiate the first administrator invitation.

**Independent Test**: Create a company, invite its initial administrator, read/update company
metadata, change status, and verify a non-Platform Owner cannot administer platform records or
read operational company data through global authority.

### Tests for User Story 1

- [ ] T012 [P] [US1] Add company-creation, status, subscription-metadata, and Platform Owner authorization API tests in `tests/Elangi.ContractTests/Platform/CompanyAdministrationContractTests.cs`.
- [ ] T013 [P] [US1] Add company validation and initial Suspended-state unit tests in `tests/Elangi.UnitTests/Platform/CompanyTests.cs`.

### Implementation for User Story 1

- [ ] T014 [US1] Extend the existing Company and authorization core with company registration/subscription fields and add only invitation storage for the first-administrator flow in `supabase/migrations/202611070005_platform_company_invitation_core.sql`; do not recreate Company, profile, membership, role, or permission tables. A new Company starts `Suspended`, Pending membership has no profile, and invitations store only a token digest.
- [ ] T015 [US1] Implement the Company domain model and validation for legal name, trade name, supported country, applicable legal identifier, address, contact details, and `Suspended` initial status in `src/Modules/Platform/Domain/Company.cs`.
- [ ] T016 [US1] Implement create, list, read, update, activate/suspend, and non-billing subscription use cases in `src/Modules/Platform/Application/CompanyAdministrationService.cs`; persist the required non-secret audit event for each successful mutation through `AuditWriter`.
- [ ] T017 [US1] Implement the Platform Owner company endpoints from `contracts/api.md` in `src/Elangi.Api/Endpoints/Platform/CompanyEndpoints.cs`; deny any implied operational-company access and never report a sensitive mutation successful if its audit write fails.
- [ ] T018 [US1] Implement the initial Company Administrator invitation endpoint using the Company Administrator initial role and actor attribution through the verified Platform Owner profile in `src/Modules/Platform/Application/InitialAdministratorInvitationService.cs` and `src/Elangi.Api/Endpoints/Platform/InitialAdministratorInvitationEndpoints.cs`; write the required audit record through `AuditWriter`.
- [ ] T019 [US1] Build the Platform Owner company list, create/edit form, status controls, subscription metadata display, and initial-administrator invitation interaction in `frontend/src/features/platform-companies/`.

**Checkpoint**: Company creation and platform metadata administration work independently, without
granting a Platform Owner operational membership.

---

## Phase 4: User Story 2 - Invitar usuarios y administrar membresías (Priority: P1)

**Goal**: Implement invitation, membership lifecycle, verified acceptance, resend/revoke/
expiration, and personal default-company selection without changing other-company memberships.

**Independent Test**: Invite and accept with the matching authenticated email and token, test
resend/expiry/revocation, transition membership status, set a personal default, and verify tenant
isolation and last-active-administrator protection.

### Tests for User Story 2

- [ ] T020 [P] [US2] Add unit tests for membership status transitions, terminal Revoked state, cleared default selection, and last-administrator invariant in `tests/Elangi.UnitTests/Security/MembershipTests.cs`.
- [ ] T021 [P] [US2] Add invitation contract/integration tests for verified-email matching, token digest comparison, suspended-company acceptance, seven-day resend, old-token invalidation, expiration materialization, and revocation in `tests/Elangi.IntegrationTests/Security/InvitationLifecycleTests.cs`.
- [ ] T022 [P] [US2] Add API tests proving `/users/me/default-company` uses the caller from the token and cannot change another user's preference in `tests/Elangi.ContractTests/Security/DefaultCompanyContractTests.cs`.

### Implementation for User Story 2

- [ ] T023 [US2] Add the remaining membership/invitation lifecycle constraints and indexes to the existing core tables: allowed status transitions, one Pending invitation per normalized company/email, and at most one active default per user in `supabase/migrations/202611070006_membership_lifecycle_constraints.sql`; do not recreate constraints already established by the authorization core.
- [ ] T024 [US2] Implement Membership and Invitation domain transitions; membership status is `Pending`, `Active`, `Suspended`, or terminal `Revoked`, and transitions include `Pending -> Active/Revoked`, `Active -> Suspended/Revoked`, and `Suspended -> Active/Revoked` in `src/Modules/Security/Domain/Membership.cs` and `src/Modules/Security/Domain/Invitation.cs`.
- [ ] T025 [US2] Implement invitation create/resend/accept/revoke/expire use cases; normalize invitee email, store only token digests, rotate the digest and reset expiry to seven days on valid resend, atomically revoke an expired pending membership, and audit successful changes in `src/Modules/Security/Application/InvitationService.cs`.
- [ ] T026 [US2] Implement invitation endpoints, accepting only the authenticated verified matching email and body `token`, comparing that token with the stored digest, and never logging or returning the original token in `src/Elangi.Api/Endpoints/Security/InvitationEndpoints.cs`.
- [ ] T027 [US2] Implement membership status changes and `PUT /users/me/default-company`; serialize last-active-Company-Administrator checks, prohibit Pending/Revoked reactivation, and audit successful mutations in `src/Modules/Security/Application/MembershipService.cs` and `src/Elangi.Api/Endpoints/Security/MembershipEndpoints.cs`.
- [ ] T028 [US2] Build invitation, membership-state, and personal default-company controls in `frontend/src/features/memberships/`.

**Checkpoint**: Invitation and membership behavior passes with a second company present; suspension
or revocation affects only the selected company's membership.

---

## Phase 5: User Story 3 - Administrar roles, permisos y límites de acceso (Priority: P1)

**Goal**: Keep platform roles separate from company roles, support the company permission catalog,
and ensure global Platform Owner assignment uses controlled bootstrap rather than company APIs.

**Independent Test**: Assign company roles and permissions, deny cross-company or out-of-catalog
assignments, verify platform-role isolation, and prove no company administrator can grant a
global role.

### Tests for User Story 3

- [ ] T029 [P] [US3] Add unit tests for permission scope and company-role assignment constraints in `tests/Elangi.UnitTests/Security/RolePermissionTests.cs`.
- [ ] T030 [P] [US3] Add API/integration tests for role CRUD, cross-company denial, least-active-administrator protection, and rejection of global platform roles through company APIs in `tests/Elangi.IntegrationTests/Security/RoleAuthorizationTests.cs`.

### Implementation for User Story 3

- [ ] T031 [US3] Populate the approved permission catalog and add any remaining US3-specific assignment constraints and RLS policies to the existing authorization tables in `supabase/migrations/202611070007_roles_permissions.sql`; do not recreate tables established by the authorization core, and preserve separation between Platform-scoped and Company-scoped permissions.
- [ ] T032 [US3] Implement role and permission domain rules so only Company-scoped catalog entries can be assigned to company roles in `src/Modules/Security/Domain/CompanyRole.cs` and `src/Modules/Security/Domain/PermissionDefinition.cs`.
- [ ] T033 [US3] Implement company role list/create/update and permission-catalog read use cases, with transactional protection of the last active Company Administrator and durable audit events for mutations, in `src/Modules/Security/Application/CompanyRoleService.cs`.
- [ ] T034 [US3] Implement `GET/POST /companies/{companyId}/roles`, `PUT /companies/{companyId}/roles/{roleId}`, and `GET /companies/{companyId}/permissions` in `src/Elangi.Api/Endpoints/Security/RoleEndpoints.cs`.
- [ ] T035 [US3] Define the controlled Platform Owner bootstrap operation outside ordinary company-administration endpoints and prevent company role APIs from assigning platform roles in `src/Modules/Platform/Application/PlatformRoleBootstrap.cs`.
- [ ] T036 [US3] Build company role and permission assignment screens from backend capabilities in `frontend/src/features/company-roles/`.

**Checkpoint**: Company role administration is tenant-limited and cannot grant global platform
authority.

---

## Phase 6: User Story 7 - Consultar auditoría administrativa (Priority: P1)

**Goal**: Persist durable, append-only audit entries for sensitive operations and expose only
company/platform audit information within the caller's authorized scope.

**Independent Test**: Perform successful administrative operations and verify actor, timestamp,
operation, company context, and non-secret summary; verify a Platform Owner can filter/page
platform and company administrative events without operational data; deny unauthorized reads and
all attempts to alter existing records.

### Tests for User Story 7

- [ ] T037 [P] [US7] Add audit unit tests proving summaries exclude credentials, secrets, and raw tokens in `tests/Elangi.UnitTests/Audit/AuditRecordTests.cs`.
- [ ] T038 [P] [US7] Add API/database tests for company-scoped reads and Platform Owner `GET /api/v1/platform/audit-records`, including every filter, date-range validation, stable ordering, page/pageSize defaults and maximum, redacted summaries, no operational/document content, append-only storage, fail-closed writes, and non-Platform-Owner denial in `tests/Elangi.IntegrationTests/Audit/AuditAuthorizationTests.cs`.

### Implementation for User Story 7

- [ ] T039 [US7] Add audit read indexes, append-only enforcement, and read-scope policies to the shared audit table in `supabase/migrations/202611070008_audit_read_protections.sql`.
- [ ] T040 [US7] Implement audit read models and paginated query operations for platform and company events, applying companyId, actorUserProfileId, operation, targetType, inclusive from/to filters, stable occurredAt/id ordering, and non-sensitive summaries without returning operational records or document contents in `src/Modules/Audit/Domain/AuditRecord.cs` and `src/Modules/Audit/Application/AuditQueryService.cs`.
- [ ] T041 [US7] Implement `GET /api/v1/platform/audit-records` exclusively for Platform Owner and `GET /api/v1/companies/{companyId}/audit-records` under company membership/permission; enforce documented query validation and response shapes without granting operational access in `src/Elangi.Api/Endpoints/Audit/AuditEndpoints.cs`.
- [ ] T042 [US7] Build company-scoped audit history and a Platform Owner global audit view with filters, pagination, and non-sensitive summaries in `frontend/src/features/audit/`.

**Checkpoint**: No sensitive mutation reports success unless its required audit record is durable.

---

## Phase 7: User Story 4 - Administrar sucursales empresariales (Priority: P2)

**Goal**: Create and list branches under a single company while preserving tenant ownership and
the approved API surface.

**Independent Test**: Create branches for two active companies, list each company's branches, and
deny a caller who selects another company's branch or an inactive company.

### Tests for User Story 4

- [ ] T043 [P] [US4] Add branch domain/API tests for company ownership, active-company authorization, and cross-tenant denial in `tests/Elangi.IntegrationTests/Organizations/BranchIsolationTests.cs`.

### Implementation for User Story 4

- [ ] T044 [US4] Add the Branch table with UUID ID and company ownership constraints in `supabase/migrations/202611070009_branches.sql`.
- [ ] T045 [US4] Implement Branch domain and list/create application operations for name, location/address, and contact details; audit successful branch mutations in `src/Modules/Organizations/Domain/Branch.cs` and `src/Modules/Organizations/Application/BranchService.cs`.
- [ ] T046 [US4] Implement only the contracted `GET` and `POST /companies/{companyId}/branches` operations with authorization and same-company validation in `src/Elangi.Api/Endpoints/Organizations/BranchEndpoints.cs`.
- [ ] T047 [US4] Build company-scoped branch listing and creation forms in `frontend/src/features/branches/`.

**Checkpoint**: Branch rows and references remain company-owned; this story adds no operational
branch workflows.

---

## Phase 8: User Story 5 - Configurar parámetros regionales y precisión (Priority: P2)

**Goal**: Read supported reference catalogs and atomically configure each company's regional
values and currency-specific precision.

**Independent Test**: Save and retrieve complete settings for two companies; reject invalid
catalog values, equal separators, incompatible currencies, and precision outside zero through
eight without partial persistence.

### Tests for User Story 5

- [ ] T048 [P] [US5] Add regional-settings validation and per-currency precision unit tests in `tests/Elangi.UnitTests/Configuration/RegionalSettingsTests.cs`.
- [ ] T049 [P] [US5] Add API/database tests for catalog reads, atomic settings writes, invalid values, precision bounds, and cross-company isolation in `tests/Elangi.IntegrationTests/Configuration/RegionalSettingsIsolationTests.cs`.

### Implementation for User Story 5

- [ ] T050 [US5] Add company regional settings and enabled-currency tables, primary-currency references, and same-company constraints in `supabase/migrations/202611070010_regional_settings.sql`.
- [ ] T051 [US5] Implement regional settings and currency models; country, locale, time-zone, and date-format values are validated against supported reference catalogs, separators must differ, precision values are integers from zero through eight, and each enabled currency's `monetary_precision` is an integer from zero through eight in `src/Modules/Configuration/Domain/RegionalSettings.cs` and `src/Modules/Configuration/Domain/CompanyCurrency.cs`.
- [ ] T052 [US5] Implement supported `GET /reference-catalogs` and atomic `GET/PUT /companies/{companyId}/regional-settings` operations; audit successful regional-setting mutations in `src/Modules/Configuration/Application/RegionalSettingsService.cs` and `src/Elangi.Api/Endpoints/Configuration/RegionalSettingsEndpoints.cs`.
- [ ] T053 [US5] Build regional configuration forms using API-provided countries, currencies, languages, cultures, and time zones in `frontend/src/features/regional-settings/`.

**Checkpoint**: Regional configuration is independently stored per company; invalid updates persist
no partial settings.

---

## Phase 9: User Story 6 - Administrar terceros (Priority: P2)

**Goal**: Manage company-owned customer/supplier records without a shared cross-company identity
or visibility.

**Independent Test**: Create customer, supplier, and dual-classified third parties in two
companies; verify each company can only read or update its own records.

### Tests for User Story 6

- [ ] T054 [P] [US6] Add third-party classification, API validation, and cross-company access tests in `tests/Elangi.IntegrationTests/ThirdParties/ThirdPartyIsolationTests.cs`.

### Implementation for User Story 6

- [ ] T055 [US6] Add the company-owned Third Party table and tenant-safe constraints in `supabase/migrations/202611070011_third_parties.sql`.
- [ ] T056 [US6] Implement Third Party model and create/read/update/list operations for name, identification/contact data, and Customer/Supplier classifications; audit successful mutations in `src/Modules/ThirdParties/Domain/ThirdParty.cs` and `src/Modules/ThirdParties/Application/ThirdPartyService.cs`.
- [ ] T057 [US6] Implement only contracted `GET/POST /companies/{companyId}/third-parties` and `GET/PUT /companies/{companyId}/third-parties/{thirdPartyId}` endpoints with non-disclosing foreign-resource behavior in `src/Elangi.Api/Endpoints/ThirdParties/ThirdPartyEndpoints.cs`.
- [ ] T058 [US6] Build tenant-scoped third-party list and create/update forms in `frontend/src/features/third-parties/`.

**Checkpoint**: The same external party may have independent records per company; no cross-tenant
identity or search is introduced.

---

## Phase 10: User Story 8 - Configurar permisos y numeración de documentos (Priority: P2)

**Goal**: Configure global document types, branch-bound company series, exact membership grants,
and atomic counters per company/document type/series, without implementing document workflows.

**Independent Test**: Configure multiple series on one branch and multiple types per series,
authorize one exact membership/type/series tuple, then validate successful numbering, concurrency,
rollback, disablement, cancellation non-reuse contract, and cross-company rejection.

### Tests for User Story 8

- [ ] T059 [P] [US8] Add contract tests for document source/series endpoints, series deactivation, and separate GET/PUT membership grants in `tests/Elangi.ContractTests/Configuration/DocumentConfigurationContractTests.cs`.
- [ ] T060 [P] [US8] Add number allocation integration tests for exact grants, same-company source/series/branch ownership, inactive series, disabled pairs, and rejection without counter consumption in `tests/Elangi.IntegrationTests/Configuration/DocumentNumberAuthorizationTests.cs`.
- [ ] T061 [P] [US8] Add parallel allocation, transaction rollback, and test-fixture cancellation cases proving unique monotonic values per tuple, independent values across tuples, no committed increments after failed finalization, and no reuse of a committed/canceled number in `tests/Elangi.IntegrationTests/Configuration/DocumentNumberConcurrencyTests.cs`; do not add a production document workflow.

### Implementation for User Story 8

- [ ] T062 [US8] Add global DocumentSource, company DocumentSeries, enabled source/series pairs, exact membership grants, and sequence tables with same-company references, a unique `(company_id, document_source_id, document_series_id)` counter scope, and `last_value` as a nonnegative integer in `supabase/migrations/202611070012_document_configuration.sql`.
- [ ] T063 [US8] Seed the approved document-type catalog (`Invoice`, `CreditNote`, `PurchaseOrder`, `DebitNote`, `CashReceipt`, `PaymentVoucher`) without defining operational document workflows in `supabase/seed.sql`.
- [ ] T064 [US8] Implement separate DocumentSource, DocumentSeries, enabled pair, and membership-grant domain rules; enforce that `DocumentSeries` belongs to exactly one same-company Branch and a Branch may have multiple series in `src/Modules/Configuration/Domain/DocumentSource.cs`, `src/Modules/Configuration/Domain/DocumentSeries.cs`, and `src/Modules/Configuration/Domain/DocumentSeriesGrant.cs`.
- [ ] T065 [US8] Implement `GET /document-sources`, `GET/POST /document-series`, and `GET/PUT/DELETE /document-series/{seriesId}`; DELETE and source/series disablement preserve history and counters without resetting them, acquire conflicting locks with allocation in the same order, and audit successful changes in `src/Modules/Configuration/Application/DocumentConfigurationService.cs` and `src/Elangi.Api/Endpoints/Configuration/DocumentConfigurationEndpoints.cs`.
- [ ] T066 [US8] Implement exact membership source/series grant read and replacement at separate GET and PUT routes; audit successful replacement in `src/Modules/Security/Application/DocumentSeriesPermissionService.cs` and `src/Elangi.Api/Endpoints/Security/DocumentSeriesPermissionEndpoints.cs`.
- [ ] T067 [US8] Implement transaction-bound `AllocateNext(companyId, documentSourceId, documentSeriesId, actor, transaction)`; before incrementing verify Active company and membership, exact grant, same-company source/series/branch, Active DocumentSeries, and enabled source/series pair, then lock the protected rows in a consistent order through commit, atomically increment/return and format the counter, and roll back allocation with failed finalization in `src/Modules/Configuration/Application/DocumentNumberAllocator.cs`.
- [ ] T068 [US8] Build document-type, series, formatting, enablement, and membership-grant configuration screens without adding issuance workflows in `frontend/src/features/document-configuration/`.

**Checkpoint**: Document numbering is an internal finalization capability only; no invoice,
purchase, receipt, payment, sales, or inventory workflow is added.

---

## Phase 11: User Story 9 - Administrar el logo de la empresa (Priority: P2)

**Goal**: Store optional company logos in a public-read Supabase Storage bucket, with backend-only
write/delete authorization, strict validation, tenant paths, auditing, and explicit cleanup.

**Independent Test**: Create a company without a logo; upload valid files up to 2 MB; reject invalid
files and unauthorized/cross-tenant attempts; replace/delete; verify public reads, correct
`LogoStoragePath`, cleanup, and durable audit records.

### Tests for User Story 9

- [ ] T069 [P] [US9] Add unit tests for MIME/signature validation, PNG/JPG/JPEG/WebP allowlist, inclusive 2 MB limit, and rejection of mismatched content in `tests/Elangi.UnitTests/Organizations/CompanyLogoValidationTests.cs`.
- [ ] T070 [P] [US9] Add Storage/API integration tests for public reads, backend-only writes/deletes, company-prefixed paths, authorization, replacement cleanup, null-on-delete, audit persistence, and surfaced partial failures in `tests/Elangi.IntegrationTests/Organizations/CompanyLogoStorageTests.cs`.

### Implementation for User Story 9

- [ ] T071 [US9] Add nullable `logo_storage_path` to Company, whose model constraint is “Optional nullable path to the company's logo object in Supabase Storage; not a URL or binary value,” and configure the public-read `company-logos` bucket with no client write/delete policy in `supabase/migrations/202611070013_company_logos.sql`; use only Supabase Free-compatible Storage capabilities.
- [ ] T072 [US9] Configure local `company-logos` bucket initialization and persistence for Supabase CLI/Docker in `supabase/config.toml` and `supabase/seed.sql`, without paid-plan services.
- [ ] T073 [US9] Implement server-side logo file validation by actual content and size, allowing only PNG, JPG/JPEG, or WebP up to 2 MB in `src/Modules/Organizations/Application/CompanyLogoValidator.cs`.
- [ ] T074 [US9] Implement the Storage adapter using server-only credentials and company-prefixed opaque object paths in `src/Modules/Organizations/Infrastructure/SupabaseCompanyLogoStorage.cs` and `src/Elangi.Api/appsettings.example.json`; never expose privileged credentials to React.
- [ ] T075 [US9] Implement authorized PUT replacement that stages a new object, atomically updates `LogoStoragePath` with its audit record, cleans staged data on failure, and removes the old object only after commit in `src/Modules/Organizations/Application/ReplaceCompanyLogo.cs`.
- [ ] T076 [US9] Implement authorized DELETE that removes the selected company's object, writes the audit record, and leaves `LogoStoragePath` null; surface storage/database partial failures in `src/Modules/Organizations/Application/DeleteCompanyLogo.cs`.
- [ ] T077 [US9] Implement `PUT/DELETE /companies/{companyId}/logo`, multipart `file` input, file-size error mapping, and same-company authorization in `src/Elangi.Api/Endpoints/Organizations/CompanyLogoEndpoints.cs`.
- [ ] T078 [US9] Build logo rendering from the derived public Storage URL and authorized upload/replace/delete controls; do not persist a fixed URL or upload directly from the browser in `frontend/src/features/company-logo/`.

**Checkpoint**: Logo objects are public for reads but mutable only through authorized backend
operations; each path and operation remains scoped to one company.

---

## Phase 12: Polish & Cross-Cutting Validation

**Purpose**: Verify the full approved scope, local setup, and security/data boundaries before
implementation delivery.

- [ ] T079 [P] Add end-to-end acceptance coverage for company administration, invitations, roles, branches, regional configuration, third parties, audit, document configuration/numbering, and logos in `frontend/tests/acceptance/platform-admin-config.spec.ts`.
- [ ] T080 Run all local Supabase CLI/Docker migrations and the `quickstart.md` acceptance scenarios; document reproducible commands and outcomes in `specs/001-platform-admin-config/quickstart.md`.
- [ ] T081 Review all endpoints, RLS policies, migration grants, audit summaries, and client code against `specs/001-platform-admin-config/data-model.md`, `contracts/api.md`, and `contracts/document-numbering.md`; resolve cross-company access, secret exposure, and out-of-scope workflows in the affected files.

## Dependencies and Execution Order

### Story completion graph

```text
Phase 1 Setup
    ↓
Phase 2 Foundational identity, migrations, authorization, RLS, and errors
    ├── US1 Platform companies and initial invitation storage
    │      ↓
    │    US2 Invitations and memberships
    │      ↓
    │    US3 Company roles and permissions
    │      ↓
    │    US7 Company and Platform Owner audit queries (audit-write foundation is shared)
    ├── US4 Branches
    │      ↓
    │    US8 Document series and numbering
    ├── US5 Regional configuration
    ├── US6 Third parties
    └── US9 Company logos (requires company authorization and audit support)

US8 additionally depends on US2 membership grants and US3 permission enforcement.
US9 additionally depends on US7's durable audit writer.
```

### Parallel execution examples

- After Phase 1, authentication, migration/RLS foundation, shared error handling, and test harness
  work can proceed concurrently where file ownership is separate.
- Once shared authorization and database foundations are ready, US1 company management can proceed
  alongside US4 branches, US5 regional settings, and US6 third parties; US2/US3 build the access
  model required by later grant-sensitive features.
- US7 audit query UI/API work can proceed in parallel with independent story frontends after the
  audit schema/writer contract is established.
- In US8, source/series authorization tests, configuration UI, and domain validation tests can
  proceed in parallel; the allocator itself waits for the tables, grants, branch model, and
  permission implementation.
- In US9, validation tests and Storage adapter tests can be authored in parallel after local
  Storage configuration is agreed; replacement and deletion use cases must wait for the storage
  adapter and audit writer.

## Implementation Strategy

1. **MVP**: Complete US1 (company registration, Platform Owner lifecycle, and first administrator
   invitation) on top of shared security/migration foundations.
2. **Access control**: Complete US2 and US3, then US7, so membership, roles, permissions, and
   durable auditing are ready before additional company configuration.
3. **Company setup**: Deliver US4, US5, and US6 as independently testable P2 increments.
4. **Shared document capability**: Deliver US8 only after branch ownership, membership grants,
   and permissions work. Do not build operational document workflows.
5. **Company identity asset**: Deliver US9 using Supabase Free-compatible public read and
   backend-authorized mutation in local and deployed configurations.
6. Run the complete `quickstart.md` acceptance suite, database/RLS tests, concurrent-number
   tests, and frontend acceptance tests before considering the feature complete.

## Open Decisions and Blockers

- No blocker remains for Platform Owner global audit supervision: the API contract now defines
  `GET /api/v1/platform/audit-records`, its filters/pagination/response, and its non-operational
  scope.
- Production scale and service-level targets remain intentionally unspecified.
- Use only Supabase Free-compatible database, Auth, and Storage capabilities for this version;
  verify the current account's file/storage quotas during deployment configuration without
  requiring paid-only features.
- Supabase Storage and PostgreSQL do not share a transaction. The logo tasks must preserve the
  prior reference on failed replacement, surface post-commit cleanup failures, and never report a
  fully successful operation when required storage cleanup or audit persistence fails.
- Migration filenames are planned execution-order paths; assign actual timestamps consistently
  when migrations are authored.
