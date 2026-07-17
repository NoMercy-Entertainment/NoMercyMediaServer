#!/usr/bin/env bash
# -----------------------------------------------------------------------------
# Runs the test assemblies concurrently, each in a fully isolated app-data root
# (NOMERCY_APP_PATH -> a private temp dir) so parallel test processes never share
# a database, cache or log directory. Within-assembly parallelism stays disabled
# (see tests/SharedTestParallelization.cs); the speed-up comes from running the
# ~19 assemblies in parallel instead of one after another.
#
# Usage:
#   scripts/run-tests.sh [name-filter-regex]
#   TEST_JOBS=8 scripts/run-tests.sh            # cap concurrency (default: nproc)
# -----------------------------------------------------------------------------
set -u
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
filter="${1:-}"
jobs="${TEST_JOBS:-$(nproc 2>/dev/null || echo 4)}"
results="$(mktemp -d)"

# Match only real test projects (NoMercy.Tests.*). A bare `grep -i tests` also
# matched sample/fixture projects under tests/ (e.g. NoMercy.Plugin.Samples.Echo),
# which run zero tests and reported a false green.
projects="$(find "$repo/tests" -name '*.csproj' | grep -E 'NoMercy\.Tests\.' | sort)"
[ -n "$filter" ] && projects="$(printf '%s\n' "$projects" | grep -iE "$filter")"

# Build once, up front, then run every assembly with --no-build. Each `dotnet
# test` would otherwise rebuild the src/ projects they all share, and parallel
# builds race on the same output DLLs -- MSB3021 "being used by another process"
# fails an assembly for reasons that have nothing to do with its tests.
echo "Building solution once (parallel test runs then use --no-build)..."
if ! dotnet build "$repo/NoMercy.Server.sln" -p:AllowMissingPrunePackageData=true \
  --nologo -clp:ErrorsOnly > "$results/build.log" 2>&1; then
  echo "BUILD FAILED -- tests not run:"
  tail -30 "$results/build.log"
  exit 1
fi

run_one() {
  local proj="$1" name root start rc
  name="$(basename "$proj" .csproj)"
  root="$(mktemp -d "/tmp/nm-${name}.XXXXXX")"
  start=$(date +%s)
  NOMERCY_APP_PATH="$root" dotnet test "$proj" --no-build \
    -p:AllowMissingPrunePackageData=true --nologo -clp:ErrorsOnly \
    >"$results/$name.log" 2>&1
  rc=$?
  echo "$rc $(( $(date +%s) - start )) $name" >> "$results/summary"
  rm -rf "$root"
}

echo "Running test assemblies (up to $jobs in parallel):"
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
if [ "$fail" -ne 0 ]; then
  echo "--- logs (tail) of failed assemblies ---"
  awk '$1!=0{print $3}' "$results/summary" | while read -r n; do
    echo "### $n"; tail -15 "$results/$n.log"
  done
  exit 1
fi
