# Oyunu tek komutla derleyip acar.
#   Windows penceresi: powershell -ExecutionPolicy Bypass -File Tools\Run.ps1
#   Telefon:           powershell -ExecutionPolicy Bypass -File Tools\Run.ps1 -Phone
# -NoBuild son derlemeyi kullanir. -CheckPhone sadece telefon baglantisini dener.
# Windows penceresinde fare parmak yerine gecer: tiklama dokunus, surukleme kaydirma, tekerlek yakinlastirma.
param(
    [switch]$Phone,
    [switch]$NoBuild,
    [switch]$CheckPhone,
    [float]$TestSeconds = 0
)

$unityRoot = 'C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor'
$unity = Join-Path $unityRoot 'Unity.exe'
$adb = Join-Path $unityRoot 'Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
$project = Split-Path $PSScriptRoot -Parent
$buildLog = Join-Path $env:TEMP 'geowar-build.log'
$addressFile = Join-Path $project 'UserSettings\phone-address.txt'
$package = 'com.geowar.game/com.unity3d.player.UnityPlayerGameActivity'

function Build-Game([string]$target, [string]$method) {
    $lock = Join-Path $project 'Temp\UnityLockfile'
    if ((Test-Path $lock) -and (Get-Process Unity -ErrorAction SilentlyContinue)) {
        Write-Host 'Unity bu projeyle acik. Derleme icin Unity penceresini kapat.'
        exit 1
    }

    Write-Host "Derleniyor ($target)..."
    $arguments = @('-batchmode', '-nographics', '-projectPath', "`"$project`"", '-buildTarget', $target, '-executeMethod', $method, '-logFile', "`"$buildLog`"")
    $process = Start-Process -FilePath $unity -ArgumentList $arguments -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        Write-Host 'Derleme olmadi:'
        Select-String -Path $buildLog -Pattern 'error CS|Error building|BuildFailedException' | Select-Object -First 15 | ForEach-Object { Write-Host $_.Line }
        Write-Host "Ayrinti: $buildLog"
        exit 1
    }
}

function Test-PhoneReady {
    $devices = @(& $adb devices | Select-Object -Skip 1 | Where-Object { $_ -match '\sdevice\s*$' })
    return $devices.Count -gt 0
}

# Once kayitli adres denenir; mDNS acik telefonu bile bazen gormez.
function Connect-Phone {
    & $adb start-server | Out-Null
    if (Test-PhoneReady) { return $true }

    $candidates = @()
    if (Test-Path $addressFile) { $candidates += (Get-Content $addressFile -TotalCount 1).Trim() }
    $services = (& $adb mdns services) -join "`n"
    $match = [regex]::Match($services, '_adb-tls-connect\._tcp\S*\s+(\d+\.\d+\.\d+\.\d+:\d+)')
    if ($match.Success) { $candidates += $match.Groups[1].Value }

    foreach ($address in $candidates) {
        & $adb connect $address | Out-Null
        if (Test-PhoneReady) {
            Set-Content -Path $addressFile -Value $address -Encoding ASCII
            return $true
        }
    }

    Write-Host 'Telefon bulunamadi. Kablosuz hata ayiklama ana ekranindaki IP adresi ve baglanti noktasini yaz.'
    $typed = Read-Host 'Ornek 192.168.1.8:42277 (bos birakirsan vazgecer)'
    if ($typed) {
        & $adb connect $typed.Trim() | Out-Null
        if (Test-PhoneReady) {
            Set-Content -Path $addressFile -Value $typed.Trim() -Encoding ASCII
            return $true
        }
    }

    return $false
}

function Test-PhoneUnlocked {
    $lock = (& $adb shell "dumpsys window | grep -E 'isKeyguardShowing' | head -1") -join ' '
    $power = (& $adb shell "dumpsys power | grep -E 'mWakefulness='") -join ' '
    return ($lock -match 'isKeyguardShowing=false') -and ($power -match 'Awake')
}

if ($CheckPhone) {
    if (Connect-Phone) { Write-Host 'Telefon bagli.'; & $adb devices -l; exit 0 }
    Write-Host 'Telefon bagli degil.'
    exit 1
}

if ($Phone) {
    if (-not $NoBuild) { Build-Game 'Android' 'GeoWar.EditorTools.GeoWarBuild.BuildAndroid' }
    if (-not (Connect-Phone)) { exit 1 }

    Write-Host 'Telefona kuruluyor...'
    & $adb install -r (Join-Path $project 'Build\Android\GeoWar.apk')
    if ($LASTEXITCODE -ne 0) { exit 1 }

    if (Test-PhoneUnlocked) {
        & $adb shell am start -n $package | Out-Null
        Write-Host 'Oyun telefonda acildi.'
    }
    else {
        Write-Host 'Kuruldu. Telefon kilitli: kilidi acip GeoWar uygulamasini baslat.'
    }

    exit 0
}

if (-not $NoBuild) { Build-Game 'Win64' 'GeoWar.EditorTools.GeoWarBuild.BuildWindows' }
$arguments = @('-screen-width', '390', '-screen-height', '844', '-screen-fullscreen', '0')
if ($TestSeconds -gt 0) { $arguments += @('-geowarTestSeconds', $TestSeconds.ToString([Globalization.CultureInfo]::InvariantCulture)) }
$player = Start-Process -FilePath (Join-Path $project 'Build\Windows\GeoWar.exe') -ArgumentList $arguments -PassThru
Write-Host 'Oyun Windows penceresinde acildi.'
if ($TestSeconds -gt 0) { $player.WaitForExit(); Write-Host "Cikis kodu: $($player.ExitCode)" }
