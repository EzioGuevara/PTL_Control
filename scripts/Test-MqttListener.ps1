[CmdletBinding()]
param(
    [ValidateRange(1, 65535)]
    [int]$Port = 2026,

    [ValidatePattern('^(?:\d{1,3}\.){3}\d{1,3}$')]
    [string]$ListenAddress = '192.168.172.172',

    [switch]$NoPause
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

function Write-Section {
    param([string]$Title)

    Write-Host ''
    Write-Host "===== $Title =====" -ForegroundColor Cyan
}

function Test-TcpPort {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Address,

        [Parameter(Mandatory = $true)]
        [int]$TargetPort,

        [int]$TimeoutMs = 3000
    )

    $client = New-Object Net.Sockets.TcpClient
    try {
        $result = $client.BeginConnect($Address, $TargetPort, $null, $null)
        if (-not $result.AsyncWaitHandle.WaitOne($TimeoutMs)) {
            return [PSCustomObject]@{
                Success = $false
                Detail = 'TIMEOUT'
            }
        }

        $client.EndConnect($result)
        return [PSCustomObject]@{
            Success = $true
            Detail = 'OPEN'
        }
    }
    catch {
        return [PSCustomObject]@{
            Success = $false
            Detail = $_.Exception.GetBaseException().Message
        }
    }
    finally {
        $client.Dispose()
    }
}

function Write-TestResult {
    param(
        [string]$Name,
        [object]$Result
    )

    $color = if ($Result.Success) { 'Green' } else { 'Red' }
    Write-Host "$Name = $($Result.Success) ($($Result.Detail))" `
        -ForegroundColor $color
}

$desktop = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::Desktop)
if (-not $desktop -or -not (Test-Path -LiteralPath $desktop)) {
    $desktop = $env:TEMP
}

$reportPath = Join-Path $desktop (
    'PTL-MQTT-Listener-Test-' +
    (Get-Date -Format 'yyyyMMdd-HHmmss') +
    '.txt')

$transcriptStarted = $false

try {
    Start-Transcript -LiteralPath $reportPath -Force | Out-Null
    $transcriptStarted = $true

    Write-Host 'PTL MQTT listener read-only diagnostics' `
        -ForegroundColor Green
    Write-Host 'This script does not modify Mosquitto, services, or firewall rules.'
    Write-Host "Computer: $([Environment]::MachineName)"
    Write-Host "Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"
    Write-Host "Target: $ListenAddress`:$Port"
    Write-Host "Report: $reportPath"

    Write-Section 'IPv4 address'
    $assignedAddress = Get-NetIPAddress `
        -AddressFamily IPv4 `
        -IPAddress $ListenAddress `
        -ErrorAction SilentlyContinue

    if ($null -eq $assignedAddress) {
        Write-Host "$ListenAddress is NOT assigned to this computer." `
            -ForegroundColor Red
    }
    else {
        $assignedAddress |
            Format-List IPAddress, PrefixLength, InterfaceAlias,
                AddressState
    }

    Write-Section 'Mosquitto service'
    $service = Get-Service -Name mosquitto -ErrorAction SilentlyContinue
    if ($null -eq $service) {
        Write-Host 'Mosquitto service was not found.' -ForegroundColor Red
    }
    else {
        $service | Format-List Name, Status, StartType
        & "$env:SystemRoot\System32\sc.exe" qc mosquitto
    }

    Write-Section 'Actual TCP listeners'
    $listeners = @(Get-NetTCPConnection `
        -State Listen `
        -LocalPort $Port `
        -ErrorAction SilentlyContinue)

    if ($listeners.Count -eq 0) {
        Write-Host "No TCP listener was found on port $Port." `
            -ForegroundColor Red
    }
    else {
        $listeners |
            Select-Object LocalAddress, LocalPort, OwningProcess |
            Format-Table -AutoSize

        $processIds = @($listeners |
            Select-Object -ExpandProperty OwningProcess -Unique)
        foreach ($processId in $processIds) {
            Get-Process -Id $processId -ErrorAction SilentlyContinue |
                Select-Object Id, ProcessName, Path |
                Format-List
        }
    }

    Write-Section 'TCP connection tests'
    $loopbackResult = Test-TcpPort `
        -Address '127.0.0.1' `
        -TargetPort $Port
    $nicResult = Test-TcpPort `
        -Address $ListenAddress `
        -TargetPort $Port

    Write-TestResult "127.0.0.1`:$Port" $loopbackResult
    Write-TestResult "$ListenAddress`:$Port" $nicResult

    Write-Section 'Matching Windows Firewall rules'
    $firewallRules = @(Get-NetFirewallRule `
        -Enabled True `
        -Direction Inbound `
        -ErrorAction SilentlyContinue |
        Where-Object {
            $portFilter = $_ | Get-NetFirewallPortFilter
            $portFilter.Protocol -eq 'TCP' -and
            ($portFilter.LocalPort -contains [string]$Port -or
                $portFilter.LocalPort -eq 'Any')
        })

    if ($firewallRules.Count -eq 0) {
        Write-Host "No enabled inbound firewall rule matched TCP $Port." `
            -ForegroundColor Yellow
    }
    else {
        foreach ($rule in $firewallRules) {
            $portFilter = $rule | Get-NetFirewallPortFilter
            $addressFilter = $rule | Get-NetFirewallAddressFilter
            [PSCustomObject]@{
                DisplayName = $rule.DisplayName
                Action = $rule.Action
                Profile = $rule.Profile
                LocalPort = $portFilter.LocalPort -join ','
                LocalAddress = $addressFilter.LocalAddress -join ','
                RemoteAddress = $addressFilter.RemoteAddress -join ','
                PolicySource = $rule.PolicyStoreSource
            } | Format-List
        }
    }

    Write-Section 'Mosquitto configuration (read only)'
    $configCandidates = @(
        (Join-Path $env:ProgramFiles 'mosquitto\mosquitto.conf'),
        (Join-Path ${env:ProgramFiles(x86)} 'mosquitto\mosquitto.conf')
    )
    $configPath = $configCandidates |
        Where-Object { $_ -and (Test-Path -LiteralPath $_) } |
        Select-Object -First 1

    if ($configPath) {
        Write-Host "Configuration: $configPath"
        Get-Content -LiteralPath $configPath |
            Select-String -Pattern (
                '^\s*(listener|bind_address|allow_anonymous)\b')
    }
    else {
        Write-Host 'mosquitto.conf was not found.' -ForegroundColor Yellow
    }

    Write-Section 'Diagnosis'
    $listenerAddresses = @($listeners |
        Select-Object -ExpandProperty LocalAddress -Unique)
    $externalListener = (
        $listenerAddresses -contains '0.0.0.0' -or
        $listenerAddresses -contains $ListenAddress)

    if ($listeners.Count -eq 0) {
        Write-Host 'RESULT: Mosquitto is not listening on the requested port.' `
            -ForegroundColor Red
    }
    elseif (-not $externalListener) {
        Write-Host (
            'RESULT: The port is listening, but only on another address. ' +
            'If it is 127.0.0.1, the external listener configuration was not loaded.'
        ) -ForegroundColor Red
    }
    elseif (-not $loopbackResult.Success) {
        Write-Host 'RESULT: The local listener cannot accept TCP connections.' `
            -ForegroundColor Red
    }
    elseif (-not $nicResult.Success) {
        Write-Host (
            'RESULT: Loopback works and an external listener exists, but the NIC ' +
            'address is blocked. Check firewall address scope, WFP, IPsec, or EDR.'
        ) -ForegroundColor Red
    }
    else {
        Write-Host (
            'RESULT: Local Mosquitto listening and NIC-address TCP tests passed. ' +
            'Run Test-NetConnection from another device to verify external ingress.'
        ) -ForegroundColor Green
        Write-Host (
            "External command: Test-NetConnection $ListenAddress -Port $Port")
    }
}
finally {
    if ($transcriptStarted) {
        try {
            Stop-Transcript | Out-Null
        }
        catch {
        }
    }
}

Write-Host ''
Write-Host "Diagnostics completed: $reportPath" -ForegroundColor Green

if (-not $NoPause) {
    [void](Read-Host 'Press Enter to close this window')
}
