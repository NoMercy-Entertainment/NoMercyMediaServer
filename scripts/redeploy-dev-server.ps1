# Stops the dev server, rebuilds it, and starts it again.
#
# The three steps have to happen in that order and the middle one fails silently
# if the first has not finished: the running server holds NoMercyQueue.dll and
# the rest of its dependency graph, so a build started too early dies on
# MSB3027 after ten copy retries and leaves the old binary in place. Which looks
# exactly like a deploy that worked.
#
#   ./scripts/redeploy-dev-server.ps1
#   ./scripts/redeploy-dev-server.ps1 -Project src/NoMercy.Api/NoMercy.Api.csproj
#
# Stopping goes through dev-server.ps1, which uses /manage/stop rather than a
# force-kill, because killing the process orphans cloudflared and the other
# children it owns.

param(
    [string]$Project = "src/NoMercy.Service/NoMercy.Service.csproj",
    [int]$StopTimeoutSeconds = 300
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$devServer = Join-Path $PSScriptRoot "dev-server.ps1"

& $devServer -Stop

$deadline = (Get-Date).AddSeconds($StopTimeoutSeconds)
while (Get-Process -Name "NoMercyMediaServer" -ErrorAction SilentlyContinue) {
    if ((Get-Date) -gt $deadline) {
        throw "Server still running after ${StopTimeoutSeconds}s — not building over a locked binary."
    }
    Start-Sleep -Seconds 2
}
Write-Host "Stopped."

# A stale testhost from an interrupted `dotnet test` holds the same DLLs and
# produces the identical failure, so clear it before building rather than
# reading the error twice.
Get-Process -Name "testhost" -ErrorAction SilentlyContinue |
    Stop-Process -Force -Confirm:$false

Push-Location $root
try {
    dotnet build $Project --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Build failed — leaving the server stopped." }
}
finally {
    Pop-Location
}

& $devServer
