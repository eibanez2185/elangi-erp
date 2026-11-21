# Administración de plataforma ELANGI ERP

## Configuración local

1. Inicia Supabase local con `npx supabase start`.
2. Copia `.env.example` a `.env.local`.
3. Configura `VITE_SUPABASE_URL` con la URL local de Supabase y
   `VITE_SUPABASE_ANON_KEY` con la clave anónima/publicable que muestra
   `npx supabase status -o env`. No uses `service_role`, `secret` ni credenciales de PostgreSQL
   en el frontend.
4. Inicia la API con la configuración local cargada:

   ```powershell
   . .\supabase\scripts\load-local-environment.ps1
   $env:ConnectionStrings__ApplicationDatabase = "Host=127.0.0.1;Port=54322;Database=postgres;Username=elangi_runtime;Password=<local-runtime-password>"
   dotnet run --project .\src\Elangi.Api\Elangi.Api.csproj --urls http://localhost:5000
   ```

5. En otra terminal:

   ```powershell
   Set-Location .\frontend
   Copy-Item .env.example .env.local
   npm install
   npm run dev
   ```

La aplicación inicia sesión mediante Supabase Auth y envía el access token a la API. El backend
vuelve a validar el rol Platform Owner en cada operación. La invitación inicial se entrega por
correo SMTP local y el token original no se devuelve a la interfaz.
Los correos de desarrollo pueden consultarse en Mailpit: `http://127.0.0.1:54324`.

## Comandos

- `npm run dev`: inicia Vite.
- `npm run build`: verifica TypeScript y genera la compilación.
- `npm test`: ejecuta las pruebas de componentes.
- `npm run lint`: analiza el frontend con Oxlint.
