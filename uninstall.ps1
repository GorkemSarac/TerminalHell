<#
    TERMINAL HELL - uninstaller

        powershell -ExecutionPolicy Bypass -File "%LOCALAPPDATA%\Programs\TerminalHell\uninstall.ps1"
    or
        irm https://raw.githubusercontent.com/GorkemSarac/TerminalHell/main/uninstall.ps1 | iex

    Removes the game, its PATH entry, the Start menu shortcut and the saved settings.
#>
& {
    $ErrorActionPreference = 'Stop'
    $InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\TerminalHell'
    $SettingsDir = Join-Path $env:LOCALAPPDATA 'TerminalHell'

    Get-Process -Name TerminalHell -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    if ($userPath) {
        $parts = $userPath -split ';' | Where-Object { $_ -and ($_.TrimEnd('\') -ne $InstallDir.TrimEnd('\')) }
        [Environment]::SetEnvironmentVariable('Path', ($parts -join ';'), 'User')
    }

    $lnk = Join-Path ([Environment]::GetFolderPath('Programs')) 'TERMINAL HELL.lnk'
    if (Test-Path $lnk) { Remove-Item $lnk -Force }
    if (Test-Path $SettingsDir) { Remove-Item $SettingsDir -Recurse -Force -ErrorAction SilentlyContinue }

    if (Test-Path $InstallDir) {
        # this script may be running from inside the install folder: delete it once we have exited
        $here = $PSCommandPath
        if ($here -and $here.StartsWith($InstallDir, [StringComparison]::OrdinalIgnoreCase)) {
            Start-Process -WindowStyle Hidden -FilePath cmd.exe -ArgumentList "/c timeout /t 2 /nobreak >nul & rmdir /s /q `"$InstallDir`""
        }
        else { Remove-Item $InstallDir -Recurse -Force }
    }
    Write-Host 'TERMINAL HELL has been uninstalled.' -ForegroundColor Green
}
