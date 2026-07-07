# Rouge

A simulation-first 2D top-down roguelike built with **C# / .NET 10** and **MonoGame 3.8.4.1 (WindowsDX)**. See [ProjectSetup.md](ProjectSetup.md) for the full architecture and roadmap.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- MonoGame project templates (one-time install):

```powershell
dotnet new install MonoGame.Templates.CSharp
```

- Windows with Direct3D 11 support

## First-time setup

From the repository root:

```powershell
dotnet tool restore
dotnet restore Rouge.slnx
dotnet build Rouge.slnx
```

## Run the game

```powershell
dotnet run --project src/Game.Client/Game.Client.csproj
```

A 1280×720 window titled **Rouge** should open with a startup screen (dark background, placeholder grid, status text). Press **Escape** to exit.

## Run tests

Headless simulation tests run without opening a window:

```powershell
dotnet test Rouge.slnx
```

## Solution layout

```text
src/
  Game.Client/       MonoGame WindowsDX — rendering, input, game loop
  Game.Simulation/   Pure .NET simulation — no MonoGame references
tests/
  Game.Simulation.Tests/   xUnit smoke tests
```

The client ticks `SimulationHost`, receives a `RenderSnapshot`, and draws via `StartupRenderer`. Simulation logic stays independent of MonoGame so it can be tested and run headlessly.

## Next steps

See **Phase 1** in [ProjectSetup.md](ProjectSetup.md): action-mapped controls, chunk renderer, player movement, YAML content loading, save/load, and debug inspector.
