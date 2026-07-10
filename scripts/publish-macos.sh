#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
artifact_root="$repo_root/artifacts/macos-arm64"
include_preview=false

if [[ "${1:-}" == "--include-preview" ]]; then
  include_preview=true
elif [[ $# -gt 0 ]]; then
  echo "Usage: $0 [--include-preview]" >&2
  exit 2
fi

dotnet publish "$repo_root/src/Game.Client/Game.Client.csproj" \
  --configuration Release \
  --runtime osx-arm64 \
  --self-contained true \
  --output "$artifact_root/Game.Client"

if [[ "$include_preview" == true ]]; then
  dotnet publish "$repo_root/src/Game.IslandPreview/Game.IslandPreview.csproj" \
    --configuration Release \
    --runtime osx-arm64 \
    --self-contained true \
    --output "$artifact_root/Game.IslandPreview"
fi

echo "macOS artifacts: $artifact_root"
