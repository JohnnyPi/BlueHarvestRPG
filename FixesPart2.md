Blue Harvest — Codebase Review
Critical bugs
1. Border transitions don't move the player entity → player becomes invulnerable
Files: MapTransitionService.TryTransitionToMap, compared against StructureTransitionService.TryApplyTransition; consumed by MovementService.TryMoveLocal, CombatResolver.TryAttack, GameSession.RefreshPlayerVitals.
When the player walks across a local-map edge, TryTransitionToMap stores the old map, reassigns ActiveLocalMap to the destination, and calls SyncPlayerEntityPosition(). It never removes the player entity from the old map and never calls EnsurePlayerEntity() on the new one. The structure-transition path does both correctly; the border path does neither.
Consequences, all confirmed by tracing the code:

The destination map has no EntityId.Player in its entity store. GameSession.PlayerEntity then falls back to constructing a throwaway EntityFactory.CreatePlayer(...) on every access.
CombatResolver.TryAttack subtracts damage from that throwaway entity, then calls RefreshPlayerVitals(), which reads health back from ActiveLocalMap.Entities.GetById(EntityId.Player) — null on the new map — so the damage is discarded. session.PlayerHealth never drops, and the death check never fires. The player is effectively invincible on every map reached by border-crossing (as opposed to EnterWorldCell, which does spawn the entity).
Because the player is absent from the entity store, RaptorBehavior.CanEnter sees the player's tile as empty and lets creatures walk onto/through it.
The old map is persisted with a "ghost" player entity at the crossing point, which later gets serialized into the save.

Recommendation: give both transition paths one shared "switch active map" primitive that (a) RefreshPlayerVitals + Store + Entities.Remove(EntityId.Player) on the outgoing map and (b) reassigns ActiveLocalMap, sets position, then calls EnsurePlayerEntity(). The simplest immediate fix is to mirror the four relevant lines from StructureTransitionService into TryTransitionToMap.
2. Save-load regenerates the world with the wrong generation inputs (nondeterminism)
Files: SaveManager.TryLoad vs GameBootstrap.CreateSimulationHost (new-game path); IslandPlanner.Generate (uses _biomeRules in RegionBiomeStage and _blueprintCatalog in StructureFinalizeStage).
New games build the generator as new IslandWorldGenerator(bundle.Island, blueprintCatalog, bundle.BiomeRules). Loading builds it as new IslandWorldGenerator(islandDefinition) — biome rules and blueprint catalog default to new BiomeRulesDefinition() / StructureBlueprintCatalogDefaults.Create(). Since biomes and structure finalization consume those inputs, a reloaded world can diverge from the world that was actually played whenever the content-loaded rules differ from the code defaults. Persisted local-map edits, scenario target cells, and landmark positions are all pinned to overworld coordinates, so any divergence silently corrupts them.
The BiomeRulesHash guard doesn't protect against this: it only refuses to load when content changed between sessions; it doesn't make the loader use those content rules. It regenerates with defaults regardless.
Recommendation: thread bundle.BiomeRules and the blueprint catalog into TryLoad and pass them to IslandWorldGenerator, exactly as the new-game path does. Add a determinism test (below) so this can't regress.
Likely bugs
3. "Autosave" never saves automatically; closing the window loses progress
Files: BlueHarvestGame (no OnExiting override), UiManager (pause-quit and run-end do save), GameBootstrap.SaveGame.
The only save triggers are the SaveGame key, the pause menu's Save/Quit, and the run-end screen. There is no periodic autosave and no save on OnExiting, so quitting via the OS close button or Alt+F4 discards everything since the last manual save — despite the slot being named autosave. For a survival roguelike this is a meaningful data-loss path.
Recommendation: override OnExiting to call GameBootstrap.SaveGame, and/or add an interval/turn-count autosave. At minimum, saving on exit closes the worst case.
4. Road-corridor entry fallback scans the wrong column for east/west edges
File: WalkabilityHelper.FindRoadCorridorEntry.
For Direction.East or Direction.West connections it always probes new LocalCoord(0, y) (the west column). An east connection's corridor enters at x = Width-1. This is a fallback used only when TryFindNearestWalkable fails during EnterWorldCell, so impact is low, but it can land the player off the intended road.
Recommendation: pick the column based on the edge (0 for West, Width-1 for East); same for rows on North/South.
5. The shipped entry point runs the preview tool, not the game
Files: Program.cs (new IslandPreviewGame(seed)), BlueHarvestGame (referenced only by itself).
The top-level entry launches IslandPreviewGame. BlueHarvestGame — the actual game with simulation, save/load, and UI — is never instantiated anywhere in this assembly. If BlueHarvest is the intended product and this is meant to be its assembly, the entry point is wrong; if this is deliberately the generation-debug project, then BlueHarvestGame and its whole dependency chain (SaveManager wiring, UiManager, WorldRenderer) are dead here. Worth confirming which is intended — a lot of the review below only matters if BlueHarvestGame actually ships.
Incomplete / partially implemented functionality
6. PersistentLocalMapRepository persists nothing
File: PersistentLocalMapRepository.
Despite the name, it's a thin pass-through around InMemoryLocalMapRepository with an Inner accessor; nothing streams to disk. All persistence actually happens in SaveManager on explicit save. The type exists only so GameBootstrap can pattern-match is PersistentLocalMapRepository in SaveGame — but that branch behaves identically to the InMemory branch. Either implement real incremental persistence or delete the type and pass InMemoryLocalMapRepository directly.
7. No incremental/streaming map persistence
Tied to #6. Every visited local map is held in memory for the whole session and written wholesale on save. Fine at current scale, but the abstraction implies something that isn't there. Decide whether streaming persistence is a goal or drop the pretense.
Architecture / organization issues
8. Load builds the session and repository twice
Files: SaveManager.TryLoad and GameBootstrap.CreateSimulationHost.
TryLoad fully constructs a populated GameSession + InMemoryLocalMapRepository. GameBootstrap then throws that session away, builds a second GameSession + PersistentLocalMapRepository, copies the maps over, and hand-copies ~20 fields (inventory, quests, pressure state, vitals, movement path, run state, world time, message log…). Every new session field must now be remembered in two places or it silently won't survive load. The GameSession constructor also re-runs ScenarioGenerator.Generate, StartScenarioQuests, spawn resolution, and visibility — all immediately overwritten.
Recommendation: pick one construction path. Either have TryLoad return the finished session/repository and let GameBootstrap use them directly, or move all field population into GameBootstrap and have TryLoad return raw WorldSaveData. The repository-type swap that motivates the duplication is a no-op today (see #6), so it isn't a real reason to rebuild.
9. Two divergent map-switch code paths with a shared invariant only one upholds
This is the structural root of bug #1. MapTransitionService and StructureTransitionService both "make a different local map active," but the player-entity bookkeeping lives independently in each. Consolidating them into a single primitive removes the class of bug entirely rather than patching one call site.
10. TerrainId.ShallowWater has two contradictory meanings
Files: BoundaryConnectionPass.StampRiver (ShallowWater with ContainsWater only → walkable ford) vs the biome generators in LocalMapGenerator (ShallowWater with BlocksMovement | ContainsWater → impassable) vs GridPathfinder.GetTerrainMoveCost (assigns ShallowWater a finite cost of 8, i.e. treats it as traversable).
Walkability is carried entirely in the flags, so the same terrain id is sometimes passable and sometimes not. It happens to work because BuildPathTo uses map.BlocksMovement as the block predicate, but any code that reasons about walkability from the terrain id (as the pathfinder cost table implicitly does) will be wrong. Consider splitting ford vs deep shallow water into distinct ids, or make one helper the single source of truth for "is this tile walkable."
11. session.WorldTime duplicates the clock
GameSession.WorldTime exists only as a conduit into Clock.Restore at load; saves read host.Clock.WorldTime directly. It's harmless today but is exactly the kind of shadow field that drifts. Consider removing it and sourcing world time from the clock everywhere.
Redundancy / cleanup (dead code)

EntityRegistry and GameSession.Entities: the property has no consumers, and its only method EnsureDefaultsSpawned is never called (spawning happens in InMemoryLocalMapRepository.GetOrGenerate). Remove both.
GameSession.DescribeOverworldLandmark: defined, never called. (DescribeOverworldGeology is the one actually used in InspectTile.)
Legacy generation branches gated by UseLegacyIslandMask / UseLegacyRandomRoads: off by default and spread across ~12 stage files, each carrying a dual code path. If the preview tool still needs them, keep them isolated; otherwise they're maintenance drag. Worth an explicit decision.
GameSession.EnsurePlayerEntity's spawn-scan loop (the for (attempt…) after FindUnoccupiedWalkable) is redundant — FindUnoccupiedWalkable already returns an unoccupied tile, so the loop condition is almost never true.

Performance risks
12. Overworld snapshot rebuilds all static masks every time
File: SimulationHost.BuildOverworldSnapshot and its BuildTectonicBoundaries / BuildRiverEdgeMask / BuildRoadEdgeMask / BuildRoadCells helpers.
The overworld is 512×512 (262k cells). Each snapshot rebuild walks the full grid for biome, tectonic boundaries, river edges, road edges, road cells, and visibility — roughly 1.5M cell reads plus a full-map landmark LINQ pass. Biome, tectonic, river, and road data are static after generation and never change during play; only visibility/explored change. Since RenderDirty is set on essentially every action (each overworld step marks both render and visibility dirty), auto-travel triggers this per step.
Recommendation: build the static masks once (lazily on first overworld snapshot) and reuse them; only recompute the visible/explored buffers per rebuild. That removes ~80% of the per-snapshot work.
13. Bounds-checked cell access in tight loops
Overworld.GetCellValue(new WorldCoord(x, y)) runs a Contains check (with a throw path) on every call; the snapshot loops call it hundreds of thousands of times. Use the Cells span directly in these hot loops.
Future improvement opportunities

Determinism test: generate a world from a fixed seed twice — once with the new-game generator config and once with the load-path config — and assert the plans are byte-identical. This directly guards #2 and would have caught it.
Save round-trip test: save → load → assert position, vitals, entities (including exactly one player), quests, inventory, and pressure state match. Guards #1 and #8.
A single MapService owning "active map" transitions (surface, border, structure) with the player-entity invariant enforced in one place (resolves #1/#9 permanently).
Optional: cross-map terrain blending at biome seams for visual continuity — currently adjacent maps meet with a hard edge, which is acceptable but noticeable.

Prioritized action plan
Fix first (correctness, low effort, high impact):

Border-transition player entity (#1) — restores combat/death and the entity-store invariant. Small, localized change.
Load-path generation inputs (#2) — pass biome rules + blueprint catalog into TryLoad. Prevents silent world corruption on reload.
Save on exit (#3) — add OnExiting save. One method; eliminates the worst data-loss path.

Do next (stability and velocity):
4. Unify the two map-switch paths (#9) so #1 can't recur, and collapse the double session construction on load (#8) so save fields stop silently dropping.
5. Add the determinism and save-round-trip tests, which lock in 1–3.
6. Confirm the intended entry point (#5) before investing further in BlueHarvestGame.
Can wait (cleanup and polish):
7. Cache static overworld masks (#12) — noticeable during auto-travel but not blocking.
8. Remove dead code (EntityRegistry, DescribeOverworldLandmark, redundant spawn loop) and resolve PersistentLocalMapRepository (#6).
9. Resolve the ShallowWater dual-meaning (#10) and decide the fate of the legacy generation branches.
The two changes that most improve stability are #1 and #2; the two that most improve development velocity are the unified map-switch primitive (#9) plus the round-trip/determinism tests, because together they stop the highest-value bugs from silently reappearing as the code grows.