# Rouge

A simulation-first 2D top-down roguelike built with **C# / .NET 10** and **MonoGame 3.8.4.1 (DesktopGL)**. The same client source and content pipeline support Windows x64 and Apple Silicon macOS. See [ProjectSetup.md](ProjectSetup.md) for the full architecture and roadmap.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows x64 or Apple Silicon macOS with OpenGL support
- MonoGame project templates (optional, one-time install):

```shell
dotnet new install MonoGame.Templates.CSharp
```

## First-time setup

From the repository root:

```shell
dotnet tool restore
dotnet restore Rouge.slnx
dotnet build Rouge.slnx
```

## Run the game

```shell
dotnet run --project src/Game.Client/Game.Client.csproj
```

A 1280×720 window titled **Rouge** should open with a startup screen (dark background, placeholder grid, status text). Press **Escape** to exit.

## Run tests

Headless simulation tests run without opening a window:

```shell
dotnet test Rouge.slnx
```

## Publish native builds

The publish scripts create self-contained Release folders. The game is the default artifact; pass the optional preview flag to publish the Island Preview tool too.

Windows x64, from PowerShell:

```powershell
.\scripts\publish-windows.ps1
.\scripts\publish-windows.ps1 -IncludePreview
```

Apple Silicon macOS:

```shell
bash scripts/publish-macos.sh
bash scripts/publish-macos.sh --include-preview
```

Outputs are written to:

```text
artifacts/
  windows-x64/
    Game.Client/
    Game.IslandPreview/    # optional
  macos-arm64/
    Game.Client/
    Game.IslandPreview/    # optional
```

Run `Game.Client.exe` from the Windows game folder or `./Game.Client` from the macOS game folder. macOS output is currently a runnable folder, not a signed `.app`; Gatekeeper distribution, signing, notarization, DMG packaging, and Intel Mac support are not included.

Build and smoke-test each artifact on its native operating system before distributing it. In particular, verify content/font loading, keyboard and mouse input, Retina scaling on macOS, world generation, and save/load.

## Solution layout

```text
src/
  Game.Client/       MonoGame DesktopGL — rendering, input, game loop
  Game.Simulation/   Pure .NET simulation — no MonoGame references
tests/
  Game.Simulation.Tests/   xUnit smoke tests
```

The client ticks `SimulationHost`, receives a `RenderSnapshot`, and draws via `StartupRenderer`. Simulation logic stays independent of MonoGame so it can be tested and run headlessly.

## Next steps

See **Phase 1** in [ProjectSetup.md](ProjectSetup.md): action-mapped controls, chunk renderer, player movement, YAML content loading, save/load, and debug inspector.
