<#
  Builds the Linux version of TERMINAL HELL on Windows, ready to attach to a GitHub release.
  Needs the free .NET 10 SDK (https://dot.net) - not Visual Studio.

  Usage:  .\linux\build.ps1                      -> linux\dist\terminalhell-linux-x64 and terminalhell-linux-arm64
          .\linux\build.ps1 -Rid linux-musl-x64  -> other runtimes (Alpine uses the musl ones)

  The Linux installer downloads  terminalhell-<runtime>  from the latest GitHub release, so attach the files
  from linux\dist to a release with exactly those names.
#>
param([string[]]$Rid = @('linux-x64', 'linux-arm64'))
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'The .NET SDK was not found: install the .NET 10 SDK from https://dot.net' }
$project = Join-Path $PSScriptRoot 'TerminalHell.Linux.csproj'
$dist = Join-Path $PSScriptRoot 'dist'

foreach ($r in $Rid) {
    Write-Host "Building $r ..."
    & dotnet publish $project -c Release -r $r -o (Join-Path $dist $r) -nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed for $r (dotnet exit code $LASTEXITCODE)." }
    Copy-Item (Join-Path $dist "$r\terminalhell") (Join-Path $dist "terminalhell-$r") -Force
    Write-Host "Built linux\dist\terminalhell-$r" -ForegroundColor Green
}
