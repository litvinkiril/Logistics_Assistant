param(
    [string]$PostgresBin = 'C:/Program Files/PostgreSQL/18/bin',
    [switch]$AllTests
)

$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$runPath = Join-Path $repoPath ('artifacts/points-tests-' + [guid]::NewGuid().ToString('N'))
$dataPath = Join-Path $runPath 'pgdata'
$pgCtl = Join-Path $PostgresBin 'pg_ctl.exe'
$initDb = Join-Path $PostgresBin 'initdb.exe'
if (!(Test-Path -LiteralPath $pgCtl) -or !(Test-Path -LiteralPath $initDb)) {
    throw 'PostgreSQL tools not found. Pass -PostgresBin with your PostgreSQL bin directory.'
}

New-Item -ItemType Directory -Path $runPath | Out-Null
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$testPort = $listener.LocalEndpoint.Port
$listener.Stop()
$previousConnection = $env:LOGISTICS_TEST_CONNECTION
$started = $false
Push-Location $repoPath
try {
    & $initDb -D $dataPath -U postgres --auth-local=trust --auth-host=trust --encoding=UTF8 --locale=C *> (Join-Path $runPath 'initdb.log')
    if ($LASTEXITCODE -ne 0) { throw "initdb failed; see $runPath/initdb.log" }
    & $pgCtl -D $dataPath -l (Join-Path $runPath 'postgres.log') -o "-h 127.0.0.1 -p $testPort" -w start
    if ($LASTEXITCODE -ne 0) { throw "PostgreSQL did not start; see $runPath/postgres.log" }
    $started = $true
    $env:LOGISTICS_TEST_CONNECTION = "Host=127.0.0.1;Port=$testPort;Database=postgres;Username=postgres;Pooling=false"
    if ($AllTests) {
        & dotnet test Logistics_Assistant.slnx --verbosity minimal
    } else {
        & dotnet test Logistics_Assistant.slnx --filter 'Feature=LogisticsPoints' --verbosity minimal
    }
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed; see test output above.' }
} finally {
    $env:LOGISTICS_TEST_CONNECTION = $previousConnection
    if ($started) {
        & $pgCtl -D $dataPath -m fast -w stop
        if ($LASTEXITCODE -ne 0) { Write-Warning "Could not stop test PostgreSQL at $dataPath" }
    }
    Pop-Location
    Write-Host "Test logs and stopped cluster: $runPath"
}
