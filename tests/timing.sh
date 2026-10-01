#!/usr/bin/env bash
# Times each test project: three runs after one build, printing the median wall clock and the median
# divided by the number of tests. Needs Docker running (the database tests start PostgreSQL).
# Usage: tests/timing.sh [runs]
set -euo pipefail

runs="${1:-3}"
root="$(cd "$(dirname "$0")/.." && pwd)"
projects=(
  tests/ModularMonolith.Architecture.Tests
  tests/ModularMonolith.Module.Tests
  tests/ModularMonolith.Api.Tests
)

dotnet build "$root/ModularMonolith.Api.sln" -c Release -v q -nologo > /dev/null

printf '%-36s %6s %12s %14s\n' "Project" "Tests" "Median (s)" "Per test (ms)"
for project in "${projects[@]}"; do
  times=()
  tests=0
  for ((i = 0; i < runs; i++)); do
    start=$(perl -MTime::HiRes=time -e 'printf "%.3f", time')
    output=$(dotnet test "$root/$project" -c Release --no-build -nologo 2>&1)
    end=$(perl -MTime::HiRes=time -e 'printf "%.3f", time')
    tests=$(grep -Eo 'Total: +[0-9]+' <<< "$output" | grep -Eo '[0-9]+' | tail -1)
    if grep -q 'Failed!' <<< "$output"; then
      echo "$project failed; not timing it." >&2
      exit 1
    fi
    times+=("$(echo "$end - $start" | bc)")
  done
  median=$(printf '%s\n' "${times[@]}" | sort -n | awk '{ a[NR] = $1 } END { print (NR % 2) ? a[(NR + 1) / 2] : (a[NR / 2] + a[NR / 2 + 1]) / 2 }')
  per_test=$(echo "scale=1; $median * 1000 / $tests" | bc)
  printf '%-36s %6s %12.2f %14s\n' "$(basename "$project")" "$tests" "$median" "$per_test"
done
