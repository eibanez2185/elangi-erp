CREATE TABLE audit.audit_records (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid,
    actor_user_profile_id uuid,
    operation text NOT NULL CHECK (operation ~ '^[a-z][a-z0-9_.-]*$'),
    occurred_at timestamptz NOT NULL,
    target_type text NOT NULL,
    target_id uuid,
    change_summary jsonb
);

ALTER TABLE audit.audit_records ENABLE ROW LEVEL SECURITY;
ALTER TABLE audit.audit_records FORCE ROW LEVEL SECURITY;

CREATE INDEX audit_records_company_occurred_at_idx
    ON audit.audit_records (company_id, occurred_at DESC);

GRANT SELECT, INSERT ON audit.audit_records TO elangi_runtime;
REVOKE UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER
    ON audit.audit_records FROM elangi_runtime;

CREATE POLICY audit_records_insert_scoped
    ON audit.audit_records
    FOR INSERT
    TO elangi_runtime
    WITH CHECK (
        nullif(current_setting('app.current_actor_id', true), '') IS NOT NULL
        AND (
            company_id = nullif(current_setting('app.current_company_id', true), '')::uuid
            OR (
                company_id IS NULL
                AND current_setting('app.is_platform_owner', true) = 'true'
            )
        )
    );

CREATE POLICY audit_records_read_scoped
    ON audit.audit_records
    FOR SELECT
    TO elangi_runtime
    USING (
        company_id = nullif(current_setting('app.current_company_id', true), '')::uuid
        OR (
            company_id IS NULL
            AND current_setting('app.is_platform_owner', true) = 'true'
        )
    );
