# Internal Contract: Transactional Document Number Allocation

## Purpose and scope

This internal application contract lets future document modules receive a number while
finalizing a document. It does not implement document-specific business workflows.

## Allocation scope

One counter is identified by the exact tuple:

`(companyId, documentSourceId, documentSeriesId)`

`DocumentSource` is a document type. `DocumentSeries` is a company-owned emission series linked
to exactly one Branch in the same company. A series may be enabled for multiple document types;
each type/series combination has an independent counter.

## Preconditions

The calling use case verifies the Supabase access token before opening the database transaction
and derives the actor from its verified subject. Within the same database transaction that
allocates the number and finalizes the document, it must:

1. Verify the company is Active and the actor has an Active membership in that company.
2. Verify that the membership has an emission grant for this exact document source and series.
3. Verify that the source, series, series-to-branch association, membership, and grant all
   belong to the same company, that the DocumentSeries is Active, and that the source/series pair
   is enabled.
4. Complete all other validation that can reject the operation before consuming a number.

No client-provided identifier is accepted as proof of identity, company access, or permission.
The company status, membership, exact grant, DocumentSeries active status, and enabled
source/series pair are read and protected inside this transaction. Acquire row locks (or
equivalent database serialization) on these authorization/scope records, including the
DocumentSeries row, before relying on their values, and retain protection through commit.
Operations that suspend the company or membership, revoke the grant, deactivate the
DocumentSeries, or disable the source/series pair must acquire conflicting locks on the same
records. Use a consistent lock ordering across emission and those state-changing operations.
This makes an emission and a concurrent authorization, series-state, or enablement change
serialize: either the emission validates and commits before the change, or it observes the
committed change and is rejected. Do not rely on checks performed only before the transaction or
on frontend state.

## Operation contract

Conceptually:

`AllocateNext(companyId, documentSourceId, documentSeriesId, actor, transaction) -> { value, formattedNumber }`

- Requires the caller's active database transaction.
- In that transaction, atomically increments the counter row for the tuple in PostgreSQL and
  returns the new value from that operation (for example, an update-returning operation or an
  equivalent concurrency-safe primitive). Never read the current value, increment it in
  application memory, and save it in a later operation.
- Initial stored counter value is zero; the first committed allocation is one.
- Formats the value using the prefix and zero-padding width configured for that exact tuple.
- Persists the numeric value and formatted number on the finalized document within the same
  transaction.
- Commits the counter and finalized document together.
- If validation or finalization fails before commit, rollback restores the counter.
- Different tuples can allocate independently; simultaneous allocations for one tuple serialize
  and return distinct values.
- Counter state is continuous and does not reset periodically.
- The calling document module owns request idempotency for retries after a lost response. It must
  define an idempotency mechanism for its emission requests so a retry does not create a second
  document or consume another number. Implementing that mechanism is outside this numbering
  contract.

The formatted number is a string for display/reference; the stored integer remains the authoritative
sequence value. Number identity includes the full company/source/series tuple and allocated value;
formatted strings are not guaranteed unique across different tuples. A change to prefix or padding
does not reset or decrement the counter.

## Cancellation and audit

After a finalized document commits, subsequent cancellation retains its assigned numeric and
formatted number. The counter is never decremented, and the number is never reused. Cancellation
is not a rollback of the original issuance. The owning document's status change and creation of
the corresponding durable audit record occur in the same database transaction. If either action
fails, the transaction rolls back; a cancellation cannot commit without its audit record.

## Failure behavior

| Condition | Result |
|---|---|
| Missing/invalid token | Reject; do not allocate. |
| Company is Suspended | Reject; do not allocate. |
| Missing/inactive membership or missing exact grant | Reject; do not allocate. |
| Company/source/series/branch ownership mismatch | Reject without disclosing foreign data; do not allocate. |
| DocumentSeries is inactive | Reject; do not allocate. |
| Source/series pair disabled | Reject; do not allocate. |
| Concurrent authorized allocation | Serialize per tuple and return distinct values. |
| Concurrent suspension, membership/grant change, or source/series disablement | Serialize with the emission transaction; reject if the change commits first, otherwise allow the already-authorized emission to commit first. |
| Finalization fails before transaction commit | Roll back document and counter increment. |
| Cancellation status update or audit insert fails | Roll back both; no unaudited cancellation is committed. |
| Cancellation after committed finalization | Retain number, mark canceled and write its audit record atomically; never reuse. |

## Integrity requirements

- Enforce uniqueness of the counter scope tuple.
- Reference an enabled source/series pair and same-company branch through tenant-aware constraints.
- Restrict counter table writes to the trusted application/database operation.
- Runtime database access is non-owner and cannot bypass RLS.
- The caller cannot allocate a number through a client-only or unauthenticated path.
