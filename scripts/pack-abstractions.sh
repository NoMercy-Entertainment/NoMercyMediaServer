#!/usr/bin/env bash
# Pack the plugin contract for the plugin repos to build against.
#
# NoMercy.Events and NoMercy.Design travel with it: all three are in the
# shared-assembly set and a plugin cannot restore one without the others at the
# SAME version — a mismatch fails with NU1102 naming only the one you forgot.
# NoMercy.Design is the design system's component contract, which the plugin
# contract now references so a plugin can name any of the fifty-six components.
#
# Stable versions, no prerelease suffix: a plugin pinning Version="*" will not
# match a prerelease, and the failure reads as a missing package rather than a
# wrong one.
#
#   scripts/pack-abstractions.sh 10.0.101
set -uo pipefail

version="${1:-}"
if [ -z "$version" ]; then
  echo "usage: $0 <version>   e.g. $0 10.0.101" >&2
  exit 2
fi

here="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
feed="C:/Projects/StoneyEagle/.nm-plugin-feed"
mkdir -p "$feed"

projects=(NoMercy.Plugins.Abstractions NoMercy.Events NoMercy.Design)

for project in "${projects[@]}"; do
  echo "== $project $version =="
  if ! dotnet pack "$here/src/$project/$project.csproj" \
    -c Release -p:PackageVersion="$version" -p:IsPackable=true \
    -o "$feed" --nologo; then
    echo "   FAILED: $project" >&2
    exit 1
  fi
done

# Each plugin repo keeps its own _nupkgs folder as its nuget source, so the
# packages have to land in every one of them, not only the shared feed.
for repo in C:/Projects/StoneyEagle/nomercy-*-plugin; do
  [ -d "$repo" ] || continue
  mkdir -p "$repo/_nupkgs"
  for project in "${projects[@]}"; do
    cp "$feed/$project.$version.nupkg" "$repo/_nupkgs/" 2>/dev/null
  done
  echo "seeded $(basename "$repo")"
done
