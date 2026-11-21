param(
    [ValidateSet('Api', 'Frontend')]
    [string]$Service
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$frontendPath = Join-Path $repositoryRoot 'frontend'

if ($Service -eq 'Api') {
    Set-Location $repositoryRoot
    & dotnet run --project '.\src\Elangi.Api\Elangi.Api.csproj' --urls 'http://localhost:5000'
    exit $LASTEXITCODE
}

if ($Service -eq 'Frontend') {
    Set-Location $frontendPath
    & npm run dev -- --host 127.0.0.1
    exit $LASTEXITCODE
}

Set-Location $repositoryRoot

$startOutput = & $env:ComSpec /d /c 'npx supabase start 2>nul'
$startExitCode = $LASTEXITCODE
if ($startExitCode -ne 0) {
    throw "Unable to start local Supabase (exit code $startExitCode). Ensure Docker Desktop is running."
}

$migrationOutput = & $env:ComSpec /d /c 'npx supabase migration up 2>nul'
$migrationExitCode = $LASTEXITCODE
if ($migrationExitCode -ne 0) {
    throw "Unable to apply pending local Supabase migrations (exit code $migrationExitCode)."
}

$statusLines = & $env:ComSpec /d /c 'npx supabase status -o env 2>nul'
$statusExitCode = $LASTEXITCODE
if ($statusExitCode -ne 0) {
    throw "Unable to read local Supabase settings (exit code $statusExitCode)."
}

$settings = @{}
foreach ($line in $statusLines) {
    if ($line -match '^\s*([A-Z_]+)=(?:"(.*)"|(.*))\s*$') {
        $settings[$Matches[1]] = if ($null -ne $Matches[2]) { $Matches[2] } else { $Matches[3] }
    }
}

foreach ($required in @('API_URL', 'DB_URL', 'JWT_SECRET')) {
    if (-not $settings.ContainsKey($required) -or
        [string]::IsNullOrWhiteSpace($settings[$required])) {
        throw "Local Supabase status did not include the required $required value."
    }
}

$publicKey = $settings['PUBLISHABLE_KEY']
if ([string]::IsNullOrWhiteSpace($publicKey)) {
    $publicKey = $settings['ANON_KEY']
}
if ([string]::IsNullOrWhiteSpace($publicKey)) {
    throw "Local Supabase status did not include a publishable or anonymous key."
}

$databaseUri = [Uri]$settings['DB_URL']
if ($databaseUri.Scheme -ne 'postgresql' -or
    $databaseUri.Host -notin @('127.0.0.1', 'localhost') -or
    $databaseUri.Port -lt 1) {
    throw "The database URL must point to the local Supabase instance."
}

$runtimePasswordPath = Join-Path $repositoryRoot '.env.elangi.local'
$runtimePassword = $env:ELANGI_RUNTIME_PASSWORD
if ([string]::IsNullOrWhiteSpace($runtimePassword) -and
    (Test-Path -LiteralPath $runtimePasswordPath)) {
    foreach ($line in Get-Content -LiteralPath $runtimePasswordPath) {
        if ($line -match '^ELANGI_RUNTIME_PASSWORD=(.*)$') {
            $runtimePassword = $Matches[1].Trim()
            break
        }
    }
}

if ([string]::IsNullOrWhiteSpace($runtimePassword)) {
    $runtimePassword = [Guid]::NewGuid().ToString('N')
    Set-Content -LiteralPath $runtimePasswordPath `
        -Value "ELANGI_RUNTIME_PASSWORD=$runtimePassword" `
        -Encoding Ascii
}

$configuration = Get-Content -LiteralPath (Join-Path $repositoryRoot 'supabase\config.toml') -Raw
if ($configuration -notmatch '(?m)^\s*project_id\s*=\s*"([^"]+)"') {
    throw "Unable to read the Supabase project_id from supabase/config.toml."
}
$databaseContainer = "supabase_db_$($Matches[1])"
$sql = "ALTER ROLE elangi_runtime WITH LOGIN PASSWORD '$runtimePassword';"
$databaseCommand = "docker exec -i $databaseContainer psql -U postgres -d postgres -v ON_ERROR_STOP=1 2>nul"
$databaseOutput = $sql | & $env:ComSpec /d /c $databaseCommand
$databaseExitCode = $LASTEXITCODE
if ($databaseExitCode -ne 0) {
    throw "Unable to configure the local elangi_runtime database password (exit code $databaseExitCode)."
}

$databaseName = [Uri]::UnescapeDataString($databaseUri.AbsolutePath.TrimStart('/'))
$env:SUPABASE_JWT_ISSUER = "$($settings['API_URL'].TrimEnd('/'))/auth/v1"
$env:SUPABASE_JWT_AUDIENCE = 'authenticated'
$env:SUPABASE_JWT_SECRET = $settings['JWT_SECRET']
$env:ConnectionStrings__ApplicationDatabase =
    "Host=127.0.0.1;Port=$($databaseUri.Port);Database=$databaseName;Username=elangi_runtime;Password=$runtimePassword"
$env:VITE_SUPABASE_URL = $settings['API_URL'].TrimEnd('/')
$env:VITE_SUPABASE_ANON_KEY = $publicKey
$env:VITE_API_BASE_URL = 'http://localhost:5000'

if (-not (Test-Path -LiteralPath (Join-Path $frontendPath 'node_modules'))) {
    $installCommand = 'npm ci --prefix "' + $frontendPath + '" 2>nul'
    $installOutput = & $env:ComSpec /d /c $installCommand
    $installExitCode = $LASTEXITCODE
    if ($installExitCode -ne 0) {
        throw "Unable to install frontend dependencies with npm ci (exit code $installExitCode)."
    }
}

$shellPath = (Get-Process -Id $PID).Path
$apiArguments = "-NoExit -File `"$PSCommandPath`" -Service Api"
$frontendArguments = "-NoExit -File `"$PSCommandPath`" -Service Frontend"

Start-Process -FilePath $shellPath `
    -ArgumentList $apiArguments `
    -WorkingDirectory $repositoryRoot
Start-Process -FilePath $shellPath `
    -ArgumentList $frontendArguments `
    -WorkingDirectory $frontendPath

Write-Host 'Local ELANGI services are starting in separate PowerShell windows.'
Write-Host 'API:      http://localhost:5000'
Write-Host 'Frontend: http://localhost:5173'
Write-Host 'Mailpit:  http://127.0.0.1:54324'
Write-Host 'Close each service window or press Ctrl+C in it to stop that service.'
