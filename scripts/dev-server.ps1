# Starts the media server on the DEV profile and waits until it is actually serving.
#
# Launching the built exe without --dev silently runs the PRODUCTION profile: a different
# data directory, a different database, and a different certificate. The symptom is not
# obvious — clients fail TLS with "Hostname <ip>.<id>.srv.nomercy.tv not verified, DN:
# CN=*.<id>.nomercy.tv", because the dev profile registered the synthesized-DNS hostname
# while the production profile is serving the apex certificate. That reads like a server
# bug and is not one, so the flag is not optional here.
#
#   ./scripts/dev-server.ps1
#   ./scripts/dev-server.ps1 -Stop
#
# Stopping goes through /manage/stop, never a force-kill: killing the process orphans
# cloudflared and the other children it owns.

param(
    [switch]$Stop,
    [switch]$Release,
    [int]$ReadyTimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"
$health = "https://127.0.0.1:7626/health"

function Test-ServerUp {
    try { $null = Invoke-WebRequest -Uri $health -SkipCertificateCheck -TimeoutSec 5; return $true }
    catch { return $false }
}

if ($Stop) {
    if (-not (Test-ServerUp)) { Write-Host "Not running."; exit 0 }
    Invoke-WebRequest -Uri "https://127.0.0.1:7626/manage/stop" -Method Post -SkipCertificateCheck -TimeoutSec 30 | Out-Null
    Write-Host "Stop requested."
    exit 0
}

if (Test-ServerUp) {
    # A second instance fights the first for port 7626 and one of them exits, which looks
    # like the server "dying on its own" a few minutes in.
    Write-Host "Already serving on 7626 — not starting a second instance."
    exit 0
}

$config = if ($Release) { "Release" } else { "Debug" }
$dir = Join-Path $PSScriptRoot "../src/NoMercy.Service/bin/$config/net10.0"
$exe = Join-Path $dir "NoMercyMediaServer.exe"
if (-not (Test-Path $exe)) { throw "No build at $exe — run: dotnet build src/NoMercy.Service" }

Write-Host "Starting media server ($config, dev profile)..."
Start-Process -FilePath $exe -ArgumentList "--dev" -WorkingDirectory $dir -WindowStyle Minimized

$deadline = (Get-Date).AddSeconds($ReadyTimeoutSeconds)
while ((Get-Date) -lt $deadline) {
    if (Test-ServerUp) { Write-Host "Serving on 7626."; exit 0 }
    Start-Sleep -Seconds 3
}
throw "Server did not answer $health within ${ReadyTimeoutSeconds}s."
