[CmdletBinding()]
param(
    [ValidateRange(1, 65535)]
    [int]$Port = 2026,

    [ValidatePattern('^(?:\d{1,3}\.){3}\d{1,3}$')]
    [string]$StationAddress = '192.168.172.173',

    [switch]$WaitForHeartbeat,

    [switch]$NoPause
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

function Write-Section {
    param([string]$Title)

    Write-Host ''
    Write-Host "===== $Title =====" -ForegroundColor Cyan
}

function Test-TcpEndpoint {
    param(
        [string]$Address,
        [int]$TargetPort,
        [int]$TimeoutMs = 2000
    )

    $client = New-Object Net.Sockets.TcpClient
    try {
        $asyncResult = $client.BeginConnect($Address, $TargetPort, $null, $null)
        if (-not $asyncResult.AsyncWaitHandle.WaitOne($TimeoutMs)) {
            return 'TIMEOUT'
        }

        $client.EndConnect($asyncResult)
        return 'OPEN'
    }
    catch {
        return "FAILED: $($_.Exception.GetBaseException().Message)"
    }
    finally {
        $client.Dispose()
    }
}

$desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop)
if (-not $desktop -or -not (Test-Path -LiteralPath $desktop)) {
    $desktop = $env:TEMP
}

$reportPath = Join-Path $desktop (
    'PTL-MQTT-Diagnostics-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.txt')

try {
    Start-Transcript -LiteralPath $reportPath -Force | Out-Null

    Write-Host 'PTL MQTT diagnostics' -ForegroundColor Green
    Write-Host "Report: $reportPath"
    Write-Host "Computer: $([Environment]::MachineName)"
    Write-Host "User: $env:USERDOMAIN\$env:USERNAME"
    Write-Host "Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"

    Write-Section 'Mosquitto service'
    Get-Service -Name mosquitto -ErrorAction SilentlyContinue |
        Format-List Name, DisplayName, Status, StartType
    & "$env:SystemRoot\System32\sc.exe" qc mosquitto

    Write-Section 'TCP listeners'
    netstat -ano | Select-String ":$Port\s+.*LISTENING"
    Write-Host "127.0.0.1:$Port = $(Test-TcpEndpoint '127.0.0.1' $Port)"
    Write-Host "192.168.172.172:$Port = $(Test-TcpEndpoint '192.168.172.172' $Port)"

    Write-Section 'Windows Firewall'
    netsh advfirewall firewall show rule name="PTL MQTT Broker TCP $Port" verbose

    Write-Section 'IPv4 configuration'
    ipconfig
    route print -4

    Write-Section 'eStation network'
    ping -n 2 -w 1000 $StationAddress
    arp -a | Select-String '192.168.172.'

    Write-Section 'Mosquitto configuration'
    $mosquittoConfig = Join-Path $env:ProgramFiles 'mosquitto\mosquitto.conf'
    if (Test-Path -LiteralPath $mosquittoConfig) {
        Get-Content -LiteralPath $mosquittoConfig
    }
    else {
        Write-Host "NOT FOUND: $mosquittoConfig" -ForegroundColor Red
    }

    Write-Section 'Mosquitto log'
    $mosquittoLog = Join-Path $env:ProgramData 'PTLControl\mosquitto\mosquitto.log'
    $detectedStationIds = @()
    if (Test-Path -LiteralPath $mosquittoLog) {
        try {
            $brokerLogLines = Get-Content -LiteralPath $mosquittoLog `
                -Encoding UTF8 -Tail 200 -ErrorAction Stop
            $brokerLogLines
            $detectedStationIds = @($brokerLogLines |
                ForEach-Object {
                    if ($_ -match 'as ([0-9A-Fa-f]{12}) \(p') {
                        $Matches[1].ToUpperInvariant()
                    }
                } |
                Sort-Object -Unique)
        }
        catch {
            Write-Host "LOG READ FAILED: $($_.Exception.Message)" -ForegroundColor Red
            Write-Host 'Run Configure-Mosquitto.ps1 again to repair log permissions.'
        }
    }
    else {
        Write-Host "NOT FOUND: $mosquittoLog" -ForegroundColor Red
    }

    Write-Section 'PTLControl startup configuration'
    $ptlRoot = Join-Path $env:APPDATA 'PTLControl'
    $startupConfig = Join-Path $ptlRoot 'startup_config.json'
    $configObject = $null
    if (Test-Path -LiteralPath $startupConfig) {
        try {
            $configObject = Get-Content -LiteralPath $startupConfig `
                -Encoding UTF8 -Raw |
                ConvertFrom-Json
            if ($null -ne $configObject.mqtt -and
                $null -ne $configObject.mqtt.password -and
                -not [string]::IsNullOrEmpty([string]$configObject.mqtt.password)) {
                $configObject.mqtt.password = '<redacted>'
            }
            $configObject | ConvertTo-Json -Depth 10
        }
        catch {
            Write-Host "CONFIG READ FAILED: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    else {
        Write-Host "NOT FOUND: $startupConfig" -ForegroundColor Red
    }

    Write-Section 'Latest PTLControl log'
    $latestAppLog = Get-ChildItem -LiteralPath (Join-Path $ptlRoot 'logs') `
        -Filter '*.log' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -ne $latestAppLog) {
        Write-Host "File: $($latestAppLog.FullName)"
        Get-Content -LiteralPath $latestAppLog.FullName -Encoding UTF8 -Tail 200
    }
    else {
        Write-Host 'No PTLControl log was found for the current Windows user.' -ForegroundColor Red
    }

    Write-Section 'Local MQTT protocol test'
    $mosquittoRoot = Join-Path $env:ProgramFiles 'mosquitto'
    $subExe = Join-Path $mosquittoRoot 'mosquitto_sub.exe'
    $pubExe = Join-Path $mosquittoRoot 'mosquitto_pub.exe'
    if ((Test-Path -LiteralPath $subExe) -and (Test-Path -LiteralPath $pubExe)) {
        $testOutput = Join-Path $env:TEMP (
            'ptl-mqtt-test-' + [Guid]::NewGuid().ToString('N') + '.txt')
        $subscriber = Start-Process -FilePath $subExe `
            -ArgumentList '-h', '127.0.0.1', '-p', $Port,
                '-t', '_ptl/diagnostics/test', '-C', '1', '-W', '5' `
            -RedirectStandardOutput $testOutput `
            -PassThru `
            -WindowStyle Hidden
        Start-Sleep -Milliseconds 500
        & $pubExe -h 127.0.0.1 -p $Port `
            -t '_ptl/diagnostics/test' -m 'PTL_MQTT_OK'
        [void]$subscriber.WaitForExit(7000)
        if (-not $subscriber.HasExited) {
            $subscriber.Kill()
        }

        if (Test-Path -LiteralPath $testOutput) {
            $testMessage = (Get-Content -LiteralPath $testOutput -Raw).Trim()
            Remove-Item -LiteralPath $testOutput -Force -ErrorAction SilentlyContinue
        }
        else {
            $testMessage = ''
        }

        if ($testMessage -eq 'PTL_MQTT_OK') {
            Write-Host 'Local MQTT publish/subscribe: PASS' -ForegroundColor Green
        }
        else {
            Write-Host 'Local MQTT publish/subscribe: FAIL' -ForegroundColor Red
        }
    }
    else {
        Write-Host 'mosquitto_pub.exe or mosquitto_sub.exe was not found.' -ForegroundColor Red
    }

    Write-Section 'Diagnosis summary'
    if ($null -eq $configObject) {
        Write-Host 'PTLControl startup_config.json could not be loaded.' -ForegroundColor Red
    }
    else {
        $configuredMode = [string]$configObject.connectionMode
        $configuredBroker = [string]$configObject.mqtt.broker
        $configuredPort = [int]$configObject.mqtt.port
        $configuredStationId = ([string]$configObject.mqtt.eStationId).ToUpperInvariant()

        Write-Host "PTLControl mode: $configuredMode"
        Write-Host "PTLControl broker: $configuredBroker`:$configuredPort"
        Write-Host "Configured eStation ID: $configuredStationId"
        Write-Host "Detected eStation IDs: $($detectedStationIds -join ', ')"

        if ($configuredMode -ne 'mqtt') {
            Write-Host 'ERROR: connectionMode must be mqtt.' -ForegroundColor Red
        }
        if ($configuredPort -ne $Port) {
            Write-Host "ERROR: PTLControl port must be $Port." -ForegroundColor Red
        }
        if ($detectedStationIds.Count -gt 0 -and
            $detectedStationIds -notcontains $configuredStationId) {
            Write-Host 'ERROR: Configured eStation ID does not match the station connected to Mosquitto.' `
                -ForegroundColor Red
            Write-Host "Set mqtt.eStationId to: $($detectedStationIds[0])" `
                -ForegroundColor Yellow
        }
        elseif ($detectedStationIds.Count -eq 0) {
            Write-Host 'ERROR: No eStation connection was found in the Mosquitto log.' `
                -ForegroundColor Red
        }
    }

    if ($WaitForHeartbeat -and (Test-Path -LiteralPath $subExe)) {
        Write-Section 'eStation heartbeat'
        Write-Host 'Waiting up to 30 seconds for /estation/+/heartbeat ...'
        & $subExe -h 127.0.0.1 -p $Port `
            -t '/estation/+/heartbeat' -v -C 1 -W 30
        if ($LASTEXITCODE -ne 0) {
            Write-Host 'No eStation heartbeat was received.' -ForegroundColor Red
        }
    }
}
finally {
    try {
        Stop-Transcript | Out-Null
    }
    catch {
    }
}

Write-Host ''
Write-Host "Diagnostics completed. Send this report for analysis:" -ForegroundColor Green
Write-Host $reportPath

if (-not $NoPause) {
    [void](Read-Host 'Press Enter to close this window')
}
