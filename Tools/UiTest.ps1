# Arayuzun duman testi: acik Unity'yi kopru uzerinden surer, dugmelere ekran konumundan basar ve sonuclarini kontrol eder.
# Unity bu projeyle acik olmali. Calisan oyunu durdurup kodu yeniler ve Play'e girer; sen oynarken calistirma.
# Kullanim: powershell -NoProfile -ExecutionPolicy Bypass -File Tools\UiTest.ps1
$ErrorActionPreference = 'Stop'
$base = 'http://127.0.0.1:47800/'
$failures = 0

function CallRaw([string]$path) {
    for ($i = 0; $i -lt 30; $i++) {
        try {
            $client = New-Object Net.WebClient
            $client.Encoding = [Text.Encoding]::UTF8
            return $client.DownloadString($base + $path)
        } catch [System.Net.WebException] {
            Start-Sleep -Seconds 1
        }
    }

    throw "No answer: $path"
}

function Call([string]$path) {
    return (CallRaw $path) | ConvertFrom-Json
}

function Check([string]$name, [bool]$passed, $detail) {
    if ($passed) { "  ok   $name" } else { "  FAIL $name -> $detail"; $script:failures++ }
}

function Tap([double]$x, [double]$y) { $result = (Call ('tap?x=' + $x.ToString([Globalization.CultureInfo]::InvariantCulture) + '&y=' + $y.ToString([Globalization.CultureInfo]::InvariantCulture))).result; Start-Sleep -Milliseconds 200; return $result }
function TapText([string]$text) { $result = (Call ('tapText?text=' + [Uri]::EscapeDataString($text))).result; Start-Sleep -Milliseconds 200; return $result }

$status = Call 'status'
"Before: playing=$($status.isPlaying) focused=$($status.unityFocused) compileErrors=$($status.compileErrors.Count)"
if ($status.compileErrors.Count -gt 0) { $status.compileErrors; exit 1 }

# Biri oyunu o sirada kullaniyorsa kesilmez.
if ($status.isPlaying -and $null -ne $status.game -and $status.game.secondsSinceInput -lt 30) {
    'Oyun su an kullaniliyor, test baslatilmadi.'
    exit 2
}

Call 'rerun' | Out-Null
$deadline = (Get-Date).AddMinutes(4)
$running = $false
while (-not $running -and (Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 2
    try {
        $status = Call 'status'
        if ($status.compileErrors.Count -gt 0) { 'Compile errors:'; $status.compileErrors; exit 1 }
        $running = $status.isPlaying -and $null -ne $status.game
    } catch { }
}

if (-not $running) { 'Game did not start'; exit 1 }
Start-Sleep -Seconds 3
$sequence = (Call 'status').logSequence

$s = Call 'state'
"Screen $($s.screenWidth)x$($s.screenHeight), view $($s.viewWidth)x$($s.viewHeight), day $($s.day), date $($s.date), fps $($s.fps)"

'Speed buttons (real hit test by label position):'
$r = TapText '3x'; Check 'tap 3x hits a button' ($r -like 'button*') $r
Check 'speed is 3' ((Call 'state').speed -eq 3) (Call 'state').speed
$r = TapText 'II'; Check 'tap pause hits a button' ($r -like 'button*') $r
$a = Call 'state'; Start-Sleep -Seconds 2; $b = Call 'state'
Check 'pause stops the day bar' ($a.speed -eq 0 -and $a.dayProgress -eq $b.dayProgress) "$($a.dayProgress) vs $($b.dayProgress)"
$r = TapText '1x'; Check 'tap 1x hits a button' ($r -like 'button*') $r
$a = Call 'state'; Start-Sleep -Seconds 2; $b = Call 'state'
Check 'day bar moves about 0.4 in 2 seconds at 1x' (($b.dayProgress - $a.dayProgress) -gt 0.3 -and ($b.dayProgress - $a.dayProgress) -lt 0.5 -or $b.day -gt $a.day) "$($a.dayProgress) -> $($b.dayProgress)"

'Zoom buttons and camera:'
$w = (Call 'state').viewWidth
$z0 = (Call 'state').zoom
$r = Tap ($w - 26) 150; Check 'plus hits a button' ($r -eq 'button') $r
Start-Sleep -Milliseconds 700
$z1 = (Call 'state').zoom; Check 'plus zooms in by 1.4' ([math]::Abs($z1 / $z0 - 1.4) -lt 0.02) "$z0 -> $z1"
$r = Tap ($w - 26) 190; Check 'minus hits a button' ($r -eq 'button') $r
Start-Sleep -Milliseconds 700
$z2 = (Call 'state').zoom; Check 'minus zooms back' ([math]::Abs($z2 - $z0) -lt 0.02) "$z2 vs $z0"
$c0 = Call 'state'; Call 'pan?dx=40&dy=0' | Out-Null; $c1 = Call 'state'
Check 'pan moves the camera' ($c0.camX -ne $c1.camX) "$($c0.camX) -> $($c1.camX)"
$r = Tap ($w - 26) 230; Check 'fit hits a button' ($r -eq 'button') $r
Start-Sleep -Milliseconds 700

'Tap on the map and the UI blocking it:'
$st = Call 'state'
$r = Tap ($st.viewWidth / 2) 300; Check 'center of the map reaches the map' ($r -eq 'map') $r
$r = Tap ($st.viewWidth / 2) 30; Check 'header blocks the map' ($r -ne 'map') $r

'Resource window:'
Call 'select?name=Ankara' | Out-Null
Start-Sleep -Milliseconds 600
$r = Tap 75 49; Check 'stock cell hits a button' ($r -eq 'button') $r
Check 'window opened on cell 1' ((Call 'state').resourceCell -eq 1) (Call 'state').resourceCell
$over = CallRaw 'labels?overflow=1'
"  overflow labels with window open: $over"
$r = TapText 'Gübre'; Check 'tap a card' ($r -like 'button*') $r
$labels = (CallRaw 'labels') | ConvertFrom-Json
$fertilizer = $labels | Where-Object { $_.text -eq 'Gübre' } | Select-Object -First 1
$cardWidth = ($st.viewWidth - 20 - 6) / 3
$pinX = $fertilizer.x - 25 + $cardWidth - 15
$r = Tap $pinX ($fertilizer.y + 8); Check 'pin hits a button' ($r -eq 'button') $r
$pinLog = (Call "logs?since=$sequence").lines | Where-Object { $_.text -like '*resource pin=fertilizer cell=1*' }
Check 'pin moved the resource to the open cell' ([bool]$pinLog) 'no pin log'
$r = Tap ($st.viewWidth / 2) ($st.viewHeight - 20); Check 'outside tap closes the window' ($r -eq 'button' -and (Call 'state').resourceCell -eq -1) "$r $((Call 'state').resourceCell)"

'Province panel and building:'
Call 'select?name=Ankara' | Out-Null
Start-Sleep -Milliseconds 800
$built0 = (Call 'state').builtFacilities
$labels = (CallRaw 'labels') | ConvertFrom-Json
$slotCaption = $labels | Where-Object { $_.text -like 'Slot *' } | Select-Object -First 1
"  $($slotCaption.text)"
$used = [int]($slotCaption.text -replace 'Slot (\d+)/.*','$1')
$tileX = 10 + $used * 56 + 26
$tileY = $slotCaption.y + $slotCaption.h + 3 + 26
$r = Tap $tileX $tileY; Check 'empty slot tile hits a button' ($r -eq 'button') $r
$r = TapText 'TESİS KUR'; Check 'build button hits a button' ($r -like 'button*') $r
Check 'builder opened' ((Call 'state').builderOpen) 'closed'
$over = CallRaw 'labels?overflow=1'
"  overflow labels in builder: $over"
$r = TapText 'KUR · '; Check 'KUR hits a button' ($r -like 'button*') $r
$after = Call 'state'
Check 'facility was built' ($after.builtFacilities -eq $built0 + 1) "$built0 -> $($after.builtFacilities)"
Check 'builder closed after building' (-not $after.builderOpen) 'still open'
$labels = (CallRaw 'labels') | ConvertFrom-Json
Check 'panel shows construction' ([bool]($labels | Where-Object { $_.text -like 'İnşaat:*' })) 'no construction label'

'Alert jump and upgrade:'
$r = Tap 60 104; Check 'ready alert hits a button' ($r -eq 'button') $r
Start-Sleep -Milliseconds 600
"  selected: $((Call 'state').selected)"
$money0 = (Call 'state').money
$r = TapText 'YÜKSELT'; Check 'upgrade button hits a button' ($r -like 'button*') $r
$labels = (CallRaw 'labels') | ConvertFrom-Json
Check 'upgrade started' ([bool]($labels | Where-Object { $_.text -like 'AR-GE:*' })) ($labels | Where-Object { $_.text -in 'Hazır','Çalışıyor' } | ForEach-Object { $_.text })

'Foreign panel:'
Call 'select?name=Bağdat' | Out-Null
Start-Sleep -Milliseconds 700
$labels = (CallRaw 'labels') | ConvertFrom-Json
Check 'foreign panel shows country' ([bool]($labels | Where-Object { $_.text -like 'Irak*' })) 'no country'

'Overflow on the plain screen:'
$over = CallRaw 'labels?overflow=1'
"  $over"

$logs = Call "logs?since=$sequence"
$problems = @($logs.lines | Where-Object { $_.type -in 'Error','Exception','Assert' })
Check 'no errors in the Unity log during the test' ($problems.Count -eq 0) (($problems | Select-Object -First 3 | ForEach-Object { $_.text }) -join ' || ')
'UI log lines:'
$logs.lines | Where-Object { $_.text -like 'GeoWarUi*' } | ForEach-Object { '  ' + $_.text }

$shot = Call 'screenshot'
Start-Sleep -Seconds 2
"Screenshot: $($shot.path) exists=$(Test-Path $shot.path)"
"RESULT: $failures failures"
exit $failures
