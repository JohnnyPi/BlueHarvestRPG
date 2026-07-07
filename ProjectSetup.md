# Recommendation

For a **new large-scale 2D top-down roguelike**, I recommend:

> **C# + .NET 10 LTS + MonoGame 3.8.4.1**, with a custom simulation-first architecture and MonoGame used primarily for rendering, input, audio, and the application loop.

Given your C# experience, Windows target, preference for data-driven systems, and existing “Lego Box” component philosophy, this is the best balance of performance, development speed, architectural control, and likelihood of actually completing the game.

.NET 10 is the current active LTS release and is supported until November 2028. MonoGame 3.8.4.1 supports .NET 10 and provides rendering, input, audio, and content-processing foundations without forcing your world into a scene hierarchy. ([Microsoft][1])

## Recommended stack

| Layer              | Recommendation                                                      |
| ------------------ | ------------------------------------------------------------------- |
| Language/runtime   | **C# on .NET 10 LTS**                                               |
| Game framework     | **MonoGame 3.8.4.1 WindowsDX**                                      |
| Graphics           | Direct3D 11 through MonoGame, HLSL shaders                          |
| Simulation         | Pure .NET class libraries, independent of MonoGame                  |
| Entity model       | Data-oriented component stores for actors; dense arrays for terrain |
| World structure    | Chunked 2D grids, region graphs, simulation zones                   |
| Authored data      | YAML using YamlDotNet                                               |
| Compiled game data | MessagePack or custom versioned binary packs                        |
| Save metadata      | SQLite through Microsoft.Data.Sqlite                                |
| Chunk persistence  | Compressed binary chunk snapshots                                   |
| Compression        | Zstandard                                                           |
| Developer UI       | ImGui.NET inside the game                                           |
| External editors   | Avalonia for world/content tools                                    |
| Testing            | xUnit, property tests, headless simulation tests                    |
| Profiling          | BenchmarkDotNet, dotnet-trace, Visual Studio profiler, RenderDoc    |
| Logging            | Structured logging such as Serilog                                  |

Use MonoGame’s current stable release rather than the 3.8.5 previews. MonoGame’s roadmap still identifies 3.8.4 as the live release, while Vulkan, DirectX 12, and the new content system remain preview work for 3.8.5 and later. Direct3D 11 is more than sufficient for a sophisticated 2D game. ([MonoGame Documentation][2])

# Why this fits the project

The difficult part of this game will not be drawing sprites. It will be:

* Simulating populations, animals, factions, settlements, weather, resources, inventories, relationships, quests, and environmental processes.
* Updating a very large world without processing every object every frame.
* Saving and loading that world reliably.
* Keeping the simulation deterministic and testable.
* Building enough debugging tools to understand why something happened.

A full editor-oriented engine can help with presentation, but it can also tempt you to make every creature, item, tile, effect, and settlement an engine object. That is precisely what you should avoid.

MonoGame describes itself as a “bring your own tools” framework rather than a scene-editor engine. For this project, that is an advantage: it lets you build the world model that the simulation needs instead of adapting the simulation to an engine scene tree. ([MonoGame Documentation][3])

# Architecture

The authoritative world should live in a standalone simulation library:

```text
Game.Client
├── Presentation
│   ├── MonoGame renderer
│   ├── Camera
│   ├── Input mapping
│   ├── Audio
│   ├── UI
│   └── Visual effects
│
├── Simulation
│   ├── World chunks
│   ├── Actors and components
│   ├── AI and goals
│   ├── Ecology
│   ├── Economy
│   ├── Combat
│   ├── Factions
│   ├── Time and scheduling
│   └── Event journal
│
├── Content
│   ├── YAML definitions
│   ├── Validation
│   ├── Reference resolution
│   └── Binary content compiler
│
├── Persistence
│   ├── World manifest
│   ├── Chunk snapshots
│   ├── Entity records
│   └── Mutation/event log
│
└── Tools
    ├── In-game inspectors
    ├── World viewer
    ├── Content editor
    └── Simulation runner
```

The `Simulation` project should contain **no MonoGame types**. It should be possible to run ten simulated years from an xUnit test or command-line program without opening a window.

The renderer receives immutable or short-lived render snapshots. Input travels in the opposite direction as commands:

```text
Player input
    ↓
Input action
    ↓
Simulation intent
    ↓
Simulation resolves the action
    ↓
World state changes
    ↓
Render snapshot
    ↓
MonoGame displays the result
```

This gives you pause, single-step, fast-forward, replays, deterministic testing, AI-controlled players, and potential multiplayer without rewriting the fundamental command system.

# World representation

## Do not make tiles into entities

Terrain belongs in dense chunk arrays:

```csharp
public sealed class WorldChunk
{
    public ChunkCoordinate Coordinate { get; init; }

    public ushort[] TerrainIds { get; init; }
    public byte[] Elevation { get; init; }
    public byte[] Moisture { get; init; }
    public byte[] Temperature { get; init; }
    public byte[] Light { get; init; }
    public uint[] Occupancy { get; init; }
}
```

A starting chunk size of **64×64 cells** is reasonable. Benchmark 32×32, 64×64, and 128×128 before committing.

Use separate arrays rather than one large `Tile` object per location. Systems that only need moisture should touch the moisture array, not load every property associated with every tile.

## Use components for actors

Creatures, NPCs, vehicles, projectiles, dropped objects, settlements, and important world objects can use your component-oriented model:

```text
Entity
├── Position
├── Creature
├── Health
├── Inventory
├── Needs
├── FactionMember
├── Perception
├── GoalState
└── RenderAppearance
```

You do not necessarily need a third-party ECS. A custom sparse-set or indexed component store may fit your stable-ID, event-driven design better.

Use engine-style entities for **active simulation subjects**, not for:

* Every tile.
* Every blade of grass.
* Every unit of stored grain.
* Every distant citizen.
* Every atmospheric particle.

# Simulation scaling

A large roguelike needs multiple simulation levels.

| Level     | Typical scope                         | Update style                                   |
| --------- | ------------------------------------- | ---------------------------------------------- |
| Immediate | Player vicinity                       | Full movement, combat, perception and physics  |
| Local     | Nearby chunks                         | Simplified AI and needs                        |
| Regional  | Settlements and surrounding territory | Batched economy, ecology and faction updates   |
| Strategic | Entire world                          | Statistical models and scheduled events        |
| Dormant   | Unloaded regions                      | No continuous update; advance when reactivated |

For example, a nearby farmer can have a location, inventory, path, hunger level, current task, relationships, and perception events. A farmer thousands of cells away may temporarily exist as part of a settlement workforce aggregate.

When that region becomes active, the aggregate can expand into individuals using stable identities and deterministic reconstruction.

This is more important than choosing Rust versus C#. No language can save a design that attempts to run full perception and pathfinding for every creature in the world every frame.

## Scheduling

Use several clocks:

* Rendering: 60 FPS.
* Input sampling: every rendered frame.
* Immediate simulation: fixed ticks, perhaps 10–30 ticks per second.
* Needs and status updates: every few simulation ticks.
* Ecology and settlement production: every in-game hour or day.
* Strategic faction updates: scheduled events rather than continuous polling.

Every system should declare how often it genuinely needs to run.

# Modern rendering

MonoGame can provide modern-looking 2D rendering, but you will build the pipeline rather than enabling a checkbox.

I would use:

1. **Chunk-based terrain rendering**
   Generate one or a few vertex buffers per visible chunk rather than issuing one draw call per tile.

2. **Instanced object rendering**
   Batch trees, grass clumps, rocks, debris, and repeated props.

3. **Layered material maps**
   Terrain ID, variation, moisture, snow, damage, blood, burning, and contamination can be separate data layers interpreted by shaders.

4. **2D lighting buffers**
   Render albedo, normal, emissive, occlusion, and light accumulation targets.

5. **Fog of war and visibility**
   Maintain separate explored, currently visible, sensed, and remembered buffers.

6. **Environmental effects**
   Rain, wind, smoke, fog, fire, water distortion, heat shimmer, pollen, ash, and cloud shadows.

7. **Elevation-aware presentation**
   Even in a top-down game, support height, cliffs, roofs, bridges, canopies, overhang indicators, and layered interiors.

8. **Post-processing**
   Color grading, bloom used carefully, vignette, underwater treatment, weather desaturation, and damage effects.

For the gameplay UI, build a retained-mode interface suited to the game. Use ImGui.NET for inspectors, profiling windows, chunk visualizers, AI traces, and developer commands—not necessarily as the final player-facing UI.

# Controls

Use an action-mapping layer instead of reading keys directly:

```text
MoveNorth
MoveSouth
MoveWest
MoveEast
Interact
PrimaryAction
SecondaryAction
OpenInventory
Wait
Pause
FastForward
Inspect
ToggleCombatMode
```

Each action can bind to:

* Keyboard.
* Mouse.
* Controller.
* Accessibility alternatives.
* AI commands.
* Replay input.

This also lets you support both traditional roguelike tile commands and smoother modern movement without changing the simulation interface.

The player should submit an intent such as `Move`, `Attack`, `Interact`, or `UseItem`. The simulation decides whether it succeeds and what consequences occur.

# Data and persistence

## Authored content

Keep human-authored definitions in YAML:

```text
content/
├── creatures/
├── items/
├── materials/
├── terrain/
├── biomes/
├── factions/
├── professions/
├── structures/
├── actions/
├── effects/
└── worldgen/
```

At build or startup:

```text
YAML
  → schema validation
  → reference resolution
  → inheritance/composition
  → semantic validation
  → compiled binary content pack
```

Do not repeatedly parse hundreds of YAML files during normal play. YAML should be the authoring format; a compact binary format should be the runtime format.

## Saves

Use a hybrid save system:

* SQLite for the world manifest, stable IDs, region index, important entities, settlements, quests, relationships, and event journal.
* Compressed binary files for large chunk arrays.
* Procedural regeneration for unchanged terrain.
* Mutation records for changes such as excavated cells, constructed walls, fires, depleted deposits, and destroyed structures.
* Version numbers and migrations from the beginning.

Avoid storing one SQL row per terrain cell.

# Suggested implementation order

## Phase 0 — Performance proof

Build a headless benchmark containing:

* A 4096×4096-cell procedural world.
* Chunk loading and unloading.
* 10,000 active actors.
* 100,000 abstract distant actors.
* Needs, movement, scheduling, and simple ecology.
* Save and reload.
* Deterministic replay from a seed and command log.

This reveals whether the data model works before rendering complicates the problem.

## Phase 1 — Playable foundation

Implement:

* MonoGame window and camera.
* Action-mapped controls.
* Chunk renderer.
* Player movement.
* Collision and interaction.
* YAML content loading.
* Save/load.
* Debug inspector.

## Phase 2 — Simulation framework

Add:

* Simulation tiers.
* Event bus.
* Scheduled jobs.
* Needs and activities.
* Local and regional pathfinding.
* Inventories and material transformations.
* Basic settlements and ecology.

## Phase 3 — Modern presentation

Add:

* Lighting and normal maps.
* Weather.
* Particles.
* Smooth camera motion.
* Gamepad controls.
* Animation state machines.
* Fog of war.
* Sound propagation and ambient audio.

## Phase 4 — World depth

Add:

* Factions.
* Economy.
* Quest-driven AI.
* Relationships.
* Procedural structures.
* Long-term ecological change.
* Strategic simulation.
* Content and world-editing tools.

The central design rule should be:

> **Build a simulation that happens to have a renderer, not a renderer containing a simulation.**

[1]: https://dotnet.microsoft.com/en-us/platform/support/policy "The official .NET support policy | .NET"
[2]: https://docs.monogame.net/roadmap/ "MonoGame Roadmap | MonoGame "
[3]: https://docs.monogame.net/articles/index.html "Documentation | MonoGame "
[4]: https://bevy.org/news/bevy-0-19/ "Bevy 0.19"
[5]: https://godotengine.org/ "Godot Engine - Free and open source 2D and 3D game engine"
[6]: https://docs.godotengine.org/en/4.5/tutorials/performance/thread_safe_apis.html "Thread-safe APIs — Godot Engine (4.5) documentation in English"
