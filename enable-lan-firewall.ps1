param(
    [int]$Port = 5055
)

$ErrorActionPreference = "Stop"

$ruleName = "Jordi ELN LAN $Port"

if (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue) {
    Write-Host "Firewall rule already exists: $ruleName"
    return
}

New-NetFirewallRule `
    -DisplayName $ruleName `
    -Direction Inbound `
    -Action Allow `
    -Protocol TCP `
    -LocalPort $Port | Out-Null

Write-Host "Firewall rule created: $ruleName"
