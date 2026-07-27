[CmdletBinding()]
param(
    [switch]$Restore,
    [switch]$NoPause
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

trap {
    Write-Host ''
    Write-Host 'Firewall operation failed:' -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    if (-not $NoPause) {
        [void](Read-Host 'Press Enter to close this window')
    }
    exit 1
}

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-TcpPort {
    param(
        [string]$Address,
        [int]$Port,
        [int]$TimeoutMs = 2000
    )

    $client = New-Object Net.Sockets.TcpClient
    try {
        $result = $client.BeginConnect($Address, $Port, $null, $null)
        if (-not $result.AsyncWaitHandle.WaitOne($TimeoutMs)) {
            return $false
        }
        $client.EndConnect($result)
        return $true
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

if (-not (Test-Administrator)) {
    Write-Host 'Administrator privileges are required. Requesting UAC elevation...' `
        -ForegroundColor Yellow

    $arguments = @(
        '-NoExit',
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', "`"$PSCommandPath`""
    )
    if ($Restore) {
        $arguments += '-Restore'
    }
    if ($NoPause) {
        $arguments += '-NoPause'
    }

    Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $arguments
    exit
}

$stateRoot = Join-Path $env:ProgramData 'PTLControl'
$statePath = Join-Path $stateRoot 'firewall-state.json'
New-Item -ItemType Directory -Path $stateRoot -Force | Out-Null

if ($Restore) {
    Write-Host 'Restoring Windows Defender Firewall...' -ForegroundColor Cyan

    if (Test-Path -LiteralPath $statePath) {
        $savedProfiles = Get-Content -LiteralPath $statePath -Encoding UTF8 -Raw |
            ConvertFrom-Json
        foreach ($profile in @($savedProfiles)) {
            Set-NetFirewallProfile `
                -Profile ([string]$profile.Name) `
                -Enabled ([bool]$profile.Enabled)
        }
        Write-Host "Firewall state restored from: $statePath" -ForegroundColor Green
    }
    else {
        Set-NetFirewallProfile -Profile Domain,Private,Public -Enabled True
        Write-Host 'No saved state was found. All profiles were enabled.' `
            -ForegroundColor Yellow
    }
}
else {
    Write-Host 'Disabling Windows Defender Firewall for all profiles...' `
        -ForegroundColor Yellow

    if (-not (Test-Path -LiteralPath $statePath)) {
        Get-NetFirewallProfile |
            Select-Object Name,Enabled |
            ConvertTo-Json |
            Set-Content -LiteralPath $statePath -Encoding UTF8
        Write-Host "Original firewall state saved to: $statePath"
    }
    else {
        Write-Host "Existing firewall backup kept: $statePath"
    }

    Set-NetFirewallProfile -Profile Domain,Private,Public -Enabled False
    Write-Host 'All Windows Defender Firewall profiles are disabled.' `
        -ForegroundColor Green
    Write-Host 'Keep this computer physically isolated from external networks.' `
        -ForegroundColor Yellow
}

$mosquitto = Get-Service -Name mosquitto -ErrorAction SilentlyContinue
if ($null -ne $mosquitto) {
    if ($mosquitto.Status -eq 'Running') {
        Restart-Service -Name mosquitto -Force
    }
    else {
        Start-Service -Name mosquitto
    }

    $mosquitto = Get-Service -Name mosquitto
    $mosquitto.WaitForStatus('Running', [TimeSpan]::FromSeconds(15))
}

Write-Host ''
Write-Host 'Current firewall state:' -ForegroundColor Cyan
Get-NetFirewallProfile |
    Select-Object Name,Enabled |
    Format-Table -AutoSize

$finalMosquitto = Get-Service -Name mosquitto -ErrorAction SilentlyContinue
$mosquittoStatus = if ($null -eq $finalMosquitto) {
    'NOT INSTALLED'
}
else {
    [string]$finalMosquitto.Status
}
Write-Host "Mosquitto service: $mosquittoStatus"
Write-Host "127.0.0.1:2026 open: $(Test-TcpPort '127.0.0.1' 2026)"
Write-Host "192.168.172.172:2026 open: $(Test-TcpPort '192.168.172.172' 2026)"

if (-not $NoPause) {
    Write-Host ''
    [void](Read-Host 'Press Enter to close this window')
}
