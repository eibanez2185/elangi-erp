# ELANGI ERP

## Local development

Requirements: .NET 10 SDK, Node.js/npm, Supabase CLI, and Docker Desktop.

Start the complete local environment from the repository root:

```powershell
.\scripts\start-local.ps1
```

The script starts local Supabase, applies pending migrations without resetting the database,
configures the API and frontend from local Supabase settings, installs frontend dependencies when
needed, and opens the API and Vite frontend in separate PowerShell windows. Open
`http://localhost:5173` to view the frontend; API health is at `http://localhost:5000/health`.
The local Mailpit inbox is at `http://127.0.0.1:54324`.

The launcher stores a local-only runtime database password in `.env.elangi.local`, which is
ignored by Git. It does not reset local database data or target a hosted Supabase project. Use
versioned SQL migrations for schema changes; do not make manual schema changes through Supabase
Studio or run `db push` against a hosted project.

The login screen can be viewed immediately. Using platform administration requires a local
authenticated user with the Platform Owner role; the controlled bootstrap flow for that
assignment has not yet been implemented.

Run the foundation checks:

```powershell
dotnet test .\Elangi.sln
npm --prefix .\frontend test
npm --prefix .\frontend run build
```

The frontend is a React + TypeScript Vite application. Supabase's local services use the
PostgreSQL major version configured in `supabase/config.toml` (17); no paid-plan service is
required for this foundation.
