# ELANGI ERP

## Local development

Requirements: .NET 10 SDK, Node.js/npm, Supabase CLI, and Docker Desktop.

Start and reset only the local Supabase project:

```powershell
npx supabase start
npx supabase db reset
```

`db reset` targets the local project by default; do not add `--remote`. Migrations in
`supabase/migrations/` are applied in filename order by the CLI. Keep schema changes in versioned
SQL migrations; do not make manual schema changes through Supabase Studio or run `db push`
against a hosted project.

The runtime role receives no default table grants. Each migration must enable and force RLS on
new tables before explicitly granting the runtime role only the required operations.

The API uses the `elangi_runtime` PostgreSQL login, not the database owner. After the local
migrations create that role, set its password through the local database prompt:

```powershell
docker exec -it supabase_db_elangi-erp psql -U postgres -d postgres
```

At the `psql` prompt run `\password elangi_runtime`, enter a local-only password twice, then
exit with `\q`. Configure `ConnectionStrings__ApplicationDatabase` in the current shell using
that role and password; do not commit the value.

Load local Auth settings and the database URL for integration tests without printing generated
keys:

```powershell
. .\supabase\scripts\load-local-environment.ps1
```

The script sets `SUPABASE_JWT_ISSUER`, `SUPABASE_JWT_AUDIENCE`, `SUPABASE_JWT_SECRET`, and
`ELANGI_TEST_DATABASE` only in the current PowerShell process. Do not copy these values into
source files or logs.

Run the foundation checks:

```powershell
dotnet test .\Elangi.sln
npm --prefix .\frontend test
npm --prefix .\frontend run build
```

The frontend is a React + TypeScript Vite application. Supabase's local services use the
PostgreSQL major version configured in `supabase/config.toml` (17); no paid-plan service is
required for this foundation.
