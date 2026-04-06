$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptRoot

$env:DOTNET_CLI_HOME = Join-Path $scriptRoot ".dotnet-cli"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
New-Item -ItemType Directory -Force -Path $env:DOTNET_CLI_HOME | Out-Null

$escapedRoot = [Regex]::Escape($scriptRoot)
$runningAppProcesses = @()

try {
    $runningAppProcesses = Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" |
        Where-Object {
            $_.CommandLine -and
            $_.ProcessId -ne $PID -and
            $_.CommandLine -match $escapedRoot -and
            ($_.CommandLine -match 'ElectronicLabNotebook\.dll' -or $_.CommandLine -match 'dotnet run')
        }
}
catch {
    Write-Host "Skipping running-process scan because process metadata could not be queried."
}

foreach ($process in $runningAppProcesses) {
    Write-Host "Stopping previous app process $($process.ProcessId)..."
    Stop-Process -Id $process.ProcessId -Force
}

if ($runningAppProcesses) {
    Start-Sleep -Milliseconds 800
}

dotnet run --no-restore
