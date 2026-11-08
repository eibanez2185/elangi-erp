CREATE TABLE platform.companies (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    status text NOT NULL DEFAULT 'Suspended'
        CHECK (status IN ('Active', 'Suspended')),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE security.user_profiles (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    auth_user_id uuid NOT NULL UNIQUE REFERENCES auth.users(id) ON DELETE CASCADE,
    display_name text,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE security.memberships (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL REFERENCES platform.companies(id),
    user_profile_id uuid REFERENCES security.user_profiles(id),
    status text NOT NULL CHECK (status IN ('Pending', 'Active', 'Suspended', 'Revoked')),
    is_default_company boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (company_id, id)
);

CREATE UNIQUE INDEX memberships_company_profile_nonrevoked_uidx
    ON security.memberships (company_id, user_profile_id)
    WHERE user_profile_id IS NOT NULL AND status <> 'Revoked';

CREATE INDEX memberships_company_profile_status_idx
    ON security.memberships (company_id, user_profile_id, status);

CREATE TABLE platform.permission_catalog (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code text NOT NULL UNIQUE,
    description text NOT NULL,
    scope text NOT NULL CHECK (scope IN ('Platform', 'Company')),
    UNIQUE (id, scope)
);

CREATE TABLE security.company_roles (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL REFERENCES platform.companies(id),
    name text NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (company_id, id),
    UNIQUE (company_id, name)
);

CREATE TABLE security.company_role_permissions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL,
    role_id uuid NOT NULL,
    permission_id uuid NOT NULL,
    permission_scope text NOT NULL DEFAULT 'Company'
        CHECK (permission_scope = 'Company'),
    FOREIGN KEY (company_id, role_id)
        REFERENCES security.company_roles(company_id, id),
    FOREIGN KEY (permission_id, permission_scope)
        REFERENCES platform.permission_catalog(id, scope),
    UNIQUE (company_id, role_id, permission_id)
);

CREATE TABLE security.membership_roles (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL,
    membership_id uuid NOT NULL,
    role_id uuid NOT NULL,
    FOREIGN KEY (company_id, membership_id)
        REFERENCES security.memberships(company_id, id),
    FOREIGN KEY (company_id, role_id)
        REFERENCES security.company_roles(company_id, id),
    UNIQUE (company_id, membership_id, role_id)
);

CREATE TABLE platform.platform_roles (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code text NOT NULL UNIQUE,
    name text NOT NULL,
    is_active boolean NOT NULL DEFAULT true
);

CREATE TABLE platform.platform_role_permissions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    role_id uuid NOT NULL REFERENCES platform.platform_roles(id),
    permission_id uuid NOT NULL,
    permission_scope text NOT NULL DEFAULT 'Platform'
        CHECK (permission_scope = 'Platform'),
    FOREIGN KEY (permission_id, permission_scope)
        REFERENCES platform.permission_catalog(id, scope),
    UNIQUE (role_id, permission_id)
);

CREATE TABLE platform.user_platform_roles (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_profile_id uuid NOT NULL REFERENCES security.user_profiles(id),
    role_id uuid NOT NULL REFERENCES platform.platform_roles(id),
    UNIQUE (user_profile_id, role_id)
);

ALTER TABLE platform.companies ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform.companies FORCE ROW LEVEL SECURITY;
ALTER TABLE security.user_profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE security.user_profiles FORCE ROW LEVEL SECURITY;
ALTER TABLE security.memberships ENABLE ROW LEVEL SECURITY;
ALTER TABLE security.memberships FORCE ROW LEVEL SECURITY;
ALTER TABLE platform.permission_catalog ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform.permission_catalog FORCE ROW LEVEL SECURITY;
ALTER TABLE security.company_roles ENABLE ROW LEVEL SECURITY;
ALTER TABLE security.company_roles FORCE ROW LEVEL SECURITY;
ALTER TABLE security.company_role_permissions ENABLE ROW LEVEL SECURITY;
ALTER TABLE security.company_role_permissions FORCE ROW LEVEL SECURITY;
ALTER TABLE security.membership_roles ENABLE ROW LEVEL SECURITY;
ALTER TABLE security.membership_roles FORCE ROW LEVEL SECURITY;
ALTER TABLE platform.platform_roles ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform.platform_roles FORCE ROW LEVEL SECURITY;
ALTER TABLE platform.platform_role_permissions ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform.platform_role_permissions FORCE ROW LEVEL SECURITY;
ALTER TABLE platform.user_platform_roles ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform.user_platform_roles FORCE ROW LEVEL SECURITY;

CREATE POLICY user_profiles_read_self
    ON security.user_profiles
    FOR SELECT
    TO elangi_runtime
    USING (
        auth_user_id = nullif(current_setting('app.current_actor_id', true), '')::uuid
    );

CREATE POLICY companies_read_membership_scope
    ON platform.companies
    FOR SELECT
    TO elangi_runtime
    USING (
        id = nullif(current_setting('app.current_company_id', true), '')::uuid
        AND EXISTS (
            SELECT 1
            FROM security.memberships AS membership
            WHERE membership.company_id = companies.id
        )
    );

CREATE POLICY memberships_read_self
    ON security.memberships
    FOR SELECT
    TO elangi_runtime
    USING (
        company_id = nullif(current_setting('app.current_company_id', true), '')::uuid
        AND user_profile_id IN (
            SELECT profile.id
            FROM security.user_profiles AS profile
            WHERE profile.auth_user_id =
                nullif(current_setting('app.current_actor_id', true), '')::uuid
        )
    );

CREATE POLICY permission_catalog_read_company_scope
    ON platform.permission_catalog
    FOR SELECT
    TO elangi_runtime
    USING (scope = 'Company');

CREATE POLICY company_roles_read_assigned
    ON security.company_roles
    FOR SELECT
    TO elangi_runtime
    USING (
        company_id = nullif(current_setting('app.current_company_id', true), '')::uuid
        AND EXISTS (
            SELECT 1
            FROM security.membership_roles AS membership_role
            WHERE membership_role.company_id = company_roles.company_id
              AND membership_role.role_id = company_roles.id
        )
    );

CREATE POLICY membership_roles_read_self
    ON security.membership_roles
    FOR SELECT
    TO elangi_runtime
    USING (
        company_id = nullif(current_setting('app.current_company_id', true), '')::uuid
        AND EXISTS (
            SELECT 1
            FROM security.memberships AS membership
            WHERE membership.company_id = membership_roles.company_id
              AND membership.id = membership_roles.membership_id
        )
    );

CREATE POLICY company_role_permissions_read_assigned
    ON security.company_role_permissions
    FOR SELECT
    TO elangi_runtime
    USING (
        company_id = nullif(current_setting('app.current_company_id', true), '')::uuid
        AND EXISTS (
            SELECT 1
            FROM security.membership_roles AS membership_role
            WHERE membership_role.company_id = company_role_permissions.company_id
              AND membership_role.role_id = company_role_permissions.role_id
        )
    );

REVOKE ALL ON platform.companies, security.user_profiles, security.memberships,
    platform.permission_catalog, security.company_roles, security.company_role_permissions,
    security.membership_roles, platform.platform_roles, platform.platform_role_permissions,
    platform.user_platform_roles
    FROM PUBLIC, anon, authenticated, service_role, elangi_runtime;

GRANT SELECT ON platform.companies, security.user_profiles, security.memberships,
    platform.permission_catalog, security.company_roles, security.company_role_permissions,
    security.membership_roles TO elangi_runtime;
