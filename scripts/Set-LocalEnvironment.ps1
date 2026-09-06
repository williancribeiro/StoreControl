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

foreach ($requiredKey in @('MSSQL_SA_PASSWORD', 'JWT_SIGNING_KEY', 'REDIS_PASSWORD', 'RABBITMQ_DEFAULT_PASS')) {
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

$cachePort = if ($localValues.ContainsKey('REDIS_PORT')) { $localValues['REDIS_PORT'] } else { '6379' }
$cacheConnection = "127.0.0.1:$cachePort,password=$($localValues['REDIS_PASSWORD'])"
[Environment]::SetEnvironmentVariable('Cache__RedisConnection', $cacheConnection, 'Process')
[Environment]::SetEnvironmentVariable('STORECONTROL_TEST_REDIS_CONNECTION', $cacheConnection, 'Process')
$cacheSettings = @{
    'CACHE_ENABLED' = @('Cache__Enabled', 'true')
    'CACHE_KEY_PREFIX' = @('Cache__KeyPrefix', 'storecontrol:local')
    'CACHE_DASHBOARD_TTL_SECONDS' = @('Cache__DashboardTtlSeconds', '30')
    'CACHE_CATEGORIES_TTL_SECONDS' = @('Cache__CategoriesTtlSeconds', '300')
    'CACHE_TIMEOUT_MILLISECONDS' = @('Cache__TimeoutMilliseconds', '500')
}

$rabbitPort = if ($localValues.ContainsKey('RABBITMQ_PORT')) { $localValues['RABBITMQ_PORT'] } else { '5672' }
$rabbitUser = if ($localValues.ContainsKey('RABBITMQ_DEFAULT_USER')) { $localValues['RABBITMQ_DEFAULT_USER'] } else { 'storecontrol' }
$messagingEnabled = if ($localValues.ContainsKey('MESSAGING_ENABLED')) { $localValues['MESSAGING_ENABLED'] } else { 'true' }
$topologyPrefix = if ($localValues.ContainsKey('MESSAGING_TOPOLOGY_PREFIX')) { $localValues['MESSAGING_TOPOLOGY_PREFIX'] } else { 'storecontrol' }
[Environment]::SetEnvironmentVariable('Messaging__Enabled', $messagingEnabled, 'Process')
[Environment]::SetEnvironmentVariable('Messaging__HostName', '127.0.0.1', 'Process')
[Environment]::SetEnvironmentVariable('Messaging__Port', $rabbitPort, 'Process')
[Environment]::SetEnvironmentVariable('Messaging__UserName', $rabbitUser, 'Process')
[Environment]::SetEnvironmentVariable('Messaging__Password', $localValues['RABBITMQ_DEFAULT_PASS'], 'Process')
[Environment]::SetEnvironmentVariable('Messaging__VirtualHost', '/', 'Process')
[Environment]::SetEnvironmentVariable('Messaging__TopologyPrefix', $topologyPrefix, 'Process')
[Environment]::SetEnvironmentVariable('STORECONTROL_TEST_RABBITMQ_HOST', '127.0.0.1', 'Process')
[Environment]::SetEnvironmentVariable('STORECONTROL_TEST_RABBITMQ_PORT', $rabbitPort, 'Process')
[Environment]::SetEnvironmentVariable('STORECONTROL_TEST_RABBITMQ_USERNAME', $rabbitUser, 'Process')
[Environment]::SetEnvironmentVariable('STORECONTROL_TEST_RABBITMQ_PASSWORD', $localValues['RABBITMQ_DEFAULT_PASS'], 'Process')
[Environment]::SetEnvironmentVariable('STORECONTROL_TEST_RABBITMQ_VHOST', '/', 'Process')
foreach ($setting in $cacheSettings.GetEnumerator()) {
    $settingValue = if ($localValues.ContainsKey($setting.Key)) { $localValues[$setting.Key] } else { $setting.Value[1] }
    [Environment]::SetEnvironmentVariable($setting.Value[0], $settingValue, 'Process')
}

$demoEnabled = if ($localValues.ContainsKey('SEED_DEMO_DATA')) { $localValues['SEED_DEMO_DATA'] } else { 'false' }
$demoAdminPassword = if ($localValues.ContainsKey('DEMO_ADMIN_PASSWORD')) { $localValues['DEMO_ADMIN_PASSWORD'] } else { '' }
$demoUserPassword = if ($localValues.ContainsKey('DEMO_USER_PASSWORD')) { $localValues['DEMO_USER_PASSWORD'] } else { '' }
[Environment]::SetEnvironmentVariable('Seed__DemoDataEnabled', $demoEnabled, 'Process')
[Environment]::SetEnvironmentVariable('Seed__AdminPassword', $demoAdminPassword, 'Process')
[Environment]::SetEnvironmentVariable('Seed__UserPassword', $demoUserPassword, 'Process')

Write-Host 'Variáveis locais do StoreControl carregadas no processo atual sem exibir segredos.'
