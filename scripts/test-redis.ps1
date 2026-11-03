param(
    [string]$RedisImage = 'redis:7.4-alpine',
    [string]$PostgresBin = 'C:/Program Files/PostgreSQL/18/bin',
    [switch]$AllTests
)

$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$containerName = 'logistics-redis-tests-' + [guid]::NewGuid().ToString('N')
$previousRedis = $env:LOGISTICS_TEST_REDIS
$started = $false
Push-Location $repoPath
try {
    & docker info --format '{{.ServerVersion}}'
    if ($LASTEXITCODE -ne 0) { throw 'Start Docker Desktop before running Redis tests.' }

    # Docker chooses a free loopback port. No application Redis or host volume is used.
    & docker run --detach --rm --name $containerName --publish '127.0.0.1::6379' $RedisImage
    if ($LASTEXITCODE -ne 0) { throw 'Could not start isolated Redis.' }
    $started = $true

    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $pong = & docker exec $containerName redis-cli ping 2>$null
        if ($LASTEXITCODE -eq 0 -and $pong -eq 'PONG') { $ready = $true; break }
        Start-Sleep -Milliseconds 500
    }
    if (!$ready) { throw 'Test Redis did not become ready.' }

    $binding = & docker port $containerName 6379/tcp
    if ($LASTEXITCODE -ne 0 -or $binding -notmatch '^127\.0\.0\.1:(\d+)$') {
        throw 'Could not determine the isolated Redis port.'
    }
    $env:LOGISTICS_TEST_REDIS = "127.0.0.1:$($Matches[1]),abortConnect=false,connectTimeout=5000"

    if ($AllTests) {
        & (Join-Path $PSScriptRoot 'test-logistics-points.ps1') -PostgresBin $PostgresBin -AllTests
    } else {
        & dotnet test Logistics_Assistant.slnx --filter 'Feature=Redis' --verbosity minimal
        if ($LASTEXITCODE -ne 0) { throw 'Redis tests failed; see test output above.' }
    }
} finally {
    $env:LOGISTICS_TEST_REDIS = $previousRedis
    if ($started) {
        & docker stop --timeout 5 $containerName
        if ($LASTEXITCODE -ne 0) { Write-Warning "Could not stop test container $containerName" }
    }
    Pop-Location
}
