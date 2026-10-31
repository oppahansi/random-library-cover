#!/usr/bin/env bash
# Builds the plugin for one Jellyfin version and packages it as an installable zip.
#
#   scripts/package.sh <jellyfin-version> [plugin-version]
#   scripts/package.sh 10.10.7 1.0.0
#
# Output: dist/randomlibrarycover_<plugin-version>_jellyfin-<major.minor>.zip
set -euo pipefail

JELLYFIN_VERSION="${1:?usage: package.sh <jellyfin-version> [plugin-version]}"
PLUGIN_VERSION="${2:-1.0.0}"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/Jellyfin.Plugin.RandomLibraryCover"
IFS=. read -r MAJOR MINOR _ <<< "$JELLYFIN_VERSION"
LINE="$MAJOR.$MINOR"
FULL_VERSION="$PLUGIN_VERSION.$((MAJOR * 100 + MINOR))"
OUT="$ROOT/artifacts/jellyfin-$LINE"
STAGE="$OUT/stage"
ZIP="$ROOT/dist/randomlibrarycover_${PLUGIN_VERSION}_jellyfin-$LINE.zip"

rm -rf "$OUT"
mkdir -p "$STAGE" "$ROOT/dist"

dotnet publish "$PROJECT" -c Release \
  -p:JellyfinVersion="$JELLYFIN_VERSION" \
  -p:PluginVersion="$PLUGIN_VERSION" \
  -o "$OUT/publish" \
  --nologo -v quiet

cp "$OUT/publish/Jellyfin.Plugin.RandomLibraryCover.dll" "$STAGE/"

cat > "$STAGE/meta.json" <<JSON
{
  "category": "General",
  "changelog": "See https://github.com/oppahansi/random-library-cover/blob/main/CHANGELOG.md",
  "description": "Adds a \"Random cover\" item to the menu of the library cards in My Media on the home screen. It picks a new cover for the library from the backdrops or thumbs of its own items. Covers can also be shuffled automatically on a schedule.",
  "guid": "b94cd594-5ec9-4552-b1b4-455d524fe82e",
  "name": "Random Library Cover",
  "overview": "Give your libraries a random new cover from their own artwork",
  "owner": "oppahansi",
  "targetAbi": "$LINE.0.0",
  "timestamp": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "version": "$FULL_VERSION",
  "status": "Active",
  "autoUpdate": true,
  "assemblies": []
}
JSON

rm -f "$ZIP"
(cd "$STAGE" && zip -q -X "$ZIP" Jellyfin.Plugin.RandomLibraryCover.dll meta.json)
echo "$ZIP"
