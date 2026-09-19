<#
  Builds TERMINAL HELL from source using the C# compiler that ships with Windows
  (.NET Framework 4.x). No Visual Studio or .NET SDK required.

  Usage:  .\build.ps1            -> bin\TerminalHell.exe
          .\build.ps1 -Run       -> build and start the game
#>
param(
    [switch]$Run,
    [string]$Out = (Join-Path $PSScriptRoot 'bin\TerminalHell.exe')
)
$ErrorActionPreference = 'Stop'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw 'Could not find the .NET Framework 4 C# compiler (csc.exe).' }

$outDir = Split-Path $Out
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

$sources = Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter *.cs | ForEach-Object { $_.FullName }
$icon = Join-Path $PSScriptRoot 'assets\terminalhell.ico'
$cscArgs = @('/nologo', '/target:exe', '/optimize+', '/unsafe', '/platform:anycpu', '/codepage:65001', '/r:System.Drawing.dll', "/out:$Out")
if (Test-Path $icon) { $cscArgs += "/win32icon:$icon" }

& $csc @cscArgs @sources
if ($LASTEXITCODE -ne 0) { throw "Build failed (csc exit code $LASTEXITCODE)." }
Write-Host "Built $Out" -ForegroundColor Green

if ($Run) { & $Out }
