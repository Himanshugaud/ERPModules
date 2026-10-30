param(
    [ValidateSet('erpmodulessqldb-staging')]
    [string]$Database = 'erpmodulessqldb-staging',
    [switch]$Apply,
    [switch]$ConfigureLocal
)

$ErrorActionPreference = 'Stop'
$server = 'erpmodulesqlserver.database.windows.net'
$subscription = '69db84a9-2638-4ca3-b3cd-cdf9ee9796db'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$files = @(
    'erp-schema.sql',
    'inventory-schema.sql',
    'procurement-schema.sql',
    'workflow-schema.sql',
    'requirement-sheets-schema.sql',
    'item-taxonomy-schema.sql'
)
$plan = foreach ($name in $files) {
    $path = Join-Path $root "db/schema/$name"
    $text = Get-Content -Raw -LiteralPath $path
    if ($text -match '(?im)^\s*(USE\s|:(connect|r|setvar)\b)') {
        throw "Database-switching or external SQLCMD directives are forbidden: $name"
    }
    $batches = @([regex]::Split($text, '(?im)^\s*GO\s*$') | Where-Object { $_.Trim() })
    [pscustomobject]@{ Name = $name; Batches = $batches }
}

Write-Output "Target: $server / $Database"
foreach ($file in $plan) { Write-Output "$($file.Name): $($file.Batches.Count) batches" }
if (-not $Apply) {
    if ($ConfigureLocal) { throw 'ConfigureLocal requires Apply and a verified schema connection.' }
    Write-Output 'Dry run only. Use -Apply to initialize staging; add -ConfigureLocal to update the local API target.'
    return
}

Import-Module SqlServer -ErrorAction Stop
$token = az account get-access-token --subscription $subscription --resource https://database.windows.net/ --query accessToken --output tsv --only-show-errors
if ($LASTEXITCODE -ne 0 -or -not $token) { throw 'Could not obtain the existing Azure CLI SQL identity token.' }
$guard = "IF DB_NAME() <> N'$Database' THROW 51000, 'Refusing a non-staging database.', 1;"
$sql = @{
    ServerInstance = $server
    Database = $Database
    AccessToken = $token
    QueryTimeout = 180
    ConnectionTimeout = 20
    AbortOnError = $true
    ErrorAction = 'Stop'
}
$identity = Invoke-Sqlcmd @sql -Query "$guard SELECT DB_NAME() AS DatabaseName, HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CREATE TABLE') AS CanCreateTables;"
if ($identity.DatabaseName -ne $Database -or $identity.CanCreateTables -ne 1) {
    throw 'Staging identity verification or schema permissions failed.'
}
foreach ($file in $plan) {
    foreach ($batch in $file.Batches) {
        $statement = $batch.Replace("'", "''")
        Invoke-Sqlcmd @sql -Query "$guard`nEXEC sys.sp_executesql N'$statement';" | Out-Null
    }
    Write-Output "Applied $($file.Name) to staging."
}
$verification = Invoke-Sqlcmd @sql -Query "$guard SELECT DB_NAME() AS DatabaseName, COUNT(*) AS TableCount FROM sys.tables WHERE is_ms_shipped = 0;"
if ($verification.DatabaseName -ne $Database -or $verification.TableCount -lt 1) {
    throw 'Staging schema verification failed; local settings were not changed.'
}
Write-Output "Verified staging schema: $($verification.TableCount) user tables."

if ($ConfigureLocal) {
    $path = Join-Path $root 'backend/src/ERP.Api/local.settings.json'
    $legacyPath = Join-Path $root 'local.settings.json'
    foreach ($relative in @('local.settings.json', 'backend/src/ERP.Api/local.settings.json')) {
        git -C $root check-ignore --quiet -- $relative
        if ($LASTEXITCODE -ne 0) { throw "Refusing credential storage in a non-ignored file: $relative" }
    }
    $settings = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
    $legacy = Get-Content -Raw -LiteralPath $legacyPath | ConvertFrom-Json
    $connection = [System.Data.Common.DbConnectionStringBuilder]::new()
    $connection.set_ConnectionString([string]$settings.Values.ERP_SqlConnectionString)
    $hostValue = if ($connection.ContainsKey('Server')) { [string]$connection.get_Item('Server') } else { [string]$connection.get_Item('Data Source') }
    if (($hostValue -replace '^tcp:', '').Split(',')[0] -ne $server) {
        throw 'Unexpected local SQL server; refusing to rewrite local settings.'
    }
    if ($legacy.Values.SQL_SERVER -and $legacy.Values.SQL_SERVER -ne $server) {
        throw 'Unexpected legacy SQL server; refusing to rewrite local settings.'
    }
    $principal = 'erp_local_staging'
    $exists = Invoke-Sqlcmd @sql -Query "$guard SELECT COUNT(*) AS PrincipalCount FROM sys.database_principals WHERE name=N'$principal';"
    if ($exists.PrincipalCount -eq 0) {
        $bytes = [byte[]]::new(48)
        $random = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $random.GetBytes($bytes) } finally { $random.Dispose() }
        $password = 'Stg1!' + [Convert]::ToBase64String($bytes)
        $literal = $password.Replace("'", "''")
        Invoke-Sqlcmd @sql -Query "$guard CREATE USER [$principal] WITH PASSWORD=N'$literal';" | Out-Null
    } elseif ($connection.ContainsKey('User ID') -and $connection.get_Item('User ID') -eq $principal -and $connection.ContainsKey('Password')) {
        $password = [string]$connection.get_Item('Password')
    } else {
        throw 'Staging runtime user already exists but local credentials do not match; refusing automatic password rotation.'
    }
    $permissions = @"
$guard
IF DATABASE_PRINCIPAL_ID(N'erp_staging_runtime') IS NULL
    CREATE ROLE [erp_staging_runtime];
GRANT CONNECT TO [$principal];
GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::core TO [erp_staging_runtime];
GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::project TO [erp_staging_runtime];
GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::inventory TO [erp_staging_runtime];
GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::shared TO [erp_staging_runtime];
ALTER ROLE [erp_staging_runtime] ADD MEMBER [$principal];
"@
    Invoke-Sqlcmd @sql -Query $permissions | Out-Null
    if ($connection.ContainsKey('Initial Catalog')) { [void]$connection.Remove('Initial Catalog') }
    foreach ($key in @('Authentication', 'Integrated Security', 'Trusted_Connection', 'User ID', 'UID', 'Password', 'PWD')) {
        [void]$connection.Remove($key)
    }
    $connection.set_Item('Database', $Database)
    $connection.set_Item('User ID', $principal)
    $connection.set_Item('Password', $password)
    $connection.set_Item('Encrypt', $true)
    $connection.set_Item('TrustServerCertificate', $false)
    $runtime = Invoke-Sqlcmd -ConnectionString $connection.get_ConnectionString() -AbortOnError -ErrorAction Stop -QueryTimeout 30 -Query "$guard SELECT DB_NAME() AS DatabaseName, USER_NAME() AS PrincipalName, IS_MEMBER('db_owner') AS IsOwner, HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CREATE TABLE') AS CanCreateTables, HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'ALTER ANY USER') AS CanManageUsers;"
    if ($runtime.DatabaseName -ne $Database -or $runtime.PrincipalName -ne $principal -or $runtime.IsOwner -ne 0 -or $runtime.CanCreateTables -ne 0 -or $runtime.CanManageUsers -ne 0) {
        throw 'Least-privilege runtime verification failed; local settings were not changed.'
    }
    $settings.Values.ERP_SqlConnectionString = $connection.get_ConnectionString()
    $legacy.Values | Add-Member -NotePropertyName SQL_SERVER -NotePropertyValue $server -Force
    $legacy.Values | Add-Member -NotePropertyName SQL_DATABASE -NotePropertyValue $Database -Force
    $legacy.Values | Add-Member -NotePropertyName SQL_USER -NotePropertyValue $principal -Force
    $legacy.Values | Add-Member -NotePropertyName SQL_PASSWORD -NotePropertyValue $password -Force
    $json = $settings | ConvertTo-Json -Depth 100
    [IO.File]::WriteAllText($path, $json, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($legacyPath, ($legacy | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false))
    Write-Output 'Verified contained runtime access without db_owner, CREATE TABLE, or user-management rights.'
    Write-Output 'Both local SQL targets now use staging and its contained runtime identity. Credentials were not printed. Restart local hosts to reload.'
}