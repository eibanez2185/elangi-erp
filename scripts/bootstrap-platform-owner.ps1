param(
    [Parameter(Mandatory = $true)]
    [string]$Email
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repositoryRoot

$statusLines = & $env:ComSpec /d /c 'npx supabase status -o env 2>nul'
if ($LASTEXITCODE -ne 0) {
    throw "Unable to read local Supabase status. Start the project with .\scripts\start-local.ps1 first."
}

$settings = @{}
foreach ($line in $statusLines) {
    if ($line -match '^\s*([A-Z_]+)=(?:"(.*)"|(.*))\s*$') {
        $settings[$Matches[1]] = if ($null -ne $Matches[2]) { $Matches[2] } else { $Matches[3] }
    }
}

if (-not $settings.ContainsKey('DB_URL') -or
    [string]::IsNullOrWhiteSpace($settings['DB_URL'])) {
    throw 'Local Supabase status did not include DB_URL.'
}

$databaseUri = [Uri]$settings['DB_URL']
if ($databaseUri.Scheme -notin @('postgres', 'postgresql') -or
    $databaseUri.Host -notin @('127.0.0.1', 'localhost', '::1') -or
    $databaseUri.Port -ne 54322) {
    throw 'The bootstrap script only accepts the local Supabase database at port 54322.'
}

$credentials = $databaseUri.UserInfo.Split(':', 2)
if ($credentials.Length -ne 2) {
    throw 'The local Supabase DB_URL did not include database credentials.'
}

$connectionString = New-Object System.Data.Common.DbConnectionStringBuilder
$connectionString['Host'] = $databaseUri.Host
$connectionString['Port'] = $databaseUri.Port
$connectionString['Database'] = [Uri]::UnescapeDataString($databaseUri.AbsolutePath.TrimStart('/'))
$connectionString['Username'] = [Uri]::UnescapeDataString($credentials[0])
$connectionString['Password'] = [Uri]::UnescapeDataString($credentials[1])

$previousConnectionString = $env:ELANGI_PLATFORM_BOOTSTRAP_DATABASE
try {
    $env:ELANGI_PLATFORM_BOOTSTRAP_DATABASE = $connectionString.ConnectionString
    & dotnet run --project '.\src\Elangi.Api\Elangi.Api.csproj' -- `
        --bootstrap-platform-owner $Email
    if ($LASTEXITCODE -ne 0) {
        throw "Platform Owner bootstrap failed (exit code $LASTEXITCODE)."
    }
}
finally {
    if ($null -eq $previousConnectionString) {
        Remove-Item Env:ELANGI_PLATFORM_BOOTSTRAP_DATABASE -ErrorAction SilentlyContinue
    }
    else {
        $env:ELANGI_PLATFORM_BOOTSTRAP_DATABASE = $previousConnectionString
    }
}
