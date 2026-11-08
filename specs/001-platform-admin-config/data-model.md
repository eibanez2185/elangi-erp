# Data Model: Administración de plataforma y configuración empresarial

## Modeling conventions

- Every entity, including association entities, has a UUID primary key; technical object and
  field names are English. Compound scopes use additional unique constraints, not non-UUID
  primary keys.
- Company-owned rows include `company_id`. Relationships between company-owned rows must
  enforce matching company ownership with composite foreign keys where practical.
- Supabase Auth owns credentials. `security.user_profiles.auth_user_id` references the Auth
  identity; no application entity stores a password.
- Platform-wide identity/role assignments are separate from company memberships and company roles.
- Schemas follow the constitution: `platform`, `security`, `organizations`, `configuration`,
  `third_parties`, and `audit`.
- The entities below describe logical model ownership. Exact SQL types, indexes, constraints,
  migration ordering, and RLS expressions are implementation details for versioned SQL migrations.

## Entities

### `platform.companies` — Company

Represents a customer organization on the platform.

| Field | Meaning / validation |
|---|---|
| `id` | UUID primary key. |
| `legal_name` | Company legal name. |
| `trade_name` | Display/commercial name. |
| `country_code` | Supported country reference. |
| `legal_identifier` | Jurisdiction-specific identifier when applicable. |
| `address` | General registered/business address. |
| `contact_details` | Company contact information. |
| `status` | `Suspended` on creation; Platform Owner may change to `Active` or `Suspended`. |
| `logo_storage_path` (`LogoStoragePath`) | Optional nullable path to the company's logo object in Supabase Storage; not a URL or binary value. |
| `created_at`, `updated_at` | Typed timestamps. |

Subscription plan/status and informative dates are held as non-billing metadata associated with
the company; payment credentials, invoices, and billing transactions are out of scope.

#### Company logo object

The optional logo object is stored in the publicly readable Supabase Storage bucket
`company-logos`, not in PostgreSQL. Its object key is scoped under the owning company ID and uses
an opaque unique file name, for example `<companyId>/<opaqueFileId>.<extension>`. Company stores
only `logo_storage_path`; the application derives a public read URL from the bucket and path.
Creating a company without a logo leaves this property null.

The API accepts PNG, JPG/JPEG, or WebP up to 2 MB and validates the actual file format and size
server-side. Public read access does not permit client upload, replacement, or deletion; those
mutations require the backend to verify the Active company, Active membership, company permission,
and that the storage path belongs to the selected company. The backend owns all storage writes.
Logo changes are audited without storing file bytes or credentials in audit records.

Replacement stages a new uniquely named object, then updates `logo_storage_path` and writes the
audit record together in the database. Only after that commit is the previous object removed. If
the upload or database/audit update fails, the old reference remains and the staged object is
cleaned up. A failure to remove the previous object after the reference changes is explicitly
surfaced; it must not silently report full success. Deletion clears `logo_storage_path` and
audits the change while removing the associated object; failures in either system are surfaced
and must not be reported as a successful completed deletion. PostgreSQL and object storage do not
share a transaction, so the application must handle and expose partial failures.

### `security.user_profiles` — User Profile

Application profile linked to one Supabase Auth identity.

| Field | Meaning / validation |
|---|---|
| `id` | UUID primary key. |
| `auth_user_id` | Unique Supabase Auth subject UUID. |
| `display_name` | Application display name. |
| `created_at`, `updated_at` | Typed timestamps. |

No password, private key, or authentication secret is stored here.

### `platform.user_platform_roles` — Global Platform Role Assignment

Associates a profile with explicitly granted platform-scoped roles, including Platform Owner.
This assignment does not imply membership in any company or access to company operational data.

### `security.memberships` — Company Membership

Associates a profile with one company and governs that relationship.

| Field | Meaning / validation |
|---|---|
| `id` | UUID primary key. |
| `company_id` | Required company scope. |
| `user_profile_id` | Null while the invitation is pending; set to the authenticated profile only when the invitation is accepted. It remains null if that invitation expires or is revoked before acceptance. |
| `status` | `Pending`, `Active`, `Suspended`, or terminal `Revoked`. |
| `is_default_company` | At most one active membership per user may be default. |
| `created_at`, `updated_at` | Typed timestamps. |

Uniqueness: at most one non-revoked membership for a given company/profile. A company with
active access must retain at least one active Company Administrator. When an active membership
becomes Suspended or Revoked, its default-company selection is cleared. Membership changes do not
change other company memberships. A Pending membership has no profile, grants no access, and is
associated with exactly one Pending invitation. If that invitation expires or is revoked before
acceptance, the membership transitions from Pending to terminal Revoked; it is not left Pending
and does not acquire a new Expired membership status. A retry creates a new invitation and a new
Pending membership. If `expires_at` has passed but expiration has not yet been materialized, the
stored invitation and membership may still read Pending; they are already ineligible for
acceptance or access and transition together to Expired/Revoked when expiration is processed.

### `security.invitations` — Invitation

| Field | Meaning / validation |
|---|---|
| `id` | UUID primary key. |
| `company_id` | Target company. |
| `membership_id` | The associated membership in the same company; Pending until acceptance, then Active or terminally Revoked. |
| `invitee_email` | Normalized destination identity. |
| `initial_role_id` | Active Company role in this company to assign on acceptance. |
| `status` | `Pending`, `Accepted`, `Revoked`, or `Expired`. |
| `token_digest` | Non-reversible invitation verifier; never expose or audit raw tokens. |
| `expires_at` | Expiration timestamp; default validity is seven days. |
| `created_by_membership_id` | Company Administrator actor; references an Active membership in this company. |
| `created_by_platform_profile_id` | Platform Owner actor; references the profile assigned that global role. |

Acceptance requires authenticated identity/email match and an unexpired, unused, non-revoked
invitation. Acceptance activates the associated membership and assigns its initial company role.
The target company may be Active or Suspended: acceptance is an invitation-specific transition
and does not require the company to be Active. It never changes company status. An Active
membership in a Suspended company grants no access to company operational functionality until the
Platform Owner activates the company.
Revocation before acceptance invalidates the invitation and terminally revokes its membership;
expiration changes the invitation to `Expired` and its associated Pending membership to terminal
`Revoked`. Both transitions occur atomically. An invitation is never accepted after expiry, even
if its status has not yet been updated by a background process; acceptance checks `expires_at`
inside its transaction.

At most one Pending invitation may exist for a `(company_id, invitee_email)` pair, where
`invitee_email` is normalized consistently before storage and comparison. Enforce this with a
unique partial constraint on `(company_id, invitee_email)` for rows whose status is Pending and a
transaction that serializes creation/resend for that company/email pair. Expiration takes effect
at `expires_at`: acceptance is denied at or after that time even if a background process has not
yet updated the stored status. An expiry worker, or any create/resend/accept operation for that
pair, materializes expiration by atomically setting the invitation to Expired and its still-Pending
membership to Revoked. Before creating an invitation, that transaction expires any elapsed
Pending invitation and revokes its associated Pending membership. If a valid Pending invitation
already exists, resend reuses its invitation and membership, rotates `token_digest`, resets
`expires_at` to seven days from the resend, and invalidates the previously issued link; it does not
create a second invitation or membership. If the existing invitation is terminal, a new attempt
creates a new invitation and membership.

Exactly one actor attribution is required per invitation: either
`created_by_membership_id` or `created_by_platform_profile_id`, never both or neither. A Company
Administrator invitation records the actor through an Active membership in the target company
and requires invitation-management permission. A Platform Owner invitation records the actor
through their profile and requires the global Platform Owner role; it does not imply a company
membership or operational access. Both paths are authorized and audited server-side.

### Global permission catalog and platform roles

`platform.permission_catalog` is the global catalog of stable permission definitions. Each entry
has a UUID, unique permission code, description, and scope (`Platform` or `Company`). The catalog
contains definitions only; it does not grant permissions. Company-scoped permissions are the
only catalog entries assignable to company roles.

`platform.platform_roles` defines platform-scoped roles, including Platform Owner, with UUID ID,
stable name/code, and active state. `platform.platform_role_permissions` associates a platform role
with a Platform-scoped catalog permission. `platform.user_platform_roles` assigns a platform role
to a user profile. Both association entities have UUID IDs, foreign keys to their endpoints, and
unique constraints on their logical pair. Platform role assignments grant only platform
capabilities, never company membership or company operational access.

### Company roles, permissions, and assignments

`security.company_roles` defines roles scoped to exactly one company, including that company's
Company Administrator and Company User roles and any authorized custom roles. Each row has a UUID,
`company_id`, stable company-local name, and active state; role names are unique within a company.
Built-in role identity is company-local and cannot be changed to a platform role.

`security.company_role_permissions` associates a company role with entries from the global
permission catalog whose scope is `Company`. It has a UUID ID, `company_id`, role and permission
foreign keys, and a unique constraint on that company/role/permission tuple.
`security.membership_roles` assigns one or more company roles to a membership; it has a UUID ID,
`company_id`, membership and role foreign keys, and a unique constraint on that
company/membership/role tuple. Composite foreign keys ensure the role and membership belong to
the same company; permission scope is constrained to Company. A membership cannot receive a role
from another company. A Company Administrator is an active membership assigned that company's
active Company Administrator role. Company administrators cannot read or modify other companies'
assignments or grant themselves privileges beyond the authorized permission catalog. The catalog
excludes out-of-scope operational module capabilities.

All changes that can add or remove a company's last active Company Administrator (including
membership status changes, administrator role assignment/removal, and role deactivation) are
serialized per company. In one transaction, the trusted database operation locks the company row
before changing assignments/status, applies the change, and verifies that at least one Active
membership remains assigned the active Company Administrator role before commit. If not, it
rejects and rolls back the operation. Runtime application roles cannot bypass this operation with
direct writes to the protected membership/role-assignment tables. This company-row lock makes
concurrent administrator-removal operations observe each other's committed result.

### `organizations.branches` — Branch

Company-owned location with UUID ID, `company_id`, name, address/location, contact details, and
typed creation/update timestamps. A branch belongs to exactly one company.

### `configuration.company_regional_settings` — Regional Configuration

One company-scoped configuration contains country, primary currency, additional enabled
currencies, language, culture, time zone, date format, decimal separator, thousands separator,
and non-currency precisions. Country, locale, time-zone, and date-format values are validated
against supported reference catalogs. Separators must differ. Precision values are integers from
zero through eight:

- one independent precision for quantities;
- one for prices;
- one for percentages;
- one for exchange rates.

`configuration.company_currencies` contains one row for each enabled currency in a company, keyed
uniquely by `(company_id, currency_code)`. Each row stores its own `monetary_precision` (integer
from zero through eight). Thus two currencies enabled by the same company may use different
monetary precisions. `company_regional_settings.primary_currency_code` references one of these
company/currency rows; additional enabled currencies are represented by the remaining rows, not
an unstructured list. Updating settings and enabled-currency rows is atomic; a currency cannot be
disabled while it is primary or referenced by data that requires it.

Saving an invalid or incompatible configuration is atomic; partial updates are not persisted.
The configuration is independent for each company.

### `third_parties.third_parties` — Third Party

Company-owned person or organization record with UUID ID, `company_id`, name, applicable
identification and contact data, and role flags/classifications `Customer` and/or `Supplier`.
The same real-world party may have independent records in different companies; no cross-company
record identity or visibility is implied.

### `platform.document_sources` — Document Source

Platform catalog of document types, including Invoice, CreditNote, PurchaseOrder, DebitNote,
CashReceipt, and PaymentVoucher. A source is a type, not a series or branch.

### `configuration.document_series` — Document Series

Company-owned emission series with UUID ID, `company_id`, required `branch_id`, name/identifier,
active state, and typed timestamps. A series belongs to exactly one branch of the same company;
a branch may have multiple series. A series is not itself a Branch.

### `configuration.company_document_series_sources` — Enabled Source/Series Pair

Enables one global Document Source for one company-owned Document Series. Key includes
`company_id`, `document_source_id`, and `document_series_id`; company ownership must match the
series and its branch. A series may enable multiple sources. Stores formatting configuration:
prefix and minimum zero-padding width for that exact company/source/series combination.
Each row has a UUID primary key and a unique constraint on the company/source/series tuple, plus
an enabled state. `DocumentSources` remain the global document-type catalog; `DocumentSeries`
remain company-owned emission series, each belonging to exactly one Branch, and a Branch may
have multiple DocumentSeries.

### `configuration.document_number_sequences` — Document Number Sequence

One row per `(company_id, document_source_id, document_series_id)`; each row has a UUID primary
key and the triple has a unique constraint referencing an enabled source/series pair.
`last_value` is a nonnegative integer.
When a source/series pair is enabled, the same database transaction creates its sequence row with
`last_value = 0` before the pair becomes available for allocation. The unique company/source/series
constraint makes concurrent enablement idempotent: the transaction that creates the row does so
once, and a concurrent enable operation reuses the existing row without resetting its value.
Re-enabling a previously disabled pair also preserves its existing counter. Allocation is
permitted only after the enabled pair and sequence row both exist. The first committed allocation
is one. Allocation increments and returns the value inside the same
transaction that finalizes the owning document. No periodic reset is allowed. Prefix and padding
are applied to the number for display; they do not change the counter scope.

The operational document entity belongs to its owning future module and is intentionally not
defined here. That module must persist the assigned numeric value and formatted number with the
document in the same transaction. A committed cancellation retains the number and does not
decrement the counter. Rejected requests and failures before the finalization transaction commits
do not consume a number. Number identity includes the full company/source/series tuple and
allocated value; formatted strings are not guaranteed unique across different tuples.

### `security.company_user_document_series` — Membership Document Issue Grant

An authorization row scoped to `company_id`, `membership_id`, `document_source_id`, and
`document_series_id`. It references an active membership and an enabled source/series pair from
the same company. Each row grants only that exact type-and-series combination. Permission for
one type in a series does not authorize another type in the same series. Each row has a UUID
primary key and a unique constraint on its membership/source/series scope.

### `audit.audit_records` — Audit Record

Append-only record of administrative and sensitive operations.

| Field | Meaning / validation |
|---|---|
| `id` | UUID primary key. |
| `company_id` | Company context when applicable; null for platform-only events. |
| `actor_user_profile_id` | Verified acting profile, where authenticated. |
| `operation` | Stable English operation identifier. |
| `occurred_at` | Typed timestamp. |
| `target_type`, `target_id` | Changed resource reference. |
| `change_summary` | Relevant non-secret change data. |

Raw passwords, tokens, private keys, and secrets are excluded. Application roles cannot alter
existing audit records. Sensitive operations do not succeed unless their audit record is durable.

## Relationships

- Company 1—many Memberships, Branches, regional settings (one current configuration), Third
  Parties, Document Series, and company subscription metadata.
- Company 1—many enabled currency rows, each with its own monetary precision.
- User Profile 1—many Memberships; one profile may have memberships in multiple companies.
- User Profile 1—many global Platform Role assignments, kept separate from membership roles.
- Global permission catalog entries are assigned to platform roles or company roles according to
  their scope; platform and company role assignments are separate.
- Company 1—many Company Roles, role-permission associations, and membership-role assignments.
- Invitation 1—1 Membership throughout its lifecycle; acceptance links it to the authenticated
  profile and activates it even if the company is Suspended, without changing company status;
  expiration/revocation terminally revokes an unaccepted membership.
- A membership may be Active while its company is Suspended; company status independently gates
  access to operational functionality.
- Company and normalized invitee email have at most one Pending invitation; elapsed invitations
  are materialized as Expired before another invitation is created.
- Branch 1—many Document Series; each Document Series belongs to exactly one same-company
  Branch.
- Global Document Source many—many company Document Series via
  `company_document_series_sources`.
- Each enabled company/source/series pair is created atomically with exactly one Document Number
  Sequence whose initial `last_value` is zero.
- Membership many—many enabled source/series pairs via `company_user_document_series`.
- All company-owned relationships must enforce same-company ownership.
- Sensitive operations 1—1 or 1—many corresponding Audit Records according to the operation;
  audit context does not grant data access.

## Authorization and tenant invariants

1. Authenticate from the Supabase Auth token; resolve the profile from its verified subject.
2. For company operational requests, verify company Active status, active membership, and the
   specific operation permission. A Suspended company denies all operational access regardless
   of membership status or assigned roles.
3. Invitation acceptance is the explicit exception to the company Active-status requirement:
   authenticate the invitee, match the verified email to a valid unexpired invitation, and
   atomically activate the associated membership and assign its initial company role. This
   exception grants no operational access while the company remains Suspended and does not
   authorize any other company-scoped operation.
4. Platform Owner grants platform administration only. Company operational access requires a
   distinct explicit active company membership.
5. Only the Platform Owner may change company status from Suspended to Active. Membership or
   invitation acceptance cannot activate a company.
6. The frontend's company, role, source, series, branch, or membership identifiers are request
   selectors, never proof of authorization.
7. Backend authorization is authoritative. PostgreSQL RLS independently constrains company rows
   using transaction-local context set only by the trusted API after verification.
8. Exposed database roles have only required grants and cannot bypass RLS; migration ownership
   and runtime permissions are separate.
9. Composite tenant constraints prevent cross-company references, including membership grants,
   series-to-branch links, and source/series links.
10. Logo upload, replacement, and deletion verify Active company membership and the specific
    company permission in the backend. A storage object key must be within the selected company's
    prefix; public read access never grants mutation rights.

## State transitions

### Company

`Suspended -> Active -> Suspended`; new companies begin Suspended. Suspending blocks company
operational access without mutating individual membership state. Only the Platform Owner may
activate or suspend a company. Invitation acceptance leaves this status unchanged. When the
Platform Owner activates a company, its Active members may access company functionality subject
to their assigned roles and permissions; suspension blocks that access again without changing
membership or role assignments.

### Membership

`Pending -> Active -> Suspended -> Active` is permitted where reinstatement is authorized.
`Pending -> Revoked` and `Active/Suspended -> Revoked` are terminal. A Pending membership exists
only while its invitation is Pending; invitation acceptance sets its profile and activates it,
even when the company is Suspended, while invitation expiration or revocation atomically revokes
it without setting a profile. Acceptance assigns the initial role but does not activate the
company or permit operational access while it is Suspended. A new invitation after
expiry/revocation creates a new membership. At least one active Company Administrator is retained
through the serialized company-scoped operation described above.

### Invitation

`Pending -> Accepted`, `Pending -> Revoked`, or `Pending -> Expired`; no transition out of a
terminal state. Acceptance checks the authenticated matching email and expiry. Expiration and
revocation atomically transition the associated still-Pending membership to Revoked. Resending a
valid pending invitation keeps its record and membership Pending, rotates its token digest, and
extends its expiry; the previous link is invalid.

### Document number

Counter starts at zero and advances by one only in a committed document-finalization transaction.
Committed document cancellation changes document status and audit history only; number allocation
is never decremented or reused. A rolled-back finalization does not consume a number.
