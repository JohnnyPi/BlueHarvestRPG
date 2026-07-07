You now have the essential **world traversal and persistence proof**. The next phase should establish that the local maps belong to one coherent world and can support persistent entities and time.

I would proceed in this order.

# 1. Make local maps connect geographically

At the moment, each overworld cell probably behaves like a separate room. The next milestone should make adjacent cells feel continuous.

When the player walks off the edge of a local map:

```text
North edge → enter northern overworld cell at its south edge
East edge  → enter eastern overworld cell at its west edge
South edge → enter southern overworld cell at its north edge
West edge  → enter western overworld cell at its east edge
```

For example:

```csharp
public readonly record struct MapTransition(
    WorldCoord DestinationWorld,
    LocalCoord DestinationLocal);
```

If the player leaves through local position `(63, 24)` on the east side:

```text
World position: (10, 8) → (11, 8)
Local position: (63, 24) → (0, 24)
```

## Acceptance criteria

* Walking off any map edge enters the correct neighboring cell.
* The player appears at the corresponding edge position.
* Returning produces the same map and preserves previous changes.
* World-boundary cells cannot send the player outside the overworld.
* The transition does not depend on generation order.

This immediately makes your 64×64 overworld feel like a navigable continuous space rather than a menu of maps.

# 2. Add edge-generation contracts

Adjacent maps should agree about what crosses their boundaries.

Without this, you will eventually get:

* A river ending abruptly at a map border.
* A road entering one map but not the next.
* Water on one side and a cliff wall on the other.
* A forest opening that does not align with its neighbor.

Each overworld cell should describe its boundary connections.

```csharp
[Flags]
public enum ConnectionFlags
{
    None = 0,
    NorthRoad = 1 << 0,
    EastRoad = 1 << 1,
    SouthRoad = 1 << 2,
    WestRoad = 1 << 3,
    NorthRiver = 1 << 4,
    EastRiver = 1 << 5,
    SouthRiver = 1 << 6,
    WestRiver = 1 << 7
}
```

A better long-term representation stores exact boundary portals:

```csharp
public readonly record struct EdgeConnection(
    Direction Edge,
    int LocalOffset,
    ConnectionType Type,
    int Width);
```

For example:

```text
River enters north edge at x = 21, width = 4
River exits east edge at y = 43, width = 5
Road enters west edge at y = 12, width = 2
```

These values should be derived from a shared regional feature, not chosen independently by both local maps.

Start with roads or paths before rivers. Roads are easier to debug.

# 3. Introduce stable entities

Your terrain is persistent; now make non-terrain objects persistent.

Begin with three entity types:

* Player.
* One stationary object, such as a harvestable tree or chest.
* One moving creature.

Use stable IDs:

```csharp
public readonly record struct EntityId(ulong Value);
```

A minimal entity model could be:

```csharp
public sealed class Entity
{
    public EntityId Id { get; init; }

    public WorldCoord WorldPosition { get; set; }
    public LocalCoord LocalPosition { get; set; }

    public bool BlocksMovement { get; set; }
    public bool IsActive { get; set; }
}
```

You can later move toward component stores:

```text
Position
Renderable
Collider
Actor
Health
Inventory
AIState
Faction
Needs
```

## Important distinction

Terrain represents the underlying map:

```text
Grass
Sand
Rock floor
Shallow water
```

Entities represent things occupying it:

```text
Tree
Boulder
Creature
Chest
Dropped sword
Door
```

Avoid encoding trees, doors, creatures, and containers directly into the terrain enum.

# 4. Add a real simulation clock

Before adding sophisticated AI, establish how time advances.

A roguelike benefits from separating:

* Rendering time.
* Input time.
* Simulation time.
* World calendar time.

A simple first model is an energy scheduler.

```csharp
public sealed class ActorTurnState
{
    public int Energy { get; set; }
    public int Speed { get; set; } = 100;
}
```

Each simulation step:

```csharp
actor.Energy += actor.Speed;
```

An actor acts when it has enough energy:

```csharp
const int StandardActionCost = 100;

if (actor.Energy >= StandardActionCost)
{
    PerformAction(actor);
    actor.Energy -= StandardActionCost;
}
```

Possible action costs:

| Action                         | Cost |
| ------------------------------ | ---: |
| Wait                           |  100 |
| Walk                           |  100 |
| Move through difficult terrain |  140 |
| Open door                      |   60 |
| Harvest                        |  200 |
| Attack                         |  120 |
| Sprint                         |  150 |

Do not tie simulation actions directly to frames.

## First implementation

* The player moves.
* One creature wanders.
* Each player action advances simulation time.
* The creature receives turns according to speed.
* Pausing rendering does not alter simulation state.
* Waiting advances time intentionally.

# 5. Add one complete interaction loop

Do not add twenty object types. Build one interaction from beginning to end.

A good first example is harvesting a plant or cutting a tree:

```text
Locate tree
→ interact
→ spend time
→ remove or alter tree entity
→ create wood item
→ place item in inventory or on ground
→ autosave
→ leave map
→ return
→ tree remains removed
→ wood remains present
```

This tests:

* Entity persistence.
* Action costs.
* Inventory.
* Item creation.
* Map mutation.
* Save compatibility.
* Player feedback.

A chest is another good candidate:

```text
Closed chest
→ open chest
→ view contents
→ take item
→ leave
→ return
→ chest remains open and item remains taken
```

# 6. Add field of view and map memory

Once maps contain actors and interactable objects, the player should not see everything.

Track at least three visibility states:

```csharp
public enum VisibilityState : byte
{
    Unseen,
    Remembered,
    Visible
}
```

Per local tile:

```text
Unseen:
    Never observed

Remembered:
    Previously observed, but not currently visible

Visible:
    Currently within line of sight
```

Use a standard shadowcasting or symmetric shadowcasting algorithm.

Keep visibility separate from rendering. The simulation or perception system calculates it; the renderer displays the result.

Later you can distinguish:

* Direct sight.
* Heard activity.
* Smell.
* Thermal detection.
* Faction reports.
* Map knowledge.

For now, direct sight and remembered terrain are enough.

# 7. Improve local-map generation through passes

Your generator will become much easier to extend if it is organized as a pipeline.

Instead of:

```text
GenerateForestMap()
```

use passes:

```text
1. Initialize base terrain
2. Generate local elevation
3. Apply water
4. Apply soil or ground cover
5. Place vegetation clusters
6. Add rocks and obstacles
7. Connect boundary features
8. Place points of interest
9. Validate navigability
10. Spawn initial entities
```

A possible interface:

```csharp
public interface IGenerationPass
{
    void Execute(
        LocalMap map,
        LocalGenerationContext context);
}
```

The context can contain:

```csharp
public sealed class LocalGenerationContext
{
    public ulong Seed { get; init; }
    public WorldCoord WorldCoordinate { get; init; }
    public WorldCell WorldCell { get; init; }
    public IReadOnlyList<EdgeConnection> Connections { get; init; }
}
```

This prevents biome generators from turning into giant, difficult-to-maintain methods.

# 8. Add spatial coherence inside local maps

Pure random placement usually produces visual noise.

Replace independent placement rolls with coherent structures:

* Tree density field.
* Wetness field.
* Rockiness field.
* Clearing masks.
* Ridge lines.
* Distance from water.
* Distance from roads.

For a forest:

```text
tree suitability =
    tree-density noise
    × soil suitability
    × moisture suitability
    × distance from road
```

Then place trees only where suitability exceeds a threshold.

This produces patches and clearings instead of static.

You can also use a lower-frequency field to choose regions and a higher-frequency field for variation:

```text
density =
    70% broad forest structure
    + 30% fine variation
```

# 9. Add generator validation

Procedural generation should not merely produce a result. It should test whether that result is playable.

Start with simple validation:

* Entry location is walkable.
* Each required edge connection reaches the interior.
* At least one connected walkable region contains the player spawn.
* The map does not contain accidental isolated pockets unless intentional.
* The player cannot spawn inside an entity.
* Water and cliffs obey basic constraints.

Run flood fill from the player entry point:

```text
Walkable tiles reachable: 2,841
Total walkable tiles: 3,020
Reachability: 94.1%
```

You can then:

* Accept the map.
* Repair blocked passages.
* Retry a generation pass.
* Regenerate using a derived retry seed.

Never silently use an unpredictable random retry. Derive retries deterministically:

```text
seed = Hash(originalSeed, retryNumber)
```

# 10. Create debugging overlays now

Procedural systems become difficult to understand quickly. Add overlays before the generator becomes sophisticated.

Useful toggles:

* Terrain IDs.
* Collision.
* Local elevation.
* Moisture.
* Temperature.
* Generation noise.
* Entity positions.
* Entity IDs.
* Pathfinding costs.
* Connected regions.
* Current visibility.
* Map boundaries.
* Generation pass output.
* World and local coordinates.

A debug tile inspector should show something like:

```text
Global tile: 1482, 2031
World cell: 23, 31
Local tile: 10, 47

Terrain: Grass
Elevation: 0.54
Moisture: 0.68
Walk cost: 100
Visible: Yes
Entity: Oak Tree #18842
Generation pass: Vegetation
```

These tools will save far more time than they cost.

# 11. Strengthen persistence before adding more systems

Your autosave works, but persistence should now become versioned and testable.

Store:

```csharp
public sealed class SaveHeader
{
    public int SaveFormatVersion { get; init; }
    public int GeneratorVersion { get; init; }
    public ulong WorldSeed { get; init; }
    public DateTime CreatedUtc { get; init; }
}
```

Add tests for:

* Save and reload with no changes.
* Terrain mutation persistence.
* Entity creation and removal.
* Player position.
* Cross-map transition.
* Missing or damaged local-map data.
* Old save-version rejection or migration.
* Deterministic regeneration of an unmodified map.

Use atomic save replacement where possible:

```text
Write save.tmp
→ flush and close
→ rename current save to backup
→ rename save.tmp to current save
```

This reduces the chance that an interrupted autosave destroys the save.

# 12. Begin inactive-map handling

Do not fully simulate every previously visited local map.

For now, use three states:

```csharp
public enum MapActivityState
{
    Active,
    Cached,
    Unloaded
}
```

## Active

The player is present. Full simulation runs.

## Cached

Recently visited. The map remains in memory, but detailed simulation is paused or reduced.

## Unloaded

Saved to disk or represented by procedural state plus mutations.

Initially, only the current map needs active creature AI.

When leaving a map, record:

```text
Simulation timestamp when unloaded
Creature state
Object state
Outstanding scheduled events
```

When re-entering, calculate elapsed world time and apply a simple catch-up step.

For example:

```text
Map was inactive for 6 game hours
Plant growth advanced by 6 hours
Dropped food decayed by 6 hours
Wandering creature moved abstractly or remained local
```

Do not try to replay thousands of missing turns individually.

# 13. Add pathfinding at two scales

You will eventually need:

## Local pathfinding

Within a 64×64 map:

* A*.
* Movement costs.
* Collision.
* Difficult terrain.
* Doors.
* Avoidance.

## Overworld pathfinding

Between world cells:

* Travel cost by biome.
* Roads.
* Rivers.
* Mountains.
* Danger.
* Known versus unknown locations.

Keep these separate.

An NPC traveling to a distant settlement should not pathfind through every local tile across the world. It should:

```text
Overworld route
→ select next world cell
→ local route only when active near the player
```

Start with local A* for your first wandering or pursuing creature.

# 14. Recommended next milestone

I would define the next milestone as:

## Milestone: Continuous World and First Actor

### World traversal

* Walking off local-map edges transitions to neighboring cells.
* Corresponding entry position is maintained.
* Neighboring maps share at least one boundary feature.
* Transitions work after saving and reloading.

### Entities

* Stable entity IDs.
* One persistent non-player creature.
* One harvestable or openable object.
* Ground items.
* Basic inventory.

### Simulation

* Player actions advance simulation time.
* Creature acts through a scheduler.
* Wait action.
* Movement and interaction action costs.
* Current world time displayed in debug UI.

### Perception

* Field of view.
* Unseen, remembered, and visible tiles.
* Vision-blocking terrain and entities.

### Generation

* Generation-pass pipeline.
* Coherent vegetation placement.
* Walkability validation.
* Debug overlays for generation fields and connectivity.

### Persistence

* Entities persist.
* Inventory persists.
* Cross-cell player position persists.
* Save and generator versions are stored.
* Autosaves use safe replacement.

# What I would postpone

Do not add these yet:

* Complex combat.
* Factions.
* Detailed economics.
* Full settlement simulation.
* Weather fronts.
* Breeding populations.
* Sophisticated dialogue.
* Quest generation.
* Multiplayer.
* Hundreds of items.
* Large-scale ecological simulation.

Those systems will depend on entities, time, movement, persistence, and inactive-map handling. Building them first would likely force later rewrites.

The best immediate sequence is:

> **Continuous map transitions → stable entities → simulation clock → one complete interaction → field of view → procedural-generation pipeline → inactive-map catch-up.**

That gives you a small but genuine roguelike world rather than only a map viewer.
