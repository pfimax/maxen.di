<#
.SYNOPSIS
  Compares a fresh BenchmarkDotNet run against the committed baseline, or records a new baseline.

.DESCRIPTION
  Run the benchmarks with the json exporter first:
    dotnet run -c Release --project benchmarks/Maxen.DI.Benchmarks -- --filter "*" --job short --exporters json

  Then compare:
    pwsh scripts/compare-benchmarks.ps1

  Only the Maxen.DI rows are checked: the Microsoft and Autofac rows are there for context, and they
  shift with the machine as much as ours do. Short runs are noisy, so the default tolerance is generous;
  treat a failure as "look into it", not as proof.

.PARAMETER Results   Folder holding the BenchmarkDotNet json report(s).
.PARAMETER Baseline  The committed baseline file.
.PARAMETER Tolerance How much slower (as a fraction) a benchmark may get before failing. 0.35 = 35%.
.PARAMETER Update    Write the current numbers as the new baseline instead of comparing.
#>
param(
    [string]$Results = "BenchmarkDotNet.Artifacts/results",
    [string]$Baseline = "benchmarks/baseline.json",
    [double]$Tolerance = 0.35,
    [switch]$Update
)

$ErrorActionPreference = "Stop"

# Name -> mean in nanoseconds, for the rows that belong to Maxen.DI.
function Read-Means([string]$folder) {
    $files = Get-ChildItem -Path $folder -Filter "*-report-full*.json" -ErrorAction SilentlyContinue
    if (-not $files) { throw "No *-report-full*.json in '$folder'. Run the benchmarks with --exporters json first." }

    $means = [ordered]@{}
    foreach ($file in $files) {
        $report = Get-Content $file.FullName -Raw | ConvertFrom-Json
        foreach ($benchmark in $report.Benchmarks) {
            $name = "$($benchmark.Method)"
            if ($name -notmatch "Maxen") { continue }
            $means[$name] = [math]::Round([double]$benchmark.Statistics.Mean, 2)
        }
    }
    return $means
}

$current = Read-Means $Results

if ($Update) {
    New-Item -ItemType Directory -Force -Path (Split-Path $Baseline) | Out-Null
    $current | ConvertTo-Json | Set-Content $Baseline
    Write-Host "Baseline written to $Baseline"
    $current.GetEnumerator() | ForEach-Object { Write-Host ("  {0,-30} {1,10:N2} ns" -f $_.Key, $_.Value) }
    return
}

if (-not (Test-Path $Baseline)) { throw "No baseline at '$Baseline'. Create one with -Update." }
$before = Get-Content $Baseline -Raw | ConvertFrom-Json

$failed = $false
foreach ($entry in $current.GetEnumerator()) {
    $old = $before.($entry.Key)
    if ($null -eq $old) {
        Write-Host ("{0,-30} new benchmark, {1:N2} ns" -f $entry.Key, $entry.Value)
        continue
    }

    $change = ($entry.Value - $old) / $old
    $verdict = if ($change -gt $Tolerance) { $failed = $true; "SLOWER" } elseif ($change -lt -$Tolerance) { "faster" } else { "ok" }
    Write-Host ("{0,-30} {1,10:N2} ns (was {2,10:N2}, {3,6:P0})  {4}" -f $entry.Key, $entry.Value, $old, $change, $verdict)
}

if ($failed) { Write-Error "At least one benchmark got slower than the allowed $([int]($Tolerance * 100))%."; exit 1 }
