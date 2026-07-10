param(
    [switch]$IncludePreview
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot "artifacts\windows-x64"

dotnet publish (Join-Path $repoRoot "src\Game.Client\Game.Client.csproj") `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output (Join-Path $artifactRoot "Game.Client")

if ($LASTEXITCODE -ne 0) {
    throw "Windows game publish failed with exit code $LASTEXITCODE."
}

if ($IncludePreview) {
    dotnet publish (Join-Path $repoRoot "src\Game.IslandPreview\Game.IslandPreview.csproj") `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output (Join-Path $artifactRoot "Game.IslandPreview")

    if ($LASTEXITCODE -ne 0) {
        throw "Windows preview publish failed with exit code $LASTEXITCODE."
    }
}

Write-Host "Windows artifacts: $artifactRoot"
