The next goal should be a complete **overworld → enter square → local map → return to overworld** loop. Keep the first version deliberately small, deterministic, and mostly terrain-only.

# 1. Establish the two world scales

Treat the overworld and local maps as separate layers.

## Overworld

The overworld is a grid of large geographic cells:

```text
Overworld
┌────┬────┬────┬────┐
│Sea │Hill│Forest│Sea │
├────┼────┼────┼────┤
│Sea │Plains│Town│Hill│
├────┼────┼────┼────┤
│Sea │Swamp│Forest│Sea │
└────┴────┴────┴────┘
```

Each overworld cell might represent:

* 500 meters.
* 1 kilometer.
* Several kilometers.

Do not settle the exact physical scale yet. What matters initially is that one overworld cell generates one local map.

## Local map

Entering a world cell creates or loads a more detailed map:

```text
One forest overworld cell

64 × 64 local tiles
┌─────────────────────┐
│ trees, rocks, paths │
│ streams, clearings  │
│ creatures, objects  │
└─────────────────────┘
```

Start with:

* **64×64 overworld cells**.
* **64×64 local tiles per overworld cell**.

This gives you a conceptual world of 4096×4096 local tiles without generating all of them at once.

Later, the overworld can become much larger or effectively unbounded.

# 2. Define your coordinate types

Do not pass generic `Vector2` values around for everything. Overworld coordinates and local coordinates have different meanings.

```csharp
public readonly record struct WorldCoord(int X, int Y);

public readonly record struct LocalCoord(int X, int Y);

public readonly record struct GlobalTileCoord(int X, int Y);
```

You can convert an overworld and local coordinate into a global tile coordinate:

```csharp
public static GlobalTileCoord ToGlobalTile(
    WorldCoord world,
    LocalCoord local,
    int localMapSize)
{
    return new GlobalTileCoord(
        world.X * localMapSize + local.X,
        world.Y * localMapSize + local.Y);
}
```

Even if global coordinates are not initially rendered, they become useful for:

* Distance calculations.
* Rivers crossing multiple maps.
* Roads.
* Weather systems.
* Regional pathfinding.
* Migrating animals.
* Persistent object positions.

# 3. Create a minimal overworld cell

Initially, an overworld cell only needs environmental information.

```csharp
public enum BiomeId : ushort
{
    Ocean,
    Beach,
    Plains,
    Forest,
    Swamp,
    Hills,
    Mountains
}

public struct WorldCell
{
    public float Elevation;
    public float Moisture;
    public float Temperature;

    public BiomeId Biome;

    public bool HasLocalChanges;
}
```

Later, a cell might also contain:

```text
River presence
Road connections
Settlement ID
Faction influence
Danger level
Population estimate
Resource deposits
Weather state
Regional events
Local-map generation status
```

Do not add those yet. Start with elevation, moisture, temperature, and biome.

# 4. Store the overworld in contiguous arrays

Avoid making every cell a separate object.

```csharp
public sealed class Overworld
{
    public int Width { get; }
    public int Height { get; }
    public ulong Seed { get; }

    private readonly WorldCell[] _cells;

    public Overworld(int width, int height, ulong seed)
    {
        Width = width;
        Height = height;
        Seed = seed;
        _cells = new WorldCell[width * height];
    }

    public ref WorldCell GetCell(WorldCoord coord)
    {
        if (!Contains(coord))
            throw new ArgumentOutOfRangeException(nameof(coord));

        return ref _cells[coord.Y * Width + coord.X];
    }

    public bool Contains(WorldCoord coord)
    {
        return coord.X >= 0 &&
               coord.Y >= 0 &&
               coord.X < Width &&
               coord.Y < Height;
    }
}
```

This is easy to inspect and fast enough for the initial version.

Later, replace the single array with overworld chunks:

```text
OverworldChunk
└── 32×32 world cells
```

That allows very large worlds without loading the entire overworld.

# 5. Add deterministic generation

The most important rule is:

> A location must generate the same result every time from the same seed and coordinates.

Avoid relying on one mutable global random-number generator. Generation order should not affect the results.

Conceptually:

```text
local seed =
    hash(
        world seed,
        world X,
        world Y,
        generator version
    )
```

Create a seed utility:

```csharp
public static class SeedUtility
{
    public static ulong Derive(
        ulong worldSeed,
        int x,
        int y,
        uint generatorVersion)
    {
        unchecked
        {
            ulong hash = worldSeed;

            hash ^= (ulong)x * 0x9E3779B185EBCA87UL;
            hash = RotateLeft(hash, 27);

            hash ^= (ulong)y * 0xC2B2AE3D27D4EB4FUL;
            hash = RotateLeft(hash, 31);

            hash ^= generatorVersion * 0x165667B19E3779F9UL;

            return Mix(hash);
        }
    }

    private static ulong RotateLeft(ulong value, int amount)
    {
        return (value << amount) | (value >> (64 - amount));
    }

    private static ulong Mix(ulong value)
    {
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        value ^= value >> 31;
        return value;
    }
}
```

The exact hash is less important than keeping it stable after saves exist.

Store a generator version in the save:

```csharp
public const uint WorldGeneratorVersion = 1;
```

# 6. Generate the first overworld

Start with three fields:

1. Elevation.
2. Moisture.
3. Temperature.

For the first implementation, even simple layered noise is enough.

```text
Elevation = large-scale noise + medium-scale noise
Moisture = separate noise field
Temperature = latitude effect - elevation cooling + noise
```

Then classify biomes using thresholds:

```csharp
public static BiomeId ClassifyBiome(
    float elevation,
    float moisture,
    float temperature)
{
    if (elevation < 0.35f)
        return BiomeId.Ocean;

    if (elevation < 0.39f)
        return BiomeId.Beach;

    if (elevation > 0.82f)
        return BiomeId.Mountains;

    if (elevation > 0.68f)
        return BiomeId.Hills;

    if (moisture > 0.75f)
        return BiomeId.Swamp;

    if (moisture > 0.48f)
        return BiomeId.Forest;

    return BiomeId.Plains;
}
```

For an island world, multiply elevation by a falloff mask:

```text
final elevation = noise elevation × island falloff
```

The falloff decreases toward the boundaries of the map.

A simple radial version:

```csharp
private static float CalculateIslandFalloff(
    int x,
    int y,
    int width,
    int height)
{
    float nx = (x / (float)(width - 1)) * 2f - 1f;
    float ny = (y / (float)(height - 1)) * 2f - 1f;

    float distance = MathF.Sqrt(nx * nx + ny * ny);
    return Math.Clamp(1f - distance, 0f, 1f);
}
```

Later, distort this falloff with noise so the coastline is not circular.

# 7. Render the overworld simply

Do not begin with elaborate tile art. Give each biome a temporary color or debug sprite.

```csharp
private Color GetBiomeColor(BiomeId biome)
{
    return biome switch
    {
        BiomeId.Ocean => Color.DarkBlue,
        BiomeId.Beach => Color.SandyBrown,
        BiomeId.Plains => Color.OliveDrab,
        BiomeId.Forest => Color.DarkGreen,
        BiomeId.Swamp => Color.DarkOliveGreen,
        BiomeId.Hills => Color.SaddleBrown,
        BiomeId.Mountains => Color.Gray,
        _ => Color.Magenta
    };
}
```

The initial overworld interface only needs:

* Camera movement or zoom.
* A player marker or selection cursor.
* Arrow-key or WASD movement.
* A key to enter the selected cell.
* A debug panel showing cell values.

Example:

```text
World position: 22, 31
Biome: Forest
Elevation: 0.61
Moisture: 0.73
Temperature: 0.54
Local seed: 892361723...
```

# 8. Define the local-map format

Use a contiguous tile array again.

```csharp
public enum TerrainId : ushort
{
    DeepWater,
    ShallowWater,
    Sand,
    Dirt,
    Grass,
    Mud,
    Rock,
    Tree
}

[Flags]
public enum TileFlags : byte
{
    None = 0,
    BlocksMovement = 1 << 0,
    BlocksVision = 1 << 1,
    ContainsWater = 1 << 2
}

public sealed class LocalMap
{
    public const int Width = 64;
    public const int Height = 64;

    public WorldCoord WorldPosition { get; }

    public TerrainId[] Terrain { get; }
    public TileFlags[] Flags { get; }

    public LocalMap(WorldCoord worldPosition)
    {
        WorldPosition = worldPosition;
        Terrain = new TerrainId[Width * Height];
        Flags = new TileFlags[Width * Height];
    }

    public int GetIndex(int x, int y)
    {
        return y * Width + x;
    }
}
```

Separate arrays make it easier to add data later:

```text
Terrain[]
Elevation[]
Moisture[]
Light[]
Temperature[]
Occupancy[]
LiquidDepth[]
FireIntensity[]
```

Do not put all of these into a large tile class.

# 9. Generate local maps from overworld information

The overworld cell should act as the high-level instruction for the local generator.

```csharp
public interface ILocalMapGenerator
{
    LocalMap Generate(
        Overworld world,
        WorldCoord coordinate);
}
```

For the first pass, each biome can have a basic recipe.

## Forest

```text
Base terrain: grass
Tree probability: high
Rock probability: low
Clearings: created with local noise
```

## Plains

```text
Base terrain: grass
Tree probability: very low
Dirt patches: moderate
```

## Swamp

```text
Base terrain: mud
Shallow-water probability: moderate
Trees: scattered
```

## Mountains

```text
Base terrain: rock
Blocked rock formations: common
Sparse grass at lower elevation
```

A primitive local forest generator might look like:

```csharp
private void GenerateForest(
    LocalMap map,
    DeterministicRandom random)
{
    for (int y = 0; y < LocalMap.Height; y++)
    {
        for (int x = 0; x < LocalMap.Width; x++)
        {
            int index = map.GetIndex(x, y);

            map.Terrain[index] = TerrainId.Grass;
            map.Flags[index] = TileFlags.None;

            float roll = random.NextFloat();

            if (roll < 0.18f)
            {
                map.Terrain[index] = TerrainId.Tree;
                map.Flags[index] =
                    TileFlags.BlocksMovement |
                    TileFlags.BlocksVision;
            }
            else if (roll < 0.21f)
            {
                map.Terrain[index] = TerrainId.Rock;
                map.Flags[index] = TileFlags.BlocksMovement;
            }
        }
    }
}
```

This will look like random static at first. That is acceptable for proving the architecture.

The next improvement would be using coherent noise to form:

* Tree clusters.
* Clearings.
* Wet depressions.
* Rocky ridges.
* Paths.

# 10. Create an enter-and-exit state machine

Your game needs an explicit current world mode.

```csharp
public enum GameViewMode
{
    Overworld,
    LocalMap
}
```

The active game state might contain:

```csharp
public sealed class GameSession
{
    public GameViewMode ViewMode { get; set; }

    public WorldCoord PlayerWorldPosition { get; set; }
    public LocalCoord PlayerLocalPosition { get; set; }

    public LocalMap? ActiveLocalMap { get; set; }
}
```

Entering a square:

```csharp
public void EnterWorldCell()
{
    WorldCoord coordinate = _session.PlayerWorldPosition;

    _session.ActiveLocalMap =
        _localMapRepository.GetOrGenerate(coordinate);

    _session.PlayerLocalPosition =
        new LocalCoord(
            LocalMap.Width / 2,
            LocalMap.Height / 2);

    _session.ViewMode = GameViewMode.LocalMap;
}
```

Leaving a square:

```csharp
public void LeaveLocalMap()
{
    if (_session.ActiveLocalMap is not null)
    {
        _localMapRepository.Store(
            _session.ActiveLocalMap);
    }

    _session.ActiveLocalMap = null;
    _session.ViewMode = GameViewMode.Overworld;
}
```

For the first implementation, use a dedicated key to leave.

Later, walking off the north edge can move the player into the local map belonging to the northern overworld cell.

# 11. Use a repository for local maps

Do not make the screen or renderer responsible for generating maps.

```csharp
public interface ILocalMapRepository
{
    LocalMap GetOrGenerate(WorldCoord coordinate);

    void Store(LocalMap map);
}
```

The first implementation can store maps in memory:

```csharp
public sealed class InMemoryLocalMapRepository
    : ILocalMapRepository
{
    private readonly Dictionary<WorldCoord, LocalMap> _maps = new();
    private readonly Overworld _world;
    private readonly ILocalMapGenerator _generator;

    public InMemoryLocalMapRepository(
        Overworld world,
        ILocalMapGenerator generator)
    {
        _world = world;
        _generator = generator;
    }

    public LocalMap GetOrGenerate(WorldCoord coordinate)
    {
        if (_maps.TryGetValue(coordinate, out LocalMap? map))
            return map;

        map = _generator.Generate(_world, coordinate);
        _maps.Add(coordinate, map);

        return map;
    }

    public void Store(LocalMap map)
    {
        _maps[map.WorldPosition] = map;
    }
}
```

Later, this repository can transparently:

* Load a map from disk.
* Generate an untouched map.
* Apply saved mutations.
* Unload distant maps.
* Cache recently visited maps.

# 12. Separate generated state from changed state

A generated local map does not necessarily need to be saved.

Suppose the generator always produces the same forest. Saving all 4096 tiles is redundant.

Instead, you can eventually store only changes:

```text
Generated forest map
+
Removed tree at 14,22
Built wall at 16,20
Dropped chest at 18,19
Started fire at 25,31
=
Current local map
```

Initially, save complete visited maps because it is easier. Add mutation-only persistence once the core loop is stable.

# 13. First playable milestone

Your first milestone should contain only this:

## Overworld

* Generate a 64×64 overworld.
* Display biome colors.
* Move a player marker between cells.
* Prevent movement outside the world.
* Display selected cell information.

## Local map

* Press Enter to enter a world cell.
* Generate a deterministic 64×64 local map.
* Display terrain tiles.
* Move the player.
* Block movement through trees, rocks, and water.
* Press Escape to return to the overworld.

## Persistence test

* Enter a forest cell.
* Record its generated result.
* Leave it.
* Re-enter it.
* Confirm that it is identical.
* Cut down or remove one tree.
* Leave and return.
* Confirm that the change persists.

That is already a meaningful vertical slice.

# 14. Introduce simulation in layers

After the traversal loop works, add simulation gradually.

## Layer 1: Static terrain

* Terrain types.
* Movement blocking.
* Vision blocking.
* Water.
* Vegetation.
* Elevation.

## Layer 2: Objects

* Trees that can be cut.
* Rocks that can be mined.
* Containers.
* Doors.
* Harvestable plants.
* Dropped items.

## Layer 3: Creatures

* Position.
* Health.
* Movement.
* Basic wandering.
* Collision.
* Simple hostile or passive behavior.

## Layer 4: Turn scheduling

Give every actor an action cost:

```text
Walk: 100 energy
Run: 150 energy
Attack: 120 energy
Open door: 60 energy
Wait: 100 energy
```

The simulation advances based on actor readiness rather than rendering frames.

## Layer 5: Local environment

* Day and night.
* Temperature.
* Fire.
* Plant regrowth.
* Weather effects.
* Food decay.

## Layer 6: Inactive-map summaries

When the player leaves a local map, do not continue simulating every creature tile by tile.

Convert it to a simpler summary:

```text
Forest cell:
Deer population: 18
Wolf population: 3
Food availability: 0.71
Fire status: none
Human activity: low
```

When the player returns, advance the summary according to elapsed time and reconstruct the detailed local state.

## Layer 7: Regional simulation

Later, overworld cells can exchange:

* Migrating creatures.
* Trade.
* Disease.
* Fire.
* Weather.
* Faction influence.
* Refugees.
* Resource pressure.
* Military movement.

# Suggested project structure

```text
src/
├── Game.Client/
│   ├── Rendering/
│   ├── Input/
│   ├── Screens/
│   └── Camera/
│
├── Game.Simulation/
│   ├── Coordinates/
│   ├── World/
│   ├── LocalMaps/
│   ├── Terrain/
│   ├── Entities/
│   └── Time/
│
├── Game.Generation/
│   ├── Noise/
│   ├── Overworld/
│   ├── LocalMaps/
│   ├── Biomes/
│   └── Seeds/
│
├── Game.Persistence/
│   ├── Saves/
│   ├── Serialization/
│   └── Repositories/
│
└── Game.Tests/
    ├── Generation/
    ├── Coordinates/
    ├── Persistence/
    └── Simulation/
```

# Immediate implementation order

1. Create `WorldCoord`, `LocalCoord`, and `WorldCell`.
2. Generate a 64×64 overworld with simple elevation and biome rules.
3. Render the overworld as colored squares.
4. Add a movable overworld player marker.
5. Create `LocalMap` and terrain arrays.
6. Generate a local map based on the selected biome.
7. Switch between overworld and local-map modes.
8. Add local collision and player movement.
9. Cache local maps in memory.
10. Add one persistent terrain change.
11. Save the world seed, player position, and changed maps.
12. Add the first creature only after all of the above works.

The most important early boundary is:

> The overworld cell describes the region; the local map represents its detailed current state.

That separation lets the world become extremely large while only fully simulating the small portion around the player.
