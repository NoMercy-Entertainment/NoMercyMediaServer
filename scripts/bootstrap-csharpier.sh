#!/usr/bin/env bash
# -----------------------------------------------------------------------------
#  Bootstrap CSharpier in containers where `dotnet tool install/restore` fails.
#
#  This environment's SDK mangles the tool package during extraction
#  ("Settings file 'DotnetToolSettings.xml' was not found in the package"),
#  even though the package itself is valid. A direct curl of the .nupkg comes
#  through intact, so we fetch it ourselves, extract it, and expose a small
#  `csharpier` wrapper that runs the DLL via `dotnet`.
#
#  Idempotent: re-running is a no-op once csharpier resolves. Run it from the
#  environment setup script so every fresh container gets a working formatter.
#
#  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
# -----------------------------------------------------------------------------
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Resolve the pinned version from the tool manifest (root or .config), with a
# sensible fallback so the script still works if the manifest moves.
manifest=""
for candidate in "$repo_root/.config/dotnet-tools.json" "$repo_root/dotnet-tools.json"; do
    if [ -f "$candidate" ]; then manifest="$candidate"; break; fi
done

version=""
if [ -n "$manifest" ]; then
    version="$(grep -A3 '"csharpier"' "$manifest" \
        | grep '"version"' | head -1 \
        | sed -E 's/.*"version"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/')"
fi
version="${version:-1.2.5}"

wrapper="/usr/local/bin/csharpier"
dest="${CSHARPIER_HOME:-$HOME/.csharpier/$version}"
nupkg="$dest/csharpier.$version.nupkg"

if command -v csharpier >/dev/null 2>&1 && csharpier --version >/dev/null 2>&1; then
    echo "csharpier $(csharpier --version) already available"
    exit 0
fi

mkdir -p "$dest"
if [ ! -s "$nupkg" ]; then
    url="https://api.nuget.org/v3-flatcontainer/csharpier/$version/csharpier.$version.nupkg"
    echo "Downloading csharpier $version from $url"
    curl -fSL "$url" -o "$nupkg"
fi

unzip -oq "$nupkg" -d "$dest"

dll="$(ls "$dest"/tools/net*/any/CSharpier.dll 2>/dev/null | sort -V | tail -1 || true)"
if [ -z "$dll" ]; then
    echo "ERROR: CSharpier.dll not found inside $nupkg" >&2
    exit 1
fi

cat > "$wrapper" <<EOF
#!/usr/bin/env bash
exec dotnet "$dll" "\$@"
EOF
chmod +x "$wrapper"

echo "csharpier $("$wrapper" --version) ready ($dll)"
