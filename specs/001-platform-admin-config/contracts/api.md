# API Contract: Platform Administration and Company Configuration

## Contract scope

This contract describes the planned .NET Web API surface for the foundational feature. It is not
an implementation or generated OpenAPI document. The React client calls this API; it must not
access privileged database credentials or make authorization decisions authoritative.

All identifiers are UUIDs. Technical property names are English. The service derives the actor
from a validated Supabase Auth access token. A `companyId` in a route selects the requested
context; it never grants access to that context.

## Common behavior

- Base path: `/api/v1`.
- Authentication: `Authorization: Bearer <Supabase access token>` for authenticated operations.
- Platform-only operations require the global Platform Owner grant.
- Company-scoped operations require an Active company, Active membership, and action-specific
  permission verified by the backend on every request, except invitation acceptance as defined
  below.
- Company-scoped resources and references must belong to the same company.
- Responses never disclose whether inaccessible resources exist in another company.
- Mutation responses include the resulting resource representation or an operation identifier.
- Errors use a consistent problem response with a stable code and user-safe message:
  - `401` missing/invalid/expired authentication.
  - `403` authenticated actor lacks the required scope or permission.
  - `404` resource missing or not visible in caller scope.
  - `409` state conflict, duplicate assignment, active-admin invariant, or stale update.
  - `413` uploaded file exceeds the endpoint's size limit.
  - `422` invalid or incompatible fields/references.
  - `5xx` unexpected failure; no successful shape is returned when persistence/audit fails.
- Sensitive changes generate audit records. Error responses and audit summaries never contain
  access tokens, invitation secrets, passwords, or private credentials.

## Platform administration

| Method | Path | Authorization | Purpose |
|---|---|---|---|
| `POST` | `/platform/companies` | Platform Owner | Create a Suspended company and initial non-billing subscription metadata. |
| `GET` | `/platform/companies` | Platform Owner | List companies and platform-visible status/metadata. |
| `GET` | `/platform/companies/{companyId}` | Platform Owner | Read company administration metadata, not operational company data. |
| `PATCH` | `/platform/companies/{companyId}` | Platform Owner | Update company identity/contact and non-billing subscription metadata. |
| `PUT` | `/platform/companies/{companyId}/status` | Platform Owner | Activate or suspend a company. |
| `POST` | `/platform/companies/{companyId}/initial-administrator-invitations` | Platform Owner | Invite the first Company Administrator. |
| `GET` | `/platform/audit-records` | Platform Owner | Review platform and company administrative audit metadata without granting operational company-data access. |

The Platform Owner role alone cannot read operational company records. Such access requires a
separate company membership. The initial Platform Owner assignment is performed through a
controlled bootstrap process outside ordinary company-administration operations. Company
administrators can assign only company-scoped roles and can never assign global platform roles.

`GET /api/v1/platform/audit-records` is an explicit platform-audit scope, not operational
company access. It requires the authenticated caller's global Platform Owner grant and does not
require company membership. It returns platform events and company administrative/sensitive
events recorded by this feature; it excludes operational record contents and future operational
document activity. Company-specific audit reads remain subject to the selected company's
membership and audit permission.

Supported optional query parameters:

| Parameter | Meaning |
|---|---|
| `companyId` | UUID filter for a specific company; omit to include platform events and events from all companies. |
| `actorUserProfileId` | UUID filter for the verified actor. |
| `operation` | Exact stable operation identifier filter. |
| `targetType` | Exact target type filter. |
| `from` | Inclusive lower bound for `occurredAt`, as an ISO 8601 timestamp. |
| `to` | Inclusive upper bound for `occurredAt`, as an ISO 8601 timestamp. |
| `page` | One-based page number; defaults to `1`. |
| `pageSize` | Number of items per page; defaults to `50`, maximum `100`. |

When both `from` and `to` are supplied, `from` must not be later than `to`. Results are ordered
by `occurredAt` descending, then `id` descending for stable pagination. The response is:

```json
{
  "items": [
    {
      "id": "uuid",
      "companyId": "uuid or null",
      "actorUserProfileId": "uuid or null",
      "operation": "company.status.updated",
      "occurredAt": "ISO 8601 timestamp",
      "targetType": "Company",
      "targetId": "uuid",
      "changeSummary": "Redacted, non-sensitive administrative summary"
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalCount": 1
}
```

`items` contains audit metadata and the pre-sanitized non-sensitive `changeSummary` only; it
never includes company operational records, document contents, credentials, secrets, or raw
tokens. `companyId: null` identifies a platform-level event. Invalid filters return `422`;
missing authentication returns `401`, and a caller without the Platform Owner grant receives
`403`.

## Company context and administration

`/companies/{companyId}` is the explicit company scope. For company-scoped routes below, the API
verifies that the authenticated profile has the required Active membership and permission for
`companyId`, except for the invitation-acceptance operation.

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/companies/{companyId}/memberships` | List memberships visible to an authorized company administrator. |
| `PUT` | `/companies/{companyId}/logo` | Upload or replace the company's logo from a `multipart/form-data` file field named `file`. |
| `DELETE` | `/companies/{companyId}/logo` | Remove the company's logo and clear its stored path. |
| `POST` | `/companies/{companyId}/invitations` | Invite an email identity with an initial company role; creates a Pending membership or resends a valid Pending invitation. |
| `POST` | `/companies/{companyId}/invitations/{invitationId}/accept` | Accept a valid invitation as the authenticated matching identity; request body contains `{ "token": "<original invitation token>" }`. |
| `POST` | `/companies/{companyId}/invitations/{invitationId}/revoke` | Revoke a pending invitation and terminally revoke its associated membership. |
| `PUT` | `/companies/{companyId}/memberships/{membershipId}/status` | Transition an Active or Suspended membership to an allowed status without changing other-company memberships. |
| `PUT` | `/users/me/default-company` | Set the authenticated user's default company. |
| `GET`, `POST` | `/companies/{companyId}/roles` | List or create company-scoped roles from the available permission catalog. |
| `PUT` | `/companies/{companyId}/roles/{roleId}` | Update a company-scoped role and its allowed permission assignments. |
| `GET` | `/companies/{companyId}/permissions` | Read available permission identifiers for in-scope capabilities. |
| `GET`, `POST` | `/companies/{companyId}/branches` | List or create branches owned by the company. |
| `GET`, `PUT` | `/companies/{companyId}/regional-settings` | Read or atomically replace the company's regional settings. |
| `GET`, `POST` | `/companies/{companyId}/third-parties` | List or create company-owned customer/supplier records. |
| `GET`, `PUT` | `/companies/{companyId}/third-parties/{thirdPartyId}` | Read or update a company-owned third-party record. |
| `GET` | `/companies/{companyId}/audit-records` | Read audit entries allowed by the caller's company permissions. |

Membership and role APIs cannot modify records outside the selected company. The service rejects
an operation that would remove or demote the last active Company Administrator. Membership status
changes are transactional with this invariant check.

### Company logo

The logo is optional. `PUT /companies/{companyId}/logo` accepts one file in the multipart
`file` field. It accepts PNG, JPG/JPEG, or WebP files up to 2 MB; the backend validates the
file's actual format and size and does not rely only on a client-supplied filename or media type.
Both logo endpoints require an Active company, an Active membership, and the appropriate
company-scoped permission. The backend verifies the selected company and ensures any storage
object path belongs to that company; clients cannot authorize an operation by supplying a path.

The backend stores the object in the public-read Supabase Storage bucket `company-logos` under a
company-specific prefix, using a unique object name. Public read allows the frontend to display
the image; it does not allow browser-side upload, replacement, or deletion. Company responses
expose the nullable `logoStoragePath`, not a fixed URL; the client derives the public read URL
from the configured Storage endpoint and bucket.

On replacement, upload the new object first, then update `logoStoragePath` and persist its audit
record together. Remove the prior object only after the new reference commits. If upload or the
database/audit update fails, retain the previous reference and clean up the staged object. On
deletion, clear `logoStoragePath`, persist its audit record, and remove the associated object.
Storage and PostgreSQL do not share a transaction: the API must explicitly surface partial
storage failures and must not report a fully successful replacement or deletion until its
required storage cleanup and database/audit changes are complete. Successful deletion leaves
`logoStoragePath` null. Neither file bytes nor a fixed public URL are stored in Company or audit
records.

### Invitation and default-company rules

Invitation acceptance is an exception to the general Active-company and Active-membership
requirements. `POST /companies/{companyId}/invitations/{invitationId}/accept` requires an
authenticated user whose verified email matches the invitee email, a valid unexpired Pending
invitation, and the correct invitation token supplied in the request body's `token` property.
The backend verifies the received original token against the stored token digest. The original
token is never stored or included in logs, audit records, or responses. It may be accepted while
the target company is Suspended. In one logical operation, the API accepts the invitation,
activates its associated membership, and assigns the initial company role. It does not change
company status or grant operational access while the company remains Suspended.

When `POST /companies/{companyId}/invitations` finds a valid Pending invitation for the same
normalized email and company, it reuses that invitation and its associated Pending membership,
rotates the token digest, invalidates the previously issued link, and resets validity to seven
days from the resend. If the stored Pending invitation has reached its expiry time but expiration
has not yet been materialized, the operation first marks it Expired and revokes its Pending
membership, then creates a new invitation and Pending membership. An already-terminal invitation
also results in a new invitation and membership. Expiration, revocation of the prior Pending
membership, and replacement creation are processed consistently with the invitation state
transitions.

`PUT /users/me/default-company` derives the user exclusively from the validated access token;
the request selects the company but cannot select a user. The backend verifies that the caller has
an Active membership in that company before setting the caller's personal default-company
selection. A company administrator cannot change another user's preference.

`PUT /companies/{companyId}/memberships/{membershipId}/status` applies only these transitions:

- `Active` to `Suspended` or `Revoked`.
- `Suspended` to `Active` or `Revoked`.

`Pending` memberships are managed through invitation acceptance, revocation, and expiration, not
through this status operation. `Revoked` is terminal and cannot be reactivated. Reactivation from
`Suspended` to `Active` requires the applicable authorization and permission. The operation
performs the status change and last-active-Company-Administrator invariant check transactionally;
if the change would leave the company without an active Company Administrator, it is rejected.

## Regional reference catalogs

| Method | Path | Authorization | Purpose |
|---|---|---|---|
| `GET` | `/reference-catalogs` | Authenticated user | Read supported country, currency, language, culture, and time-zone values for regional configuration. |

`GET /reference-catalogs` returns the supported reference values needed to read and configure
company regional settings. It is read-only and does not require a company membership; the API
validates regional-setting values against these catalogs when settings are saved. These catalogs
do not introduce new operational workflows.

## Document configuration and issuance grants

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/companies/{companyId}/document-sources` | List globally defined document types available for company configuration. |
| `GET`, `POST` | `/companies/{companyId}/document-series` | List or create series, each required to reference a branch from the same company. |
| `GET`, `PUT`, `DELETE` | `/companies/{companyId}/document-series/{seriesId}` | Read/update/deactivate a series and its enabled document types and number formatting. |
| `GET` | `/companies/{companyId}/memberships/{membershipId}/document-series-permissions` | Read the membership's exact source-and-series emission grants. |
| `PUT` | `/companies/{companyId}/memberships/{membershipId}/document-series-permissions` | Replace the membership's exact source-and-series emission grants. |

`DELETE /companies/{companyId}/document-series/{seriesId}` is a logical deactivation, not a
physical deletion. It preserves the series, existing history, and all existing sequence values.
Disabling a DocumentSource/DocumentSeries combination likewise preserves its counter; it does not
delete or reset it. New number allocations are rejected if the series is inactive or the exact
source/series combination is disabled.

An emission grant is specific to one active membership, one enabled Document Source, and one
Document Series in the same company. A grant for one type in a series does not grant another
type in that series. Backend validation uses the verified actor and persisted membership; client
IDs are checked only as requested resource selectors.

This foundation does not expose complete invoice, purchase order, receipt, payment, sales, or
other operational document workflows. Future modules call the internal number-allocation
contract in [`document-numbering.md`](./document-numbering.md) within their own document
finalization transaction.

## Atomicity and auditing

- Company changes that affect related records are all-or-nothing.
- Invitation acceptance and membership activation are one logical operation.
- Number allocation and finalized document persistence share one PostgreSQL transaction.
- Rejected authorization, invalid company references, and rolled-back finalization do not consume
  a number.
- A committed later cancellation retains the issued number and writes its audit record; numbers
  are never reused.
- Failure to persist a required audit record prevents the sensitive operation from reporting
  success.

## Frontend expectations

The client may hide or disable controls based on fetched capabilities for usability, but the
backend rechecks every request. On company switch, company-scoped client data is reloaded or
invalidated before display. Forms distinguish pending, validation, conflict, success, and
transport/server failure states.
