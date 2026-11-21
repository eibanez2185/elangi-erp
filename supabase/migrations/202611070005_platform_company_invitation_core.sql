ALTER TABLE platform.companies
    ADD COLUMN legal_name text NOT NULL CHECK (btrim(legal_name) <> ''),
    ADD COLUMN trade_name text,
    ADD COLUMN country_code text NOT NULL CHECK (country_code ~ '^[A-Z]{2}$'),
    ADD COLUMN legal_identifier text,
    ADD COLUMN address text,
    ADD COLUMN contact_details jsonb NOT NULL DEFAULT '{}'::jsonb
        CHECK (jsonb_typeof(contact_details) = 'object'),
    ADD COLUMN subscription_plan_reference text,
    ADD COLUMN subscription_status text,
    ADD COLUMN subscription_started_at timestamptz,
    ADD COLUMN subscription_ends_at timestamptz,
    ADD CONSTRAINT companies_subscription_dates_ordered
        CHECK (
            subscription_started_at IS NULL
            OR subscription_ends_at IS NULL
            OR subscription_ends_at >= subscription_started_at
        );

INSERT INTO platform.platform_roles (code, name)
VALUES ('platform-owner', 'Platform Owner');

CREATE TABLE security.invitations (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL,
    membership_id uuid NOT NULL,
    invitee_email text NOT NULL CHECK (
        invitee_email = lower(btrim(invitee_email))
        AND invitee_email <> ''
    ),
    initial_role_id uuid NOT NULL,
    status text NOT NULL CHECK (status IN ('Pending', 'Accepted', 'Revoked', 'Expired')),
    token_digest text NOT NULL UNIQUE,
    expires_at timestamptz NOT NULL,
    created_by_membership_id uuid,
    created_by_platform_profile_id uuid,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (company_id, membership_id)
        REFERENCES security.memberships(company_id, id),
    FOREIGN KEY (company_id, initial_role_id)
        REFERENCES security.company_roles(company_id, id),
    FOREIGN KEY (company_id, created_by_membership_id)
        REFERENCES security.memberships(company_id, id),
    FOREIGN KEY (created_by_platform_profile_id)
        REFERENCES security.user_profiles(id),
    CHECK (
        (created_by_membership_id IS NULL) <>
        (created_by_platform_profile_id IS NULL)
    ),
    UNIQUE (company_id, id)
);

CREATE UNIQUE INDEX invitations_pending_company_email_uidx
    ON security.invitations (company_id, invitee_email)
    WHERE status = 'Pending';

CREATE INDEX invitations_company_status_expiry_idx
    ON security.invitations (company_id, status, expires_at);

ALTER TABLE platform.companies ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform.companies FORCE ROW LEVEL SECURITY;
ALTER TABLE security.invitations ENABLE ROW LEVEL SECURITY;
ALTER TABLE security.invitations FORCE ROW LEVEL SECURITY;

CREATE POLICY platform_roles_read_assigned_owner
    ON platform.platform_roles
    FOR SELECT
    TO elangi_runtime
    USING (
        is_active
        AND EXISTS (
            SELECT 1
            FROM platform.user_platform_roles AS assignment
            JOIN security.user_profiles AS profile
                ON profile.id = assignment.user_profile_id
            WHERE assignment.role_id = platform_roles.id
              AND profile.auth_user_id =
                  nullif(current_setting('app.current_actor_id', true), '')::uuid
        )
    );

CREATE POLICY user_platform_roles_read_self
    ON platform.user_platform_roles
    FOR SELECT
    TO elangi_runtime
    USING (
        EXISTS (
            SELECT 1
            FROM security.user_profiles AS profile
            WHERE profile.id = user_platform_roles.user_profile_id
              AND profile.auth_user_id =
                  nullif(current_setting('app.current_actor_id', true), '')::uuid
        )
    );

CREATE POLICY companies_read_platform_owner
    ON platform.companies
    FOR SELECT
    TO elangi_runtime
    USING (current_setting('app.is_platform_owner', true) = 'true');

CREATE POLICY companies_insert_platform_owner
    ON platform.companies
    FOR INSERT
    TO elangi_runtime
    WITH CHECK (
        current_setting('app.is_platform_owner', true) = 'true'
        AND status = 'Suspended'
    );

CREATE POLICY companies_update_platform_owner
    ON platform.companies
    FOR UPDATE
    TO elangi_runtime
    USING (current_setting('app.is_platform_owner', true) = 'true')
    WITH CHECK (current_setting('app.is_platform_owner', true) = 'true');

CREATE POLICY company_roles_read_platform_owner
    ON security.company_roles
    FOR SELECT
    TO elangi_runtime
    USING (current_setting('app.is_platform_owner', true) = 'true');

CREATE POLICY company_roles_insert_initial_admin
    ON security.company_roles
    FOR INSERT
    TO elangi_runtime
    WITH CHECK (
        current_setting('app.is_platform_owner', true) = 'true'
        AND name = 'Company Administrator'
        AND is_active
    );

CREATE POLICY memberships_read_platform_owner
    ON security.memberships
    FOR SELECT
    TO elangi_runtime
    USING (current_setting('app.is_platform_owner', true) = 'true');

CREATE POLICY memberships_insert_pending_invitation
    ON security.memberships
    FOR INSERT
    TO elangi_runtime
    WITH CHECK (
        current_setting('app.is_platform_owner', true) = 'true'
        AND status = 'Pending'
        AND user_profile_id IS NULL
        AND NOT is_default_company
    );

CREATE POLICY memberships_revoke_undelivered_invitation
    ON security.memberships
    FOR UPDATE
    TO elangi_runtime
    USING (
        current_setting('app.is_platform_owner', true) = 'true'
        AND status = 'Pending'
        AND user_profile_id IS NULL
        AND NOT is_default_company
    )
    WITH CHECK (
        current_setting('app.is_platform_owner', true) = 'true'
        AND status = 'Revoked'
        AND user_profile_id IS NULL
        AND NOT is_default_company
    );

CREATE POLICY invitations_read_platform_owner
    ON security.invitations
    FOR SELECT
    TO elangi_runtime
    USING (current_setting('app.is_platform_owner', true) = 'true');

CREATE POLICY invitations_insert_platform_owner
    ON security.invitations
    FOR INSERT
    TO elangi_runtime
    WITH CHECK (
        current_setting('app.is_platform_owner', true) = 'true'
        AND status = 'Pending'
        AND created_by_membership_id IS NULL
        AND created_by_platform_profile_id IN (
            SELECT profile.id
            FROM security.user_profiles AS profile
            WHERE profile.auth_user_id =
                nullif(current_setting('app.current_actor_id', true), '')::uuid
        )
        AND EXISTS (
            SELECT 1
            FROM security.company_roles AS company_role
            WHERE company_role.company_id = invitations.company_id
              AND company_role.id = invitations.initial_role_id
              AND company_role.name = 'Company Administrator'
              AND company_role.is_active
        )
    );

CREATE POLICY invitations_revoke_platform_owner
    ON security.invitations
    FOR UPDATE
    TO elangi_runtime
    USING (
        current_setting('app.is_platform_owner', true) = 'true'
        AND status = 'Pending'
    )
    WITH CHECK (
        current_setting('app.is_platform_owner', true) = 'true'
        AND status = 'Revoked'
    );

GRANT SELECT ON platform.platform_roles, platform.user_platform_roles TO elangi_runtime;
GRANT SELECT, INSERT, UPDATE ON platform.companies TO elangi_runtime;
GRANT SELECT, INSERT ON security.company_roles, security.memberships TO elangi_runtime;
GRANT UPDATE (status, updated_at) ON security.memberships TO elangi_runtime;
GRANT SELECT, INSERT ON security.invitations TO elangi_runtime;
GRANT UPDATE (status, updated_at) ON security.invitations TO elangi_runtime;
