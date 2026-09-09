param([string]$Source)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
& "$root/build.ps1"
if (-not $Source) {
    $Source = "$root/build/tone.wav"
    $rate = 44100
    $count = $rate * 20
    $writer = [IO.BinaryWriter]::new([IO.File]::Create($Source))
    try {
        $writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF')); $writer.Write([int](36 + $count * 2))
        $writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt ')); $writer.Write([int]16)
        $writer.Write([int16]1); $writer.Write([int16]1); $writer.Write([int]$rate)
        $writer.Write([int]($rate * 2)); $writer.Write([int16]2); $writer.Write([int16]16)
        $writer.Write([Text.Encoding]::ASCII.GetBytes('data')); $writer.Write([int]($count * 2))
        for ($i = 0; $i -lt $count; $i++) { $writer.Write([int16](800 * [Math]::Sin(2 * [Math]::PI * 440 * $i / $rate))) }
    } finally { $writer.Dispose() }
}

$stageFile = "$root/build/benchmark-stages.txt"
Remove-Item $stageFile -ErrorAction SilentlyContinue
$app = Start-Process "$root/build/YTMusicLite.exe" -ArgumentList @('--benchmark', ('"' + $Source + '"')) -WorkingDirectory "$root/build" -PassThru
$known = [Collections.Generic.HashSet[int]]::new()
[void]$known.Add($app.Id)
$clock = [Diagnostics.Stopwatch]::StartNew()
$rows = @()
try {
    while (-not $app.HasExited -and $clock.Elapsed.TotalSeconds -lt 60) {
        $all = @(Get-CimInstance Win32_Process)
        do {
            $added = $false
            foreach ($item in $all) { if ($known.Contains([int]$item.ParentProcessId) -and $known.Add([int]$item.ProcessId)) { $added = $true } }
        } while ($added)
        $seconds = [Math]::Round($clock.Elapsed.TotalSeconds, 2)
        foreach ($item in $all) {
            if ($known.Contains([int]$item.ProcessId)) {
                $rows += [pscustomobject]@{ Seconds = $seconds; PID = $item.ProcessId; Name = $item.Name; WorkingSetMiB = [Math]::Round([double]$item.WorkingSetSize / 1MB, 2); PrivateMiB = [Math]::Round([double]$item.PrivatePageCount / 1MB, 2) }
            }
        }
        Start-Sleep -Milliseconds 500
        $app.Refresh()
    }
    if (-not $app.HasExited) { throw 'Memory test exceeded its timeout.' }
    $rows | Export-Csv "$root/build/memory.csv" -NoTypeInformation
    $stages = Get-Content $stageFile -Raw
    if ($stages -match 'FAIL' -or $stages -notmatch 'playing success' -or $stages -notmatch 'paused true' -or $stages -notmatch 'stopped') { throw "Playback validation failed: $stages" }
    $remaining = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $known.Contains($_.Id) })
    if ($remaining.Count) { throw 'A playback helper remained after the client exited.' }
    $totals = $rows | Group-Object Seconds | ForEach-Object { [pscustomobject]@{ Seconds = [double]$_.Name; WorkingSetMiB = ($_.Group | Measure-Object WorkingSetMiB -Sum).Sum; PrivateMiB = ($_.Group | Measure-Object PrivateMiB -Sum).Sum } }
    $totals | Export-Csv "$root/build/memory-totals.csv" -NoTypeInformation
    $peak = ($totals | Measure-Object WorkingSetMiB -Maximum).Maximum
    $idlePeak = ($totals | Where-Object { $_.Seconds -lt 1.5 } | Measure-Object WorkingSetMiB -Maximum).Maximum
    $releasedPeak = ($totals | Where-Object { $_.Seconds -gt 10 } | Measure-Object WorkingSetMiB -Maximum).Maximum
    if ($peak -gt 140) { throw "Combined client and player memory exceeded 140 MiB: $peak MiB" }
    if ($idlePeak -gt 80) { throw "Idle client memory exceeded 80 MiB: $idlePeak MiB" }
    if ($releasedPeak -gt 80) { throw "Memory after Stop exceeded 80 MiB: $releasedPeak MiB" }
    Write-Host "Peak combined working set: $peak MiB" -ForegroundColor Green
    Write-Host "Idle client working set: $idlePeak MiB" -ForegroundColor Green
    Write-Host "Working set after Stop: $releasedPeak MiB" -ForegroundColor Green
} finally {
    if (-not $app.HasExited) { & taskkill /PID $app.Id /T /F | Out-Null }
}
