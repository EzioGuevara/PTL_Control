[CmdletBinding()]
param(
    [ValidateRange(1, 65535)]
    [int]$Port = 2026,

    [ValidatePattern('^(?:\d{1,3}\.){3}\d{1,3}$')]
    [string]$ListenAddress = '192.168.172.172',

    [string]$AllowedRemoteAddress = '192.168.172.173',

    [switch]$NoPause
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

trap {
    Write-Host ''
    Write-Host 'Mosquitto deployment failed:' -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    if (-not $NoPause) {
        [void](Read-Host 'Press Enter to close this window')
    }
    exit 1
}

function Write-Step {
    param([string]$Message)
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Administrator)) {
    Write-Host 'Administrator privileges are required. Requesting UAC elevation...' -ForegroundColor Yellow

    $elevationArguments = @(
        '-NoExit',
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', "`"$PSCommandPath`"",
        '-Port', $Port,
        '-ListenAddress', $ListenAddress,
        '-AllowedRemoteAddress', $AllowedRemoteAddress
    )
    if ($NoPause) {
        $elevationArguments += '-NoPause'
    }

    Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $elevationArguments
    exit
}

Write-Step 'Checking Mosquitto'

$mosquittoCandidates = @(
    (Join-Path $env:ProgramFiles 'mosquitto\mosquitto.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'mosquitto\mosquitto.exe')
)

$mosquittoCommand = Get-Command 'mosquitto.exe' -ErrorAction SilentlyContinue
if ($null -ne $mosquittoCommand) {
    $mosquittoCandidates += $mosquittoCommand.Source
}

$mosquittoExe = $mosquittoCandidates |
    Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } |
    Select-Object -First 1

if (-not $mosquittoExe) {
    throw 'mosquitto.exe was not found. Install Mosquitto x64 and run this script again.'
}

$assignedAddress = Get-NetIPAddress -AddressFamily IPv4 -IPAddress $ListenAddress `
    -ErrorAction SilentlyContinue
if (-not $assignedAddress) {
    throw "IP $ListenAddress is not assigned to this computer. Configure the eStation NIC as $ListenAddress/24 first."
}

Write-Host "Mosquitto: $mosquittoExe"
Write-Host "Listener: $ListenAddress`:$Port"
Write-Host "Allowed remote address: $AllowedRemoteAddress"

Write-Step 'Writing Mosquitto configuration'

$mosquittoRoot = Split-Path -Parent $mosquittoExe
$runtimeRoot = Join-Path $env:ProgramData 'PTLControl\mosquitto'
$dataRoot = Join-Path $runtimeRoot 'data'
$configPath = Join-Path $mosquittoRoot 'mosquitto.conf'
$configBackupPath = Join-Path $mosquittoRoot 'mosquitto.conf.before-ptl'
$logPath = Join-Path $runtimeRoot 'mosquitto.log'

New-Item -ItemType Directory -Path $runtimeRoot -Force | Out-Null
New-Item -ItemType Directory -Path $dataRoot -Force | Out-Null
if ((Test-Path -LiteralPath $configPath) -and
    -not (Test-Path -LiteralPath $configBackupPath)) {
    Copy-Item -LiteralPath $configPath -Destination $configBackupPath
}

$dataRootForConfig = $dataRoot.Replace('\', '/') + '/'
$logPathForConfig = $logPath.Replace('\', '/')
$configContent = @"
# PTL Control - Mosquitto configuration
# Anonymous access is intended for a physically isolated PTL network.

listener $Port 0.0.0.0
allow_anonymous true

persistence true
persistence_location $dataRootForConfig
autosave_interval 60

connection_messages true
log_dest file $logPathForConfig
log_type error
log_type warning
log_type notice
log_type information
log_timestamp true
"@

$utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText($configPath, $configContent, $utf8WithoutBom)
Write-Host "Configuration: $configPath"

& $mosquittoExe --test-config -c $configPath
if ($LASTEXITCODE -ne 0) {
    throw "Mosquitto rejected the generated configuration (exit code $LASTEXITCODE)."
}

Write-Step 'Configuring the Windows service'

$serviceName = 'mosquitto'
$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($null -ne $service -and $service.Status -ne 'Stopped') {
    Stop-Service -Name $serviceName -Force
    $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(15))
}

$binaryPath = "`"$mosquittoExe`" run"
if ($null -eq $service) {
    & $mosquittoExe install
    if ($LASTEXITCODE -ne 0) {
        throw "Mosquitto service installation failed (exit code $LASTEXITCODE)."
    }
}

$serviceRegistryPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$serviceName"
Set-ItemProperty -LiteralPath $serviceRegistryPath -Name ImagePath -Value $binaryPath
Set-Service -Name $serviceName -StartupType Automatic

& "$env:SystemRoot\System32\sc.exe" failure $serviceName `
    reset= 86400 `
    actions= restart/5000/restart/10000/restart/30000 | Out-Host
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Service recovery options could not be configured (exit code $LASTEXITCODE)."
}

Write-Step 'Configuring Windows Firewall'

$firewallRuleName = "PTL MQTT Broker TCP $Port"
Get-NetFirewallRule -DisplayName $firewallRuleName -ErrorAction SilentlyContinue |
    Remove-NetFirewallRule

New-NetFirewallRule `
    -DisplayName $firewallRuleName `
    -Description "Allow eStation MQTT traffic to Mosquitto on TCP $Port." `
    -Direction Inbound `
    -Action Allow `
    -Protocol TCP `
    -LocalAddress $ListenAddress `
    -LocalPort $Port `
    -RemoteAddress $AllowedRemoteAddress `
    -Profile Any | Out-Null

Write-Host "Firewall rule: $firewallRuleName"

Write-Step 'Starting and verifying Mosquitto'

Start-Service -Name $serviceName
$runningService = Get-Service -Name $serviceName
$runningService.WaitForStatus('Running', [TimeSpan]::FromSeconds(15))

$deadline = [DateTime]::UtcNow.AddSeconds(10)
$loopbackReady = $false
do {
    $client = New-Object Net.Sockets.TcpClient
    try {
        $client.Connect('127.0.0.1', $Port)
        $loopbackReady = $true
    }
    catch {
        Start-Sleep -Milliseconds 500
    }
    finally {
        $client.Dispose()
    }
} while (-not $loopbackReady -and [DateTime]::UtcNow -lt $deadline)

if (-not $loopbackReady) {
    Write-Host 'Mosquitto did not open the configured port. Check the log:' -ForegroundColor Red
    Write-Host $logPath
    throw "TCP port $Port verification failed."
}

$nicClient = New-Object Net.Sockets.TcpClient
try {
    $nicResult = $nicClient.BeginConnect($ListenAddress, $Port, $null, $null)
    $nicReady = $nicResult.AsyncWaitHandle.WaitOne(3000)
    if ($nicReady) {
        $nicClient.EndConnect($nicResult)
    }
}
catch {
    $nicReady = $false
}
finally {
    $nicClient.Dispose()
}

if (-not $nicReady) {
    Write-Warning "$ListenAddress`:$Port is not reachable even though Mosquitto is listening on all IPv4 interfaces."
    Write-Warning 'Check third-party endpoint security or repair the Windows TCP/IP stack.'
}

$logDeadline = [DateTime]::UtcNow.AddSeconds(5)
while (-not (Test-Path -LiteralPath $logPath) -and
    [DateTime]::UtcNow -lt $logDeadline) {
    Start-Sleep -Milliseconds 250
}

if (Test-Path -LiteralPath $logPath) {
    & "$env:SystemRoot\System32\icacls.exe" $logPath `
        /grant '*S-1-5-32-545:R' | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Read access could not be granted for $logPath."
    }
}

Write-Host ''
Write-Host 'Mosquitto deployment completed.' -ForegroundColor Green
Write-Host "Mosquitto listener: 0.0.0.0`:$Port"
Write-Host "eStation MQTT server: $ListenAddress`:$Port"
Write-Host "PTLControl Broker: 127.0.0.1`:$Port"
Write-Host 'Username: leave blank'
Write-Host 'Password: leave blank'
Write-Host "Log: $logPath"

if (-not $NoPause) {
    Write-Host ''
    [void](Read-Host 'Press Enter to close this window')
}
