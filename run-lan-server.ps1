param(
    [int]$Port = 5055
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptRoot

$env:DOTNET_CLI_HOME = Join-Path $scriptRoot ".dotnet-cli"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:ASPNETCORE_ENVIRONMENT = "Development"
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

$addresses = [System.Net.Dns]::GetHostAddresses([System.Net.Dns]::GetHostName()) |
    Where-Object {
        $_.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork -and
        -not $_.IPAddressToString.StartsWith("127.")
    } |
    Select-Object -ExpandProperty IPAddressToString -Unique

Write-Host ""
Write-Host "Jordi ELN LAN server"
Write-Host "Listening on all interfaces: http://0.0.0.0:$Port"
foreach ($address in $addresses) {
    Write-Host "Open from other devices: http://$address`:$Port"
}
Write-Host "Health check: http://localhost:$Port/health"
Write-Host ""

dotnet run --no-restore --urls "http://0.0.0.0:$Port"
