# Elangi ERP Constitution

## Core Principles

### I. Business Rules and Data Integrity
Business rules and validation MUST be enforced by the .NET backend, not solely by the React
client. Operations that change related records MUST use database transactions where needed to
preserve consistency. Changes MUST preserve established business data or document and validate
its transformation. These requirements keep the backend authoritative and prevent partial or
client-bypassed operations.

### II. Multi-Company Isolation and Authorization
Company-owned data MUST be isolated by company. A person MAY belong to multiple companies, but
every backend request MUST verify the active company membership and required permissions before
accessing or changing that company's data. PostgreSQL Row Level Security MUST provide a
complementary enforcement layer for exposed data. Company administrators MUST be limited to their
own company. Platform Owner authority MUST NOT by itself grant unrestricted access to company
operational data. Tests MUST verify that one company cannot read or modify another company's data.

### III. Identity, Security, and Auditability
Supabase Auth MUST manage user credentials and authentication. The application user profile MUST
link to the Supabase Auth identity; application tables MUST NOT store passwords. Authorization
MUST use roles and permissions, and privileged credentials or private keys MUST NOT be exposed
to the React client or committed to source control. Administrative and other sensitive operations
MUST be auditable. These controls protect identity and make sensitive actions traceable.

### IV. Modular Architecture and Separation of Responsibilities
The backend MUST own business logic; the React and TypeScript frontend MUST handle presentation
and user interaction. The system MUST separate domain, application, infrastructure, and
presentation responsibilities, use dependency injection, and keep module dependencies explicit.
Designs MUST apply SOLID principles where they improve separation and maintainability, without
adding unnecessary abstractions. This keeps the commercial, multi-company product extensible
without coupling business rules to the user interface or infrastructure.

### V. Specification, Quality, and Maintainability
Requirements and acceptance criteria MUST be agreed before implementation. Material technical
plans MUST be reviewed before coding, and implementation MUST be divided into small, verifiable
tasks. Changes MUST include relevant unit and integration tests; authorization and data-isolation
changes MUST be tested at their enforcement boundaries. Contributors MUST NOT invent requirements
or change approved architectural decisions without documenting the change. Documentation MUST be
updated when affected behavior or architecture changes. These practices prioritize correctness
and data integrity over implementation speed.

## Technical and Data Constraints

The application stack is .NET 10 Web API, React with TypeScript, PostgreSQL through Supabase, and
Supabase Auth. Development uses Visual Studio Code, Git and GitHub, GitHub Copilot, and GitHub
Spec Kit. Stack changes MUST document their rationale and impact.

PostgreSQL primary keys MUST use UUIDs. Technical identifiers and schema object names MUST use
English and consistent naming. Relational data MUST use appropriate foreign keys, constraints,
indexes, and integrity rules. Dates and times MUST use appropriate database types, never strings.
The data model MUST support multiple currencies, countries, languages, time zones, and regional
settings.

Database responsibilities MUST be organized using the `platform`, `security`, `organizations`,
`configuration`, `third_parties`, and `audit` schemas. Schema design and API exposure MUST respect
Supabase permissions and Row Level Security. Every structural database change MUST be delivered
as a versioned SQL migration tracked in Git. Manual Supabase changes MUST NOT be used to deploy
schema structure.

## Delivery and Review

Reviews MUST verify the approved requirements, backend ownership of business rules, company
isolation, authorization, data integrity, relevant tests, and migration impact. Unit and integration
tests MUST cover affected behavior, with particular attention to preventing cross-company access
and bypass of authorization checks. Major architectural changes MUST include a documented
justification. Contributors MUST run applicable automated checks before delivery and report any
checks that could not be completed. Any exception to this constitution MUST document its reason,
scope, and corrective follow-up.

## Governance

This constitution governs project changes and takes precedence over conflicting local practices.
Amendments MUST document their rationale and impact, be reviewed with affected stakeholders, and
be recorded in this file. Every amendment MUST update the version and last-amended date.

Versioning follows semantic versioning: MAJOR for backward-incompatible changes to principles or
governance, MINOR for new principles or materially expanded requirements, and PATCH for
clarifications and non-semantic edits. Reviews MUST assess compliance with the principles relevant
to each change; any exception MUST be explicit and include corrective follow-up.

Change history:
- 1.0.0 (2026-11-02): Initial constitution.
- 1.1.0 (2026-11-02): Expanded principles and technical constraints for ELANGI ERP architecture,
  multi-company isolation, identity, security, and specification-led development.

**Version**: 1.1.0 | **Ratified**: 2026-11-02 | **Last Amended**: 2026-11-02
