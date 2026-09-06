[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\.env'),
    [switch]$EnableDemoData,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-StoreControlSecret {
    param(
        [Parameter(Mandatory)]
        [string]$Prefix,
        [int]$ByteCount = 32
    )

    $secretBytes = [byte[]]::new($ByteCount)
    $random = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $random.GetBytes($secretBytes)
    }
    finally {
        $random.Dispose()
    }

    $suffix = [Convert]::ToBase64String($secretBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
    return "$Prefix$suffix"
}

$resolvedOutputPath = [IO.Path]::GetFullPath($OutputPath)
if ([IO.Path]::GetFileName($resolvedOutputPath).Equals('.env.example', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'O gerador se recusa a gravar segredos no arquivo versionado .env.example.'
}

if ((Test-Path -LiteralPath $resolvedOutputPath) -and -not $Force) {
    throw "O arquivo '$resolvedOutputPath' já existe. Use -Force somente se pretende substituir todos os segredos locais."
}

$outputDirectory = Split-Path -Parent $resolvedOutputPath
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

$demoEnabled = if ($EnableDemoData) { 'true' } else { 'false' }
$lines = @(
    '# Gerado localmente. Não versione nem compartilhe este arquivo.'
    "MSSQL_SA_PASSWORD=$(New-StoreControlSecret -Prefix 'ScSql!9_')"
    'STORECONTROL_DATABASE=StoreControl'
    'RABBITMQ_DEFAULT_USER=storecontrol'
    "RABBITMQ_DEFAULT_PASS=$(New-StoreControlSecret -Prefix 'ScRabbit!9_')"
    'MESSAGING_ENABLED=true'
    'MESSAGING_TOPOLOGY_PREFIX=storecontrol'
    "REDIS_PASSWORD=$(New-StoreControlSecret -Prefix 'ScRedis!9_')"
    'CACHE_ENABLED=true'
    'CACHE_KEY_PREFIX=storecontrol:local'
    'CACHE_DASHBOARD_TTL_SECONDS=30'
    'CACHE_CATEGORIES_TTL_SECONDS=300'
    'CACHE_TIMEOUT_MILLISECONDS=500'
    "JWT_SIGNING_KEY=$(New-StoreControlSecret -Prefix 'ScJwt!9_' -ByteCount 48)"
    ''
    "SEED_DEMO_DATA=$demoEnabled"
    "DEMO_ADMIN_PASSWORD=$(New-StoreControlSecret -Prefix 'ScAdmin!9_')"
    "DEMO_USER_PASSWORD=$(New-StoreControlSecret -Prefix 'ScUser!9_')"
    ''
    'SQLSERVER_PORT=1433'
    'REDIS_PORT=6379'
    'RABBITMQ_PORT=5672'
    'RABBITMQ_MANAGEMENT_PORT=15672'
    'API_PORT=5090'
    'WEB_PORT=5173'
)

[IO.File]::WriteAllLines($resolvedOutputPath, $lines, [Text.UTF8Encoding]::new($false))
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    [IO.File]::SetUnixFileMode(
        $resolvedOutputPath,
        [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite)
}

Write-Host "Configuração local criada em '$resolvedOutputPath'."
Write-Host 'Os segredos não foram exibidos. Consulte apenas o arquivo local quando precisar das credenciais demonstrativas.'
