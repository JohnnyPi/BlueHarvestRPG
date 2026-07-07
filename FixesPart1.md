## The three things that matter most

**1. Non-player entities are never rendered.** `RenderSnapshot` carries `CellData`, `PlayerX/Y`, and a debug string, but no entity list, and `WorldRenderer.Draw` only paints terrain + the player rect + selection + menu. The wandering creatures and harvestable trees you spawn are invisible — the local-map snapshot even locates the creature purely to print its energy into debug text. Everything downstream (FOV, combat, inspect) assumes entities are visible, so this is the first correctness gap to close.

**2. There is no keyboard player movement, and the direct-move pathway is dead.** WASD is bound to *camera* pan (`InputAction.PanNorth/...`). There is no `InputAction.MoveNorth`, `HandleSimulationActions` never queues a move, and nothing else queues `GameIntent.MoveNorth/South/East/West`. That means `SimulationHost.TryMove`, `GameSession.TryMoveOverworld/TryMoveLocal(delta)`, and those four intents are unreachable. Movement is exclusively right-click → context menu → "Move Here" → pathfind. For a roguelike this is both a missing feature and a large stranded code path.

**3. Loading a save can regenerate a *different* world.** New-game generation uses `new OverworldGenerator(new BiomeClassifier(bundle.BiomeRules))` (rules from `biome_rules.yaml`). But `SaveManager.TryLoad` regenerates with `new OverworldGenerator()`, which falls back to `BiomeClassifier.CreateDefault()` and default `BiomeRulesDefinition`. If anyone edits the biome-rules content, a loaded save produces different biomes, which shifts road eligibility (`SupportsRoads`) and desynchronizes stored local maps (keyed by coordinate) from the regenerated overworld. This is a silent determinism break. The classifier/rules must be threaded into load, and ideally the resolved rules (or a content hash) should be persisted alongside the seed.

## Correctness bugs and fragile assumptions

- **Player can spawn on a blocking tile.** `EnterWorldCell` drops the player at local (32,32) with no walkability check. In Ocean (all water) or Mountains (mostly rock) the player lands on a blocking tile and can barely move; Esc still escapes, so it's not a hard softlock, but it's clearly wrong. Add a nearest-walkable search from the intended entry point.
- **Edge transitions can dead-end.** `TryTransitionToMap` refuses if the mirrored destination tile is blocked, with no search for an adjacent open tile, so you can get "walled" at a map seam even though the crossing is legal.
- **Entity actor/energy state is not persisted.** `EntitySaveData` saves id/kind/pos/blocks/active only. On load, `ToEntity` never restores `Actor`, so `TurnScheduler` lazily recreates a default `ActorTurnState`. Harmless today because all creatures share stats, but any speed/energy differentiation will silently reset on load.
- **Deterministic IDs are collision-prone.** `CreateDeterministicId` uses `kindSalt = (uint)kind + (uint)ordinal`, so (kind=1,ord=1) and (kind=2,ord=0) collide once ordinals are used. And the player-collision guard `if (value == Player.Value) value = 1;` can itself collide with a real entity that hashed to 1. Encode kind and ordinal in separate bit-fields and reserve a player ID range.
- **Spawn placement ignores occupancy.** `PickWalkablePosition` checks terrain flags but not existing entities; two entities can hash to the same tile and both `Add` (the store only rejects duplicate *IDs*, not positions). `ReplaceAll` also skips the duplicate-ID check that `Add` enforces.
- **Energy recovery truncates.** `Speed / EnergyGranularity` is integer division, so speed 105 behaves identically to 100 and fractional energy is lost each step. Accumulate in a finer unit or carry the remainder.
- **Building a context menu has side effects.** `ContextMenuBuilder.AppendBorderTransitions` → `session.CanTransitionAcrossEdge` → `_localMapRepository.GetOrGenerate(neighbor)`, so right-clicking a border tile generates (and spawns entities into) the neighboring map. Menu construction should be pure; move the "is this crossing valid" probe behind a read-only query.
- **Startup crashes on corrupt data.** Neither `TryLoad` nor `PersistentLocalMapRepository.LoadSavedMaps` wraps deserialization; one bad `autosave.json` or `maps/*.json` throws and kills launch. Loaded terrain/entity indices also aren't range-checked before being cast to enums and used as color-array indices in the renderer.
- **`AdvanceMovement`'s local out-of-bounds branch is dead.** `BuildPathTo` clamps to `LocalMap.Width/Height`, so queued local paths never contain an out-of-bounds step; the transition-on-step logic there can't fire (border crossing is handled separately by `_transitionOnArrival`).

## Architecture and maintainability

- **`GameSession` is a god object.** It owns view mode, both position pairs, the movement queue, world transitions, terrain edits, pathfinding invocation, the entity registry, and player turn state. Movement, transition, and terrain mutation should be separate services so combat/inventory/interaction don't keep piling onto one class.
- **The player is not an entity in any store.** `PlayerEntity` is rebuilt on every access via `EntityFactory.CreatePlayer`, and player turn state lives separately in `GameSession.PlayerTurnState`. That's two sources of truth and forces special-casing everywhere FOV, targeting, and AI will need "the player."
- **The turn system is hardcoded and single-map.** `TurnScheduler.RunUntilPlayerReady` conflates player-energy recovery with a creature-action loop, filters on `EntityKind.WanderingCreature`, only simulates `session.ActiveLocalMap`, and uses an ad-hoc "highest energy first, cap 64" scheme rather than a real initiative queue. NPCs, off-screen simulation, and varied speeds will all require rewriting it.
- **Intent handling is inconsistent.** Some actions flow through the queue (`Wait`, `EnterCell`, `RemoveTerrain`); others bypass it (`SaveGame`, exit, zoom, camera). `GameIntent.SaveGame` is dead because saving is called directly, and `GameIntent.InspectSelected` is a literal no-op. Route everything through one command/action pipeline so "player intent" is cleanly separated from simulation, as you intend.
- **Simulation knows about rendering/UI.** `SimulationHost.BuildRenderSnapshot` lives in `Game.Simulation`, emits render-shaped data, and even embeds the UI hint string `"Space wait, WASD pan, wheel zoom..."`. That control text belongs in the client. The snapshot is also rebuilt every frame — a fresh `ushort[4096]` plus record plus string concatenation at 60fps even when nothing changed — so add a dirty flag and let the renderer read terrain directly.
- **Namespace/type name collision.** `Game.Generation.Overworld` (namespace) vs `Game.Simulation.World.Overworld` (type) is why `SimulationOverworld = ...` aliases are sprinkled everywhere. Rename the namespace (e.g. `Game.Generation.WorldGen`) or the type.
- **Input is untestable.** `InputMapper.Sample` calls the static `Keyboard.GetState()`/`Mouse.GetState()` directly; abstract an input source so input mapping can be unit-tested.
- **Naming.** `RougeGame`, `Window.Title = "Rouge"`, and the `"Rouge"` save folder look like an unintended misspelling of "Rogue" — worth deciding now, since it's baked into save paths.

## Dead / write-only code (safe to prune or wire up)

`WorldChunk`, `ChunkCoordinate`, `GlobalTileCoord`, and `CoordinateMath.ToGlobalTile` appear entirely unused. `WorldCell.HasLocalChanges` and `WorldCell.ConnectionFlags` are written in several places but never read; `EdgeConnection.Mirrors` is never called; `SimulationClock.SimTickCount` is written but unread (and duplicates `SimulationHost._actionTickCount`). The `ZoomIn/ZoomOut` input actions and `_wheelBindings` are redundant because zoom is driven straight off `WheelDelta` in the camera. `EntityKind.HarvestableTree` and `ActionCostTable.Harvest` exist with no harvest action implemented — and note the collision between the tree *entity* and the `TerrainId.Tree` *terrain* that `TryRemoveTerrainAtPlayer` actually chops.

## Save/load and persistence

Beyond the determinism break above, there are **two overlapping persistence mechanisms** for the same data: `autosave.json` carries a full `LocalMaps` list, while `PersistentLocalMapRepository` also writes per-map `maps/*.json` on every `Store`. On load, `CreateSimulationHost` reads the per-map files *and* copies the in-memory maps from the autosave over them. That's redundant writes, a divergence risk, and muddled ownership. Pick one: either the autosave is the source of truth for maps, or the per-map files are, not both. Also, `GeneratorVersion` and `FormatVersionNumber` are saved but never validated or migrated on load, so there's no version-gating story for when generation changes.

## UX / accessibility layer

This is where the "modern and accessible" goal is furthest from the code today.

- **Movement is right-click-menu-only.** Left-click does nothing. Modern feel wants left-click-to-move (with a path preview on hover) and right-click reserved for the context menu, plus keyboard movement (numpad/arrows/hjkl) for tactical play.
- **No camera follow.** `CenterOn` runs once behind a `_cameraCentered` flag; after that the player can walk off-screen with no "recenter" and no follow mode.
- **`TileFlags.BlocksVision` is defined but unused** — there's no FOV, no fog of war, no explored-memory, and the renderer shows the whole map.
- **No message log, no tooltips, no inspect.** `InspectSelected` is a stub, there's no hover readout of terrain/biome/coords (only the debug block), no action or attack preview, and no combat/event log — all of which roguelike players lean on heavily.
- **Silent font failure.** If `Fonts/Arial` fails to load, `_font = null` and every label and the debug panel render blank with no indication; ship a bitmap fallback.
- **Selection lingers.** Confirming a menu action never clears `_selection`, so the highlight persists until the next cancel.

Remapping via `controls.yaml` is a nice touch, and the zoom-to-cursor math in `CameraController.ApplyZoom` is actually exact (I checked it) — those are good foundations to build the rest of the UX on.

## What will break as you add systems

FOV needs per-tile visibility + seen-memory in the snapshot and a real map origin (the player-as-entity problem). NPC AI and combat need a generalized initiative scheduler and stat/HP components — today `Entity` is a fixed field bag with only `BlocksMovement`/`Actor`, so inventory/stats/factions will either bloat it or force a component refactor. Pathfinding is unweighted BFS with no terrain cost (roads should be cheap, mud/water costly). Procedural structures have the `IGenerationPass` seam (good) but only one pass and no room/structure vocabulary. Weather/time-of-day and quests have no data model or clock hooks yet. And cross-map terrain continuity doesn't exist beyond roads — adjacent maps have independent noise with a hard, only-shaded seam, and the renderer is flat colored squares with no autotiling.

---

## Phased expansion plan

I'd sequence this as stabilize → make visible/controllable → core roguelike loop → depth. Each phase is small and independently shippable.

**Phase 0 — Stabilization and cleanup (do before anything else)**
- *Goal:* remove ambiguity and the determinism/persistence traps so later work is trustworthy.
- *Systems/files:* `SaveManager`, `GameBootstrap`, `PersistentLocalMapRepository`, `BiomeClassifier`/`OverworldGenerator` wiring; delete dead code (`WorldChunk`, `ChunkCoordinate`, `GlobalTileCoord`, `CoordinateMath`, redundant zoom actions, `SimTickCount`); rename the `Generation.Overworld` namespace.
- *Risks:* touching save code can invalidate existing saves — add a format-version check and fail gracefully.
- *Acceptance:* biome rules are threaded through load (or persisted) so a save round-trips to a byte-identical overworld; corrupt files are caught and reported instead of crashing; exactly one map persistence path exists; solution builds with no unused public types.

**Phase 1 — Make the world visible and directly controllable**
- *Goal:* render entities and add keyboard + left-click movement.
- *Systems/files:* extend `RenderSnapshot` with an entity list (or have the renderer read the entity store), add drawing in `WorldRenderer`; add `InputAction.Move*`, wire `HandleSimulationActions` to queue moves, decide the fate of the stranded `TryMove` path (wire or delete); add left-click-to-move and camera follow/recenter.
- *Risks:* snapshot-per-frame cost grows — introduce the dirty flag here.
- *Acceptance:* creatures and trees are visible and animate as they wander; player moves with keys and left-click; camera keeps the player in view; no per-frame full-map reallocation when idle.

**Phase 2 — Player-as-entity and a real turn scheduler**
- *Goal:* unify the player into the entity/turn model and generalize initiative.
- *Systems/files:* move the player into a store (or a canonical `Entity` the session references), fold `PlayerTurnState` into `Entity.Actor`; replace `TurnScheduler`'s hardcoded creature loop with a speed-based initiative queue that iterates any actor; persist `Actor` in save data.
- *Risks:* this is the biggest refactor — do it behind tests. Movement/transition regressions are likely if position ownership isn't consolidated cleanly.
- *Acceptance:* one code path answers "where is the player"; a speed-150 actor demonstrably acts more often than a speed-100 one; actor energy survives save/load; existing movement behavior is unchanged.

**Phase 3 — FOV and inspect/tooltip UX**
- *Goal:* fog of war, explored memory, and readable inspection.
- *Systems/files:* an FOV computation using `TileFlags.BlocksVision`, per-map visible/explored state, snapshot fields for it, renderer dimming; implement `InspectSelected` and hover tooltips (terrain, biome, entity), plus a message log panel.
- *Risks:* FOV cost on 64×64 is fine, but recomputing every frame isn't — recompute only on move/turn.
- *Acceptance:* unseen tiles are hidden, previously-seen tiles show as memory, hovering any tile shows its details, and the inspect action reports what's there.

**Phase 4 — Interaction, weighted pathfinding, and basic combat**
- *Goal:* the first real gameplay loop.
- *Systems/files:* a small action/command system so intents (move, attack, interact, harvest) resolve uniformly; give `GridPathfinder` terrain costs (A* with road/mud/water weights) shared by player and NPCs; add HP/stats and a bump-to-attack resolver; make `HarvestableTree` actually harvestable and reconcile it with `TerrainId.Tree`.
- *Risks:* combat + pathfinding interacting with the scheduler is where turn-order bugs hide; write scenario tests.
- *Acceptance:* moving into a hostile entity attacks it; entities die and are removed and that persists; NPCs pathfind around obstacles; harvesting yields something and the change saves.

**Phase 5 — Inventory, richer generation, factions**
- *Goal:* depth.
- *Systems/files:* item entities/components and an inventory model (this is the point to decide fat-entity vs. lightweight component store, informed by what Phases 2–4 taught you); more `IGenerationPass` passes for structures/rooms and cross-map continuity blending; faction/relationship data feeding NPC AI; hooks on `SimulationClock` for weather/time; a quest data model.
- *Risks:* scope creep — keep each subsystem behind its own pass/service and ship independently.
- *Acceptance:* items can be picked up, carried across maps, and saved; generated maps contain structures and blend at seams; NPCs behave differently by faction; a minimal quest can be defined in content and completed.