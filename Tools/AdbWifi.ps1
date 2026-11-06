# Telefonu Wi-Fi uzerinden ADB'ye baglar. Telefon ve bilgisayar ayni agda olmali.
# Eslestirme ilk seferde gerekir. Kablosuz hata ayiklama her acildiginda baglanti noktasi degisir, bu yuzden tekrar calistirilir.
# Kullanim: powershell -ExecutionPolicy Bypass -File Tools\AdbWifi.ps1 [-Pair]
param([switch]$Pair)

$adb = 'C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
$discoverySeconds = 60

function Find-Service([string]$type) {
    $deadline = (Get-Date).AddSeconds($discoverySeconds)
    while ((Get-Date) -lt $deadline) {
        $match = [regex]::Match(((& $adb mdns services) -join "`n"), "$type\S*\s+(\d+\.\d+\.\d+\.\d+:\d+)")
        if ($match.Success) { return $match.Groups[1].Value }
        Start-Sleep -Seconds 2
    }
    return $null
}

& $adb start-server | Out-Null

if ($Pair) {
    Write-Host ''
    Write-Host 'Telefonda: Gelistirici secenekleri > Kablosuz hata ayiklama > Eslestirme kodu ile cihazi eslestir'
    Write-Host 'Kod ekrani acik kalsin. Telefon araniyor...'
    $pairAddress = Find-Service '_adb-tls-pairing._tcp'
    if (-not $pairAddress) {
        $pairAddress = Read-Host 'Telefon bulunamadi. Kod ekranindaki IP adresi ve baglanti noktasini yaz (ornek 192.168.1.5:41235)'
    }

    Write-Host "Eslestiriliyor: $pairAddress"
    & $adb pair $pairAddress
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Eslestirme olmadi.'
        exit 1
    }
}

Write-Host 'Baglanti araniyor...'
$connectAddress = Find-Service '_adb-tls-connect._tcp'
if (-not $connectAddress) {
    $connectAddress = Read-Host 'Telefon bulunamadi. Kablosuz hata ayiklama ana ekranindaki IP adresi ve baglanti noktasini yaz'
}

& $adb connect $connectAddress
& $adb devices -l
