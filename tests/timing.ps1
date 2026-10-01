# Times each test project: three runs after one build, printing the median wall clock and the median
# divided by the number of tests. Needs Docker running (the database tests start PostgreSQL).
# Usage: tests/timing.ps1 [-Runs 3]
param([int]$Runs = 3)
$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$projects = @(
    'tests/ModularMonolith.Architecture.Tests',
    'tests/ModularMonolith.Module.Tests',
    'tests/ModularMonolith.Api.Tests'
)

dotnet build (Join-Path $root 'ModularMonolith.Api.sln') -c Release -v q -nologo | Out-Null

'{0,-36} {1,6} {2,12} {3,14}' -f 'Project', 'Tests', 'Median (s)', 'Per test (ms)'
foreach ($project in $projects) {
    $times = @()
    $tests = 0
    for ($i = 0; $i -lt $Runs; $i++) {
        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        $output = dotnet test (Join-Path $root $project) -c Release --no-build -nologo 2>&1 | Out-String
        $watch.Stop()
        if ($output -match 'Failed!') { throw "$project failed; not timing it." }
        $tests = [int]([regex]::Matches($output, 'Total:\s+(\d+)') | Select-Object -Last 1).Groups[1].Value
        $times += $watch.Elapsed.TotalSeconds
    }
    $sorted = $times | Sort-Object
    $median = if ($sorted.Count % 2) { $sorted[[math]::Floor($sorted.Count / 2)] }
              else { ($sorted[$sorted.Count / 2 - 1] + $sorted[$sorted.Count / 2]) / 2 }
    '{0,-36} {1,6} {2,12:N2} {3,14:N1}' -f (Split-Path $project -Leaf), $tests, $median, ($median * 1000 / $tests)
}
