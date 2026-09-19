<#
    Points the installer, uninstaller and README at your GitHub repository.

    Usage:  .\tools\set-repo.ps1 -Repo yourname/TerminalHell
#>
param([Parameter(Mandatory = $true)][ValidatePattern('^[\w.-]+/[\w.-]+$')][string]$Repo)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$install = Join-Path $root 'install.ps1'
$current = (Select-String -Path $install -Pattern "^\s*\`$Repo = '([^']+)'").Matches[0].Groups[1].Value
if (-not $current) { throw 'Could not find the current repository in install.ps1' }
$utf8 = New-Object Text.UTF8Encoding $false
foreach ($name in 'install.ps1', 'uninstall.ps1', 'README.md') {
    $file = Join-Path $root $name
    if (-not (Test-Path $file)) { continue }
    $text = [IO.File]::ReadAllText($file)
    $new = $text.Replace($current, $Repo)
    if ($new -ne $text) { [IO.File]::WriteAllText($file, $new, $utf8); Write-Host "updated $name" }
}
Write-Host "Repository set to $Repo" -ForegroundColor Green
Write-Host "One-line install:  irm https://raw.githubusercontent.com/$Repo/main/install.ps1 | iex"
