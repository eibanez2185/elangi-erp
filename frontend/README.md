# Administración de plataforma ELANGI ERP

## Inicio local

Desde la raíz del repositorio ejecuta:

```powershell
.\scripts\start-local.ps1
```

El script inicia Supabase local, la API y Vite en ventanas separadas. No se necesita copiar claves
locales al archivo `.env.local`; las variables se pasan a los procesos desde el estado de Supabase.
No uses claves `service_role`, secretas ni credenciales de PostgreSQL en el frontend.

El inicio de sesión usa Supabase Auth y la API valida el rol Platform Owner en cada operación.
La pantalla de inicio se puede ver sin ese rol, pero la administración de empresas requiere una
cuenta con Platform Owner asignado mediante el proceso de bootstrap controlado, que aún no está
implementado. El inbox de correo local está en `http://127.0.0.1:54324`.

## Comandos

- `npm run dev`: inicia Vite.
- `npm run build`: verifica TypeScript y genera la compilación.
- `npm test`: ejecuta las pruebas de componentes.
- `npm run lint`: analiza el frontend con Oxlint.
