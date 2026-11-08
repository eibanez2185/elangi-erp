REVOKE CREATE ON SCHEMA platform, security, organizations, configuration, third_parties, audit
    FROM PUBLIC;
REVOKE ALL ON SCHEMA platform, security, organizations, configuration, third_parties, audit
    FROM anon, authenticated, service_role;
