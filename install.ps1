<#
    TERMINAL HELL - one-line installer

    PowerShell:
        irm https://raw.githubusercontent.com/GorkemSarac/TerminalHell/main/install.ps1 | iex

    Command Prompt (cmd.exe):
        powershell -NoProfile -ExecutionPolicy Bypass -Command "irm https://raw.githubusercontent.com/GorkemSarac/TerminalHell/main/install.ps1 | iex"

    What it does:
      1. downloads the source code of the repository below
      2. compiles it with the C# compiler that ships with Windows (.NET Framework 4.x) - nothing else to install
      3. installs to %LOCALAPPDATA%\Programs\TerminalHell and adds the "terminalhell" command to your PATH
      4. adds a "TERMINAL HELL" shortcut to the Start menu
    Running it again updates an existing installation.
#>
& {
    $ErrorActionPreference = 'Stop'
    $Repo = 'GorkemSarac/TerminalHell'      # GitHub "owner/repository" (change with tools\set-repo.ps1)
    $Branch = 'main'
    $InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\TerminalHell'

    function Say([string]$Text, [string]$Color = 'Gray') { Write-Host $Text -ForegroundColor $Color }

    Say ''
    Say '  _____ ___ ___ __  __ ___ _  _   _   _      _  _ ___ _    _' Red
    Say ' |_   _| __| _ \  \/  |_ _| \| | /_\ | |    | || | __| |  | |' Red
    Say '   | | | _||   / |\/| || || .` |/ _ \| |__  | __ | _|| |__| |__' DarkRed
    Say '   |_| |___|_|_\_|  |_|___|_|\_/_/ \_\____| |_||_|___|____|____|' DarkRed
    Say '   a first person shooter for your terminal' DarkYellow
    Say ''

    if ($env:OS -ne 'Windows_NT') { throw 'TERMINAL HELL runs on Windows only.' }

    # ---- the C# compiler that comes with Windows
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
    if (-not (Test-Path $csc)) {
        throw 'The .NET Framework 4 C# compiler (csc.exe) was not found. It is part of Windows 10/11; on older systems install the .NET Framework 4.8 runtime.'
    }

    # ---- source code: a local checkout (running .\install.ps1 from the repo) or a download from GitHub
    $tmp = $null
    if ($PSScriptRoot -and (Test-Path (Join-Path $PSScriptRoot 'src\Program.cs'))) {
        $srcRoot = $PSScriptRoot
        Say "Using local source code in $srcRoot"
    }
    else {
        if ($Repo -like 'YOUR_GITHUB_NAME/*') { throw 'This installer has not been configured yet: set $Repo (see tools\set-repo.ps1).' }
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        $tmp = Join-Path ([IO.Path]::GetTempPath()) ('terminalhell-' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $tmp | Out-Null
        $zip = Join-Path $tmp 'source.zip'
        Say "Downloading github.com/$Repo ..."
        $old = $ProgressPreference; $ProgressPreference = 'SilentlyContinue'
        try { Invoke-WebRequest -UseBasicParsing -Uri "https://github.com/$Repo/archive/refs/heads/$Branch.zip" -OutFile $zip }
        finally { $ProgressPreference = $old }
        Expand-Archive -Path $zip -DestinationPath $tmp -Force
        $srcRoot = Get-ChildItem -Path $tmp -Directory | Select-Object -First 1 -ExpandProperty FullName
    }

    # ---- compile
    Get-Process -Name TerminalHell -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    $exe = Join-Path $InstallDir 'TerminalHell.exe'
    $sources = Get-ChildItem -Path (Join-Path $srcRoot 'src') -Filter *.cs | ForEach-Object { $_.FullName }
    $cscArgs = @('/nologo', '/target:exe', '/optimize+', '/unsafe', '/platform:anycpu', '/codepage:65001', '/r:System.Drawing.dll', "/out:$exe")
    $icon = Join-Path $srcRoot 'assets\terminalhell.ico'
    if (Test-Path $icon) { $cscArgs += "/win32icon:$icon" }
    Say 'Compiling (a few seconds)...'
    $output = & $csc @cscArgs @sources 2>&1
    if ($LASTEXITCODE -ne 0) { $output | ForEach-Object { Say "$_" Red }; throw 'Compilation failed.' }
    $uninstaller = Join-Path $srcRoot 'uninstall.ps1'
    if (Test-Path $uninstaller) { Copy-Item $uninstaller (Join-Path $InstallDir 'uninstall.ps1') -Force }

    # ---- "terminalhell" command for CMD and PowerShell
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    if (-not $userPath) { $userPath = '' }
    if (($userPath -split ';') -notcontains $InstallDir) {
        [Environment]::SetEnvironmentVariable('Path', (($userPath.TrimEnd(';') + ';' + $InstallDir).TrimStart(';')), 'User')
    }
    if (($env:Path -split ';') -notcontains $InstallDir) { $env:Path = $env:Path.TrimEnd(';') + ';' + $InstallDir }

    # ---- Start menu shortcut (opens in the classic console, which the game can size and style itself)
    try {
        $lnk = Join-Path ([Environment]::GetFolderPath('Programs')) 'TERMINAL HELL.lnk'
        $shell = New-Object -ComObject WScript.Shell
        $sc = $shell.CreateShortcut($lnk)
        $sc.TargetPath = Join-Path $env:WINDIR 'System32\conhost.exe'
        $sc.Arguments = '"' + $exe + '"'
        $sc.WorkingDirectory = $InstallDir
        $sc.IconLocation = "$exe,0"
        $sc.Description = 'TERMINAL HELL - a first person shooter for your terminal'
        $sc.Save()
    }
    catch { Say "Could not create the Start menu shortcut: $($_.Exception.Message)" DarkYellow }

    if ($tmp) { Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue }

    Say ''
    Say 'TERMINAL HELL is installed!' Green
    Say "  Play:       type  terminalhell  in PowerShell or CMD (open a new window if the command isn't found)"
    Say '              or start "TERMINAL HELL" from the Start menu'
    Say "  Uninstall:  powershell -ExecutionPolicy Bypass -File `"$InstallDir\uninstall.ps1`""
    Say ''

    $interactive = [Environment]::UserInteractive -and $Host.Name -eq 'ConsoleHost' -and -not $env:TERMINALHELL_NO_LAUNCH
    if ($interactive) {
        $answer = Read-Host 'Play now? [Y/n]'
        if ($answer -notmatch '^\s*[nN]') { & $exe }
    }
}
