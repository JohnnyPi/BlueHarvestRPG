# Island Generator Preview

Standalone tool for iterating on island generation parameters and inspecting the full overworld map.

## Run

```powershell
dotnet run --project src/Game.IslandPreview/Game.IslandPreview.csproj
```

Optional fixed seed:

```powershell
dotnet run --project src/Game.IslandPreview/Game.IslandPreview.csproj -- --seed 12345
```

## Manual smoke checklist

1. Launch the preview — a split window opens (parameter sidebar + map viewport).
2. Sidebar lists all island and biome parameters; mouse wheel scrolls the list.
3. Change `Main Island Radius`, click **Generate** — island shape changes.
4. Change `Mountains Min Elevation` under Biome Rules — high-elevation biome mix changes.
5. Same seed + same parameters → identical map (determinism).
6. Left-drag on the map pans; scroll wheel zooms toward the cursor.
7. Generate at 512×512 shows a progress overlay and completes without freezing the UI.

## Controls

| Area | Action |
|------|--------|
| Sidebar | Click fields to edit, scroll to browse, **Generate** / **Randomize** / **Reset** |
| Map | Left-drag pan, scroll zoom, WASD/arrows pan |
