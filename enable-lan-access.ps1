param(
    [string]$InterfaceAlias = "Wi-Fi"
)

$ErrorActionPreference = "Stop"

Write-Host "Configuring Jordi ELN LAN access on interface: $InterfaceAlias"

$profile = Get-NetConnectionProfile -InterfaceAlias $InterfaceAlias
if ($profile.NetworkCategory -ne "Private") {
    Set-NetConnectionProfile -InterfaceAlias $InterfaceAlias -NetworkCategory Private
    Write-Host "Network profile changed to Private."
}
else {
    Write-Host "Network profile is already Private."
}

Enable-NetFirewallRule -DisplayGroup "Network Discovery" | Out-Null
Enable-NetFirewallRule -DisplayGroup "File and Printer Sharing" | Out-Null

Write-Host "Enabled firewall rules for Network Discovery and File and Printer Sharing."

$ipv4Addresses = [System.Net.Dns]::GetHostAddresses([System.Net.Dns]::GetHostName()) |
    Where-Object {
        $_.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork -and
        -not $_.IPAddressToString.StartsWith("127.") -and
        -not $_.IPAddressToString.StartsWith("172.30.")
    } |
    Select-Object -ExpandProperty IPAddressToString -Unique

Write-Host ""
Write-Host "Current IPv4 addresses:"
$ipv4Addresses | ForEach-Object { Write-Host " - $_" }
Write-Host ""
Write-Host "If Mac still cannot open Jordi ELN after this, the Wi-Fi/router is likely isolating devices from each other."
