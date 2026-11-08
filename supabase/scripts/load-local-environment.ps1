$statusLines = & npx supabase status -o env
if ($LASTEXITCODE -ne 0) {
    throw "Unable to read local Supabase status. Start the local project with 'npx supabase start'."
}

$values = @{}
foreach ($line in $statusLines) {
    if ($line -match '^\s*([A-Z_]+)=(?:"(.*)"|(.*))\s*$') {
        $values[$Matches[1]] = if ($Matches[2]) { $Matches[2] } else { $Matches[3] }
    }
}

foreach ($required in @('API_URL', 'DB_URL', 'JWT_SECRET')) {
    if (-not $values.ContainsKey($required) -or [string]::IsNullOrWhiteSpace($values[$required])) {
        throw "Local Supabase status did not include the required $required value."
    }
}

$env:SUPABASE_JWT_ISSUER = "$($values['API_URL'].TrimEnd('/'))/auth/v1"
$env:SUPABASE_JWT_AUDIENCE = 'authenticated'
$env:SUPABASE_JWT_SECRET = $values['JWT_SECRET']
$env:ELANGI_TEST_DATABASE = $values['DB_URL']
Write-Output 'Local Supabase issuer and integration-test connection configured for this PowerShell process.'
