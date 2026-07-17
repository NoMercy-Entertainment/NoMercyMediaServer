#!/usr/bin/env bash
# -----------------------------------------------------------------------------
# Runs the test assemblies with line/branch coverage collection, each in a fully
# isolated app-data root (NOMERCY_APP_PATH -> a private temp dir) so parallel
# test processes never share a database, cache or log directory. Mirrors
# run-tests.sh; the difference is the coverage collector and the merged report.
#
# Collector is `dotnet-coverage` (Microsoft, CLR profiler based), NOT the
# coverlet.collector the projects reference. coverlet rewrites IL, and that
# rewriting crashes the test host on NoMercy.Tests.Api -- it dies at ~427 of
# 1077 tests, and an aborted host never flushes its hit counts, so the report it
# leaves behind lists every instrumented line with zero hits. That reads as
# "0% covered" when the truth is "not measured", which is the more dangerous of
# the two. Under dotnet-coverage the same assembly runs all 1077 green.
#
# Each assembly writes a cobertura XML to coverage/raw/<assembly>.cobertura.xml.
# Because each test assembly only exercises part of src/, a single assembly's
# report is NOT the coverage of the files it touches -- the reports must be
# merged (union of covered lines) before any percentage is read.
# scripts/coverage-report.py does that; this script only produces the inputs.
#
# Requires: dotnet tool install --global dotnet-coverage
#
# Usage:
#   scripts/run-coverage.sh [name-filter-regex]
#   TEST_JOBS=4 scripts/run-coverage.sh          # cap concurrency (default: 4)
# -----------------------------------------------------------------------------
set -u
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
filter="${1:-}"
jobs="${TEST_JOBS:-4}"
out="$repo/coverage/raw"
results="$(mktemp -d)"

if ! command -v dotnet-coverage >/dev/null 2>&1; then
  echo "dotnet-coverage not found. Install with:"
  echo "  dotnet tool install --global dotnet-coverage"
  exit 1
fi

rm -rf "$out"
mkdir -p "$out"

projects="$(find "$repo/tests" -name '*.csproj' | grep -E 'NoMercy\.Tests\.' | sort)"
[ -n "$filter" ] && projects="$(printf '%s\n' "$projects" | grep -iE "$filter")"

# Build once, up front, then run every assembly with --no-build. Each `dotnet
# test` would otherwise rebuild the src/ projects they all share, and parallel
# builds race on the same output DLLs -- MSB3021 "being used by another process"
# fails an assembly for reasons that have nothing to do with its tests.
echo "Building solution once (parallel test runs then use --no-build)..."
if ! dotnet build "$repo/NoMercy.Server.sln" -p:AllowMissingPrunePackageData=true \
  --nologo -clp:ErrorsOnly > "$results/build.log" 2>&1; then
  echo "BUILD FAILED -- coverage not collected:"
  tail -30 "$results/build.log"
  exit 1
fi

run_one() {
  local proj="$1" name root start rc
  name="$(basename "$proj" .csproj)"
  root="$(mktemp -d "/tmp/nm-cov-${name}.XXXXXX")"
  start=$(date +%s)
  NOMERCY_APP_PATH="$root" dotnet-coverage collect \
    --output "$out/$name.cobertura.xml" \
    --output-format cobertura \
    --nologo \
    "dotnet test $proj --no-build -p:AllowMissingPrunePackageData=true --nologo -clp:ErrorsOnly" \
    >"$results/$name.log" 2>&1
  rc=$?
  echo "$rc $(( $(date +%s) - start )) $name" >> "$results/summary"
  rm -rf "$root"
}

echo "Collecting coverage (up to $jobs assemblies in parallel):"
printf '%s\n' "$projects" | sed "s#$repo/##"
start_all=$(date +%s)
for proj in $projects; do
  run_one "$proj" &
  while [ "$(jobs -r | wc -l)" -ge "$jobs" ]; do wait -n; done
done
wait

echo "=== per-assembly results (exit  seconds  name) ==="
sort -k3 "$results/summary"
fail=$(awk '$1!=0' "$results/summary" 2>/dev/null | wc -l)
echo "=== wall=$(( $(date +%s) - start_all ))s  failed_assemblies=$fail ==="
echo "=== cobertura files: $(find "$out" -name '*.cobertura.xml' | wc -l) ==="

if [ "$fail" -ne 0 ]; then
  echo "--- logs (tail) of failed assemblies ---"
  awk '$1!=0{print $3}' "$results/summary" | while read -r n; do
    echo "### $n"; tail -15 "$results/$n.log"
  done
fi

echo "Merge and rank with: python scripts/coverage-report.py"
