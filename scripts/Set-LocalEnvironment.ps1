[CmdletBinding()]
param(
    [string]$EnvironmentFile = (Join-Path $PSScriptRoot '..\.env')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedEnvironmentFile = [IO.Path]::GetFullPath($EnvironmentFile)
if (-not (Test-Path -LiteralPath $resolvedEnvironmentFile)) {
    throw "O arquivo '$resolvedEnvironmentFile' não existe. Execute Initialize-LocalEnvironment.ps1 primeiro."
}

$localValues = @{}
foreach ($line in Get-Content -LiteralPath $resolvedEnvironmentFile) {
    $trimmed = $line.Trim()
    if ($trimmed.Length -eq 0 -or $trimmed.StartsWith('#')) {
        continue
    }

    $separatorIndex = $trimmed.IndexOf('=')
    if ($separatorIndex -lt 1) {
        throw "Linha inválida no arquivo de ambiente: '$trimmed'."
    }

    $key = $trimmed.Substring(0, $separatorIndex)
    $value = $trimmed.Substring($separatorIndex + 1)
    $localValues[$key] = $value
}

foreach ($requiredKey in @('MSSQL_SA_PASSWORD', 'JWT_SIGNING_KEY')) {
    if (-not $localValues.ContainsKey($requiredKey) -or [string]::IsNullOrWhiteSpace($localValues[$requiredKey])) {
        throw "A variável obrigatória '$requiredKey' está ausente ou vazia."
    }
}

$database = if ($localValues.ContainsKey('STORECONTROL_DATABASE')) { $localValues['STORECONTROL_DATABASE'] } else { 'StoreControl' }
$sqlPort = if ($localValues.ContainsKey('SQLSERVER_PORT')) { $localValues['SQLSERVER_PORT'] } else { '1433' }
$sqlPassword = $localValues['MSSQL_SA_PASSWORD']
$applicationConnection = "Server=localhost,$sqlPort;Database=$database;User Id=sa;Password=$sqlPassword;Encrypt=False;TrustServerCertificate=True"
$testConnection = "Server=localhost,$sqlPort;Database=master;User Id=sa;Password=$sqlPassword;Encrypt=False;TrustServerCertificate=True"

[Environment]::SetEnvironmentVariable('ConnectionStrings__StoreControl', $applicationConnection, 'Process')
[Environment]::SetEnvironmentVariable('STORECONTROL_DB_CONNECTION', $applicationConnection, 'Process')
[Environment]::SetEnvironmentVariable('STORECONTROL_TEST_DB_CONNECTION', $testConnection, 'Process')
[Environment]::SetEnvironmentVariable('Authentication__Jwt__SigningKey', $localValues['JWT_SIGNING_KEY'], 'Process')
[Environment]::SetEnvironmentVariable('Authentication__RefreshToken__CookieSecure', 'false', 'Process')

$demoEnabled = if ($localValues.ContainsKey('SEED_DEMO_DATA')) { $localValues['SEED_DEMO_DATA'] } else { 'false' }
$demoAdminPassword = if ($localValues.ContainsKey('DEMO_ADMIN_PASSWORD')) { $localValues['DEMO_ADMIN_PASSWORD'] } else { '' }
$demoUserPassword = if ($localValues.ContainsKey('DEMO_USER_PASSWORD')) { $localValues['DEMO_USER_PASSWORD'] } else { '' }
[Environment]::SetEnvironmentVariable('Seed__DemoDataEnabled', $demoEnabled, 'Process')
[Environment]::SetEnvironmentVariable('Seed__AdminPassword', $demoAdminPassword, 'Process')
[Environment]::SetEnvironmentVariable('Seed__UserPassword', $demoUserPassword, 'Process')

Write-Host 'Variáveis locais do StoreControl carregadas no processo atual sem exibir segredos.'
