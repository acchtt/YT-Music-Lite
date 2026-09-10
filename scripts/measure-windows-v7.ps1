param(
    [string]$ExePath = "$PSScriptRoot\..\src-tauri\target\release\yt-music-lite.exe",
    [int]$Seconds = 60,
    [int]$BudgetMb = 200
)

$ErrorActionPreference = "Stop"
$resolved = (Resolve-Path $ExePath).Path
$process = Start-Process $resolved -PassThru

function Get-ProcessTreePrivateMb([int]$RootProcessId) {
    $rows = Get-CimInstance Win32_Process | Select-Object ProcessId, ParentProcessId
    $ids = [System.Collections.Generic.HashSet[int]]::new()
    $pending = [System.Collections.Generic.Queue[int]]::new()
    [void]$ids.Add($RootProcessId)
    $pending.Enqueue($RootProcessId)
    while ($pending.Count -gt 0) {
        $parent = $pending.Dequeue()
        foreach ($child in $rows | Where-Object ParentProcessId -eq $parent) {
            $id = [int]$child.ProcessId
            if ($ids.Add($id)) { $pending.Enqueue($id) }
        }
    }
    $bytes = 0L
    foreach ($id in $ids) {
        $item = Get-Process -Id $id -ErrorAction SilentlyContinue
        if ($item) { $bytes += $item.PrivateMemorySize64 }
    }
    [math]::Round($bytes / 1MB, 1)
}

try {
    Write-Host "Use the app normally and start playback. Sampling the full WebView2 process tree for $Seconds seconds..."
    $samples = for ($i = 0; $i -lt $Seconds; $i++) {
        Start-Sleep -Seconds 1
        Get-ProcessTreePrivateMb $process.Id
    }
    $sorted = $samples | Sort-Object
    $median = $sorted[[math]::Floor($sorted.Count / 2)]
    $peak = ($samples | Measure-Object -Maximum).Maximum
    Write-Host "Median private working set: $median MB"
    Write-Host "Peak private working set:   $peak MB"
    if ($median -gt $BudgetMb) { throw "RAM budget failed: median $median MB exceeds $BudgetMb MB." }
    Write-Host "RAM budget passed." -ForegroundColor Green
}
finally {
    if (-not $process.HasExited) { $process.CloseMainWindow() | Out-Null }
}
