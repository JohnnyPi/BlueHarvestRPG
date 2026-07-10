# Blue Harvest — World Generation Improvement Plan

A focused, step-by-step implementation plan for eight world-generation
changes. Each item lists the **current behavior** (as it exists in the code
today), the **goal**, and **ordered steps** that reference the real files to
touch. No code is written here — this is the roadmap.

A recommended build order is given at the end, because several items depend on
each other (centering must land before river-to-ocean work; height bands feed
the local-map sub-regions; the road refactor and river refactor share the same
"global tile path" mechanism).

---

## 1. Restore island scale, land-only interior, and variable-width beach perimeter

**Rechecked current behavior (seed `8478919930192148244`)**
- The preview shows three coupled regressions: the island occupies only a small
  part of the 512×512 crop, `Ocean` cells occur inside the visible island
  silhouette, and the sandy perimeter is absent or too thin to read.
- Centroid-aware cropping has been implemented, but
  `OverscanShapeFitting.ComputeSafeNormalizedHalfExtent` still sizes against a
  **geometric-center** crop. The production shape is off-center, so the fitter
  and `MaskQualityStage.ValidateCropWindow` do not evaluate the same window.
- `ComputeInitialShapeScale` adds about `1.5 ×` all domain-warp strengths to the
  authored extent. With the current stronger warp this drives the initial scale
  to its `0.58` floor. Failed quality attempts then multiply the scale by
  `0.94`, potentially down toward `0.28`; the retry policy therefore makes an
  already-small island progressively smaller.
- Reducing `oceanFrame.overscanScale` from `1.30` to `1.15` left only about 38
  overscan cells per side while requiring 32 cells of land clearance. That is
  too little tolerance for an off-center, warped shape and causes more shrink
  retries. The earlier recommendation to reduce overscan was therefore
  incorrect for the current fitter.
- There are two definitions of land. `CoastDistanceField` derives
  `CoastDistance` from the static `IslandMask`, while
  `LandmassStage.Reconcile` requires both positive mask coast distance and
  `Elevation > LandElevationThreshold`. Erosion can carve interior elevation
  below that threshold; reconciliation then labels those cells `Ocean` even
  though they remain inside the mask silhouette.
- Variable beach width cannot currently work:
  `RegionBiomeStage` reads `plan.Concavity` on land, but
  `CoastDistanceField.ComputeConcavity` explicitly sets every land cell's
  concavity to `0`. Beach width is consequently fixed at
  `beachCoastDistance` (`0.04`), half the `IsCoast` band
  (`inlandCoastDistance = 0.08`). `RegionBiomeStage` overwrites the rest of that
  band with inland biomes, while rocky/headland classifications replace Beach
  with Hills.

**Goal:** maximize the main landmass inside the configured safe margin, center
the actual generated silhouette, prohibit accidental `Ocean` holes inside it,
and form a continuous beach ring whose width varies smoothly between explicit
minimum and maximum bounds.

**Steps**
1. **Instrument the regression before more tuning.** Expose the attempted and
   selected shape scale, crop offset, land coverage ratio, validation result,
   and violation counts in `IslandPreviewGame`. Record the above seed as a
   fixed regression case. A failed ocean-frame validation must be visible
   rather than silently presented as a successful generation.
2. **Use one crop geometry everywhere.** Change `OverscanShapeFitting` to size
   against the same centroid-derived crop offset used by
   `PlanCropUtility.CropCenteredOnLandmass` and
   `MaskQualityStage.ValidateCropWindow`. Do not calculate the safe extent from
   `(overscanSize - cropSize) / 2` after choosing a centroid crop.
3. **Select the largest valid scale instead of repeatedly shrinking.** For each
   deterministic mask candidate, use a bounded search over shape scale and keep
   the largest scale that passes the actual crop-window quality checks. Rank
   valid candidates by cropped land coverage and coastline quality. Do not mix
   a new random mask seed with every scale step, and do not return a tiny
   "least-bad" candidate as if it passed.
4. **Correct the extent estimate.** Replace the sum-of-all-warp-strengths
   padding in `ComputeInitialShapeScale` with a conservative bound that matches
   how `IslandMaskStage` actually displaces coordinates. Treat the scale floor
   only as a search bound, not as the primary sizing mechanism.
5. **Restore overscan headroom before tuning shape radii.** Return
   `overscanScale` to a value that can accommodate the required 32-cell margin
   plus centroid shift and warp (start by re-evaluating `1.30`). Once the
   largest-valid-scale search works, tune overscan and authored blob radii
   together from measured cropped land coverage. Re-center the authored blobs
   near `(0, 0)` to reduce wasted overscan, but do not rely on authoring changes
   to hide a crop/fitter mismatch.
6. **Establish one authoritative final shoreline.** Flood-fill non-land from
   the crop boundary after erosion/reconciliation. Only water connected to that
   exterior component may receive `BiomeId.Ocean`. Interior cells inside the
   intended land mask must either:
   - remain land by clamping erosion above `LandElevationThreshold`, or
   - be explicitly authored/classified inland water (`ShallowWater`/river), not
     accidental `Ocean`.
   Recompute final coast distance from this authoritative land/ocean boundary
   before coastal biome assignment.
7. **Prevent river carving from deleting land.** In `ErosionStage.TraceAndCarve`,
   cap inland carving above the land threshold and represent rivers through
   `IsRiverCell`/river terrain. Permit a below-threshold channel only at an
   intentional, exterior-connected river mouth. This keeps item 6's future
   river work from reopening ocean holes.
8. **Introduce explicit variable beach bounds.** Replace the single effective
   width with `minBeachCoastDistance` and `maxBeachCoastDistance` (validated so
   `0 < min <= max <= inlandCoastDistance`, unless the coast band is enlarged).
   Build a deterministic low-frequency shoreline variation field from global
   coordinates and combine it with bay/concavity influence. Smooth along the
   shore so adjacent widths taper rather than jump.
9. **Make the variation field available on coastal land.** Compute curvature
   on shoreline cells and propagate it inward with coast distance, or propagate
   ocean-side concavity to the nearest land coast. Remove the current
   land-`Concavity = 0` dead path before using concavity in
   `RegionBiomeStage`.
10. **Guarantee beach coverage before inland biome assignment.** Assign
    `BiomeId.Beach` to every eligible perimeter land cell whose final coast
    distance is inside its local width. Coastal landform should choose the
    beach's local terrain treatment (sand, pebbles, rock, mangrove edge), not
    silently replace the perimeter biome with Hills. If true cliffs are meant
    to interrupt beaches, make that an explicit configured exception and test
    its maximum gap length.
11. **Add regression and geometry tests.** For the fixed seed and a seed batch,
    assert:
    - ocean-frame validation passes and forbidden edge-band counts are zero;
    - cropped land coverage and main-component diameter exceed agreed minimums;
    - no `BiomeId.Ocean` cell is enclosed by the final land silhouette;
    - each exterior shoreline segment has beach within the configured local
      width (except explicit cliff exemptions);
    - observed beach widths include multiple values, stay within min/max, and
      vary smoothly along the perimeter.

---

## 2. Local-map tile review + sub-regions within biomes (light → deep jungle)

**Current behavior**
- `LocalMapGenerator.GenerateFromIslandCell` switches on `Biome` only and calls
  per-biome fillers (`GenerateForest`, `GeneratePlains`, …). Each filler rolls a
  **fresh independent `random.NextFloat()` per tile**, so density is spatially
  uniform — no coherent clusters, no light-vs-deep gradient, and no continuity
  with neighboring cells.
- Jungle and Forest share `GenerateForest`; the only difference is a flat
  `treeChance` bump.

**Goal:** consistent, readable terrain per biome, plus intra-biome variation
(e.g. light jungle edges grading into dense interior).

**Steps**
1. **Introduce a shared local noise field.** Add a helper (e.g.
   `LocalTerrainField`) that samples `ValueNoise`/`NoiseUtility.Fbm` using
   **global tile coordinates** (`worldCell * LocalMap.Width + localTile`) so the
   pattern is continuous across cell boundaries and deterministic from the world
   seed. This replaces "roll per tile" with "sample a field per tile."
2. **Define a per-cell "biome depth" scalar.** In the island plan, derive how
   far each land cell is from a different-biome neighbor (a cheap BFS/flood over
   `IslandCellData.Biome`, or reuse `CoastDistance`-style logic). Store it (new
   `float[] BiomeDepth` on `IslandPlan`, populated in a small stage after
   `BiomeCoherenceStage`). Depth ≈ 0 at biome edges, ≈ 1 deep in the interior.
3. **Pass depth + fields into local gen.** Add `BiomeDepth` (and `Elevation`,
   `Moisture`) to `LocalGenerationContext`, populated in
   `LocalMapGenerator.GenerateSurface` from the island cell.
4. **Rewrite the biome fillers to be field-driven.** For each filler, compute
   `density = base + depth * range`, then place features where the noise field
   exceeds a threshold rather than on independent rolls. Concretely for jungle:
   `treeChance` scales from a "light" value at `depth≈0` to a "deep" value at
   `depth≈1`; add undergrowth/rock accents from a second noise octave.
5. **Split Jungle from Forest.** Give `BiomeId.Jungle` its own filler
   (dense canopy, undergrowth, vision-reducing tiles) distinct from temperate
   `Forest`. Consider one or two new `TerrainId` values (e.g. `Undergrowth`,
   `DenseCanopy`) added to `TerrainId.cs` and `terrain.yaml` with colors, so the
   gradient reads on screen.
6. **Audit the full palette for consistency.** Review every filler
   (`GeneratePlains/Swamp/Hills/Mountains/Beach/Reef/...`) so feature-density and
   color choices are coherent across biomes and match the new field-driven
   approach. Update `terrain.yaml` colors where needed.
7. **Keep edges continuous** by ensuring neighboring cells sample the same
   global field (step 1 guarantees this) — no seams at cell borders.

---

## 3. More height regions (foothills, hills, small mountains, mountains)

**Current behavior**
- `BiomeClassifier.Classify` has only two elevation bands above the lowlands:
  `Hills` (`hillsMinElevation 0.68`) and `Mountains` (`mountainsMinElevation
  0.82`). `biome_rules.yaml` holds these thresholds.
- Local generation never reads `Elevation`, so all "hills" cells look identical
  regardless of how high they are.

**Goal:** a graded highland progression — foothills → hills → small mountains →
mountains — both in classification and in how the local map looks.

**Steps**
1. **Decide the representation.** Two viable approaches; recommend **B** for the
   least churn:
   - **A. New biomes:** add `Foothills` and `SmallMountains` to `BiomeId` and
     wire them through classifier, coherence, rendering, road/river rules. Most
     invasive.
   - **B. Elevation-driven local terrain:** keep `Hills`/`Mountains` biomes but
     drive the *local map* appearance from the continuous `Elevation` value
     passed into local gen (from item 2, step 3). Add sub-bands there.
2. **Add threshold bands** (approach B): in `biome_rules.yaml` /
   `BiomeRulesDefinition`, add `foothillsMinElevation` and
   `smallMountainMinElevation` between the existing hills/mountains thresholds.
   Expose them as a small ordered table rather than scattered floats.
3. **Add a height-band resolver.** A helper that maps `Elevation` →
   `{Foothills, Hills, SmallMountains, Mountains}`. `BiomeClassifier` can still
   return `Hills`/`Mountains` for the overworld biome; the resolver refines the
   band for local rendering.
4. **Make the local fillers height-aware.** In `LocalMapGenerator`, replace the
   single `GenerateHills`/`GenerateMountains` with band-driven logic: rock
   coverage and passability increase with the band (foothills = mostly grass +
   scattered rock; mountains = mostly rock + few grass gaps), using the shared
   noise field from item 2 so slopes look natural, not random.
5. **Reflect bands on the overworld** (optional): give
   `OverworldGeologyColors` / `WorldRenderer` a subtle shade ramp by elevation
   band so the map shows the highland gradient.
6. **Tune** the four thresholds against `MinElevationStdDev` and the balance
   pass so highlands don't dominate or vanish.

---

## 4. Single, circular volcanic cone with land sloping down from it

**Current behavior**
- `VolcanicActivityStage.Execute` places `VolcanicConeCount` cones (config = 2,
  clamped 1–3), each **elliptical** (`aspect 1.55–2.5`, random rotation).
- `StampVolcanicCone` adds uplift in **discrete rings** (`LavaCore` /
  `MountainRing` / `HillRing` bands with fixed `heightScale` steps), which reads
  as stepped terraces, and it *adds* to existing elevation rather than shaping a
  clean radial profile.

**Goal:** exactly one circular cone; elevation peaks at the vent and slopes
smoothly down to the surrounding land.

**Steps**
1. **Force a single cone.** Set `volcanicConeCount: 1` in `island.yaml` and
   clamp to `1` in `VolcanicActivityStage` (or short-circuit after the first
   placement).
2. **Pick the best central site.** Keep the candidate scoring but bias harder
   toward island center and high elevation so the one cone lands on the main
   massif (tighten the `distFromCenter > 0.58` gate; weight `Elevation` more).
3. **Make it circular.** In the site creation, set `RadiusY = RadiusX` and drop
   the random `aspect`/`rotation` (rotation becomes irrelevant for a circle).
4. **Replace ring bands with a continuous radial profile.** In
   `StampVolcanicCone`, compute a smooth falloff `h(norm)` (e.g. a
   `smoothstep`/cosine curve from peak at `norm=0` to `0` at `norm=1`) and
   **set** the cone's contribution as `baseLandElevation + coneHeight * h(norm)`
   rather than summing stepped `heightScale` values. This yields a true conic
   slope.
5. **Slope the surrounding land.** Extend the cone's influence radius slightly
   beyond the mountain so the apron blends into neighboring biomes: outside the
   steep cone, apply a gentle `elevation += apron * (1 - norm)` so land visibly
   descends away from the volcano instead of ending abruptly.
6. **Keep `VolcanicConeUtility` in sync.** The `LavaCore/MountainRing/HillRing`
   fractions are still used by `TryGetNearestConeDistance` and biome finalize —
   repoint them at the new continuous profile (they can become simple
   thresholds on `norm` for "is lava/rock/apron").
7. **Re-run** `LandmassStage.Reconcile` and `BathymetryStage` after the change
   (already in the pipeline) so the new heights settle; confirm the cone renders
   round and graded in `IslandPreviewGame`.

---

## 5. Contiguous roads

**Current behavior**
- Two independent road representations reach the local map and can disagree:
  1. `RoadNetworkStage` pathfinds a real route and records `GlobalPathTiles`
     (`AddGlobalCenterline`), stamped by `FacilityRoadStampPass` — these follow
     the true winding path.
  2. `FacilityRoadGraphApplier.ApplyToOverworld` writes **edge connections** at
     a fixed center `LocalOffset`, and `BoundaryConnectionPass.StampRoadCorridor`
     draws a **straight full-width strip at map center** — which usually does
     *not* line up with the `GlobalPathTiles`, producing forked/broken roads.
- `AddGlobalCenterline` only adds a single "step" tile at cell borders, so
  diagonal cell-to-cell moves can leave 1-tile gaps.

**Goal:** one continuous road network that connects cleanly within and across
local maps.

**Steps**
1. **Choose a single source of truth: `GlobalPathTiles`.** Make the road that
   the player walks come entirely from the pathfound global tiles, not the
   center-strip corridor.
2. **Fix border continuity in the global path.** In
   `RoadNetworkStage.AddGlobalCenterline`, when consecutive path cells differ,
   emit a *contiguous run* of tiles from the previous cell's edge to the next
   cell's center (not just one step tile), so the road never gaps at a cell
   boundary. Include the width expansion on both axes.
3. **Align edge connections to the real crossing.** In
   `FacilityRoadGraphApplier`, compute the `LocalOffset` from where the
   `GlobalPathTiles` actually cross the shared edge between the two cells,
   instead of hardcoding `LocalMap.Height/2`. This makes the boundary marker and
   the stamped tiles coincide.
4. **Stop double-stamping straight corridors.** Change
   `BoundaryConnectionPass.ApplyRoadConnection` so it only guarantees the two
   cells *touch* at the crossing offset (a short connector to the global path),
   rather than painting a straight strip across the whole map. The winding
   `FacilityRoadStampPass` output becomes authoritative.
5. **Guarantee the door spur connects.** `FacilityRoadStampPass.StampApproachFromRoad`
   already links a structure door to the nearest in-cell road tile — verify it
   runs after the contiguous global stamp so every structure has an unbroken
   path to the network.
6. **Retire/guard the legacy pass.** `RegionalFeatureGraph.ApplyRoads`
   (`UseLegacyRandomRoads`) stays off in production; keep it only behind the
   flag for preview comparisons.
7. **Add a connectivity check** (reuse `IslandPathfinder`/`GridPathfinder`):
   assert the road tile set forms one connected component from the hub, and
   repair or log if not.

---

## 6. Natural, contiguous rivers that exit to the ocean

**Current behavior**
- Two disjoint river systems:
  1. `ErosionStage.TraceAndCarve` traces downhill from high cells, **carves
     elevation**, and marks `plan.IsRiverCell[]` — a natural, meandering path,
     but it only affects elevation/biome coherence, never places water tiles.
  2. `RegionalFeatureGraph.ApplyRivers` re-traces downhill on the finished
     `Overworld` and emits **edge connections**; `BoundaryConnectionPass.StampRiverCorridor`
     then paints a **straight center-line `ShallowFord`** across each cell.
- Result: on-map rivers are straight center strips, unrelated to the carved
  natural path, and only reach ocean if the downhill trace happens to.

**Goal:** one meandering river network that is continuous across cells and
terminates at the coast/ocean.

**Steps**
1. **Unify on the carved path.** Promote `ErosionStage`'s river trace into a
   first-class **river graph** on `IslandPlan` (mirroring `FacilityRoadGraph`):
   store ordered cell paths and a `GlobalRiverTiles` set of global tile
   coordinates, built from the same meandering trace that sets `IsRiverCell`.
2. **Guarantee ocean termination.** Extend each trace so it does not stop at the
   lowest inland cell: continue to the nearest coast/ocean cell (follow
   `CoastDistance` downhill, or run a short pathfind to the nearest
   `Ocean/ShallowWater`). Only keep rivers that actually reach water; drop or
   reroute land-locked ones.
3. **Generate global river tiles like roads.** Add a
   `RegionalFeatureGraph.ApplyRivers` replacement (or a new applier) that walks
   the unified path and writes contiguous `GlobalRiverTiles` with the border
   run-fill from item 5 (roads), so the river never gaps between cells.
4. **Stamp meandering water in local maps.** Add a `RiverStampPass` (parallel to
   `FacilityRoadStampPass`) that paints `ShallowFord`/`ShallowWater` along the
   global river tiles that fall inside the current cell, with a little
   noise-driven wobble for a natural bank. Retire the straight
   `StampRiverCorridor` (or reduce it to a fallback connector at the true
   crossing offset, like roads in item 5, step 4).
5. **Align edge offsets to the real crossing** where a river leaves a cell, so
   adjacent cells' water lines up (same fix pattern as roads).
6. **Interact cleanly with roads.** Where a river crosses a road, stamp a ford
   tile (`ShallowFord`) so both remain traversable; give roads priority on
   shared tiles per existing `StampRoad` guards.
7. **Tune** `RiverCount`, `RiverMinElevation`, `RiverHeadSpacing`, and
   `RiverCarveDepth` so rivers are visible but not flooding; verify each renders
   as a continuous line from highland to shore in `IslandPreviewGame`.

---

## 7. Buildings visible on the world map, spanning multiple tiles

**Current behavior**
- Structures carry a footprint (`StructurePlacement.Width/Height` in *global
  tiles*), but placements are small (e.g. Visitor Center 28×24, Hotel 24×18) —
  all **smaller than one 64×64 world cell**, so they occupy a single overworld
  tile.
- `WorldRenderer.DrawOverworldLandmarks` already draws each landmark as a rect
  sized by footprint fraction, but only when `tileSize >= 8`
  (zoomed in) and only for **explored** cells
  (`OverworldLandmarkCatalog.CollectExploredLandmarks` /
  `IsLandmarkVisible`).

**Goal:** buildings read clearly on the world map and genuinely occupy multiple
world-map tiles.

**Steps**
1. **Give major structures multi-cell footprints.** In `ParkLayoutStage` (and
   the counts/dimensions it passes to `StructurePlacement.CreatePending`),
   increase footprints for large buildings past `LocalMap.Width` (64) so they
   span 2×2+ world cells (e.g. Visitor Center ~96×80). Keep small facilities
   (Helipad, Dock) single-cell.
2. **Make placement respect the larger footprint.** `IslandPlacementHelper.CenteredOrigin`
   and the role-marking must mark **every** cell the footprint overlaps (not
   just the anchor), and placement must reject sites where the footprint would
   hit ocean/another structure. Add an overlap/land check before committing.
3. **Render structures on the overworld regardless of zoom.** In
   `WorldRenderer`, draw structure footprints (from
   `snapshot.OverworldLandmarks`) at a coarser threshold than the current
   `tileSize >= 8`, or add a dedicated always-on "building" layer so they're
   visible when zoomed out. Draw the whole footprint rect, not just the anchor
   cell's fraction.
4. **Verify multi-cell footprint math.** `OverworldLandmarkCatalog` and
   `WorldRenderer.IsLandmarkVisible` already iterate `minCell..maxCell` — fix the
   one latent bug where `maxCellY` divides by `LocalMap.Width` instead of
   `Height` (`WorldRenderer.IsLandmarkVisible`). Confirm footprints that cross
   cell borders are considered explored/visible correctly.
5. **Distinct visuals.** Extend `ResolveLandmarkColor` so each structure type is
   distinguishable; optionally outline the footprint (`DrawBorder`) so large
   buildings read as a block, not a dot.

---

## 8. Buildings connected to their local maps continuously

**Current behavior**
- Surface stamp: `StructureStampPass` → `StructureStampHelper.StampRect` clips
  the building to the current cell, so a multi-cell building is correctly split
  across adjacent surface maps.
- Interior: `StructureFloorGenerator.Generate` builds a **single 64×64 floor**
  keyed to one world cell (`key.WorldPosition`) via `StampBuilding`. For a
  footprint larger than 64, the interior can't represent the whole building, and
  the door/stair coordinates are relative to a single cell.
- Transition: `TileTransitionResolver` maps a surface `Door`/`StairsUp` to the
  interior door and back, using `map.WorldPosition` — assumes the building lives
  in one cell.

**Goal:** entering, moving through, and exiting a building is seamless — the
surface footprint, the interior, and the road approach all line up, including
for multi-cell buildings from item 7.

**Steps**
1. **Anchor the building to a single origin cell.** Standardize on the
   structure's **origin cell** (`GlobalOriginX/Y / LocalMap`) as the canonical
   `WorldPosition` for its interior `MapKey`, so entering from *any* overlapped
   surface cell resolves to the same interior instance. Update
   `TileTransitionResolver.TryResolveSurfaceTransition` and
   `StructurePlacementQueries.ToLocalCoord` to translate door coordinates
   relative to the origin, not the cell the player happens to stand in.
2. **Support interiors larger than one cell.** For multi-cell footprints, either
   (a) scale the interior floor map to the footprint (tile the interior across a
   virtual grid the size of the footprint and page it), or (b) cap *enterable*
   interiors at 64×64 and place the door/interior within one cell of the
   footprint. Pick (b) first for simplicity; note (a) as a follow-up.
3. **Make the door reachable from the road.** Confirm item 5's door spur
   (`FacilityRoadStampPass.StampApproachFromRoad`) targets the door on the
   correct surface cell of a multi-cell building, so the road always meets the
   entrance.
4. **Keep the exit consistent.** `TileTransitionResolver.TryResolveInteriorTransition`
   returns the player to `MapKey.Surface(map.WorldPosition)` at the door — ensure
   that surface position is the origin cell's door tile, matching where they
   entered, for any overlapped entry cell.
5. **Validate navigability end to end.** `LocalMapGenerator.ValidateNavigability`
   / `NavigabilityValidator` already repairs the surface entry; extend the check
   so: road → door → interior door → stairs are all connected, and repair/log
   when a multi-cell building breaks the chain.
6. **Round-trip test.** Enter from each overlapped surface cell, walk the
   interior, use stairs, and exit — confirm the player lands back on the same
   door tile every time.

---

## Recommended build order

1. **Item 1 (scale/shoreline/beach)** — everything downstream (rivers to ocean,
   structure placement, road reach) depends on one authoritative, correctly
   framed shoreline.
2. **Item 4 (volcano)** — self-contained elevation change; do it early so the
   height field is stable before height-band and river work reads it.
3. **Item 3 (height regions)** — establishes the elevation bands and the
   "pass Elevation/Moisture into local gen" plumbing…
4. **Item 2 (biome sub-regions)** — …which item 2 reuses (shared local noise
   field + per-cell depth). Do 3 and 2 together; they touch the same
   `LocalMapGenerator` fillers and `LocalGenerationContext`.
5. **Item 5 (roads)** — introduces the "contiguous global tile path + aligned
   edge offset" pattern.
6. **Item 6 (rivers)** — reuses that exact pattern; depends on item 1 for clean
   coast termination.
7. **Item 7 (buildings on map / multi-tile)** — footprint enlargement and
   overworld rendering.
8. **Item 8 (building ↔ local map continuity)** — depends on item 7's
   multi-cell footprints and item 5's door spur.

### Cross-cutting shared work (build once, reuse)
- A **global-tile path utility** with border run-fill and crossing-aligned edge
  offsets — shared by items 5 and 6.
- A **shared local noise field keyed to global coordinates** — shared by items
  2, 3, 4 (apron), and 6 (river wobble).
- **Extending `LocalGenerationContext`** with `Elevation`, `Moisture`, and
  `BiomeDepth` — shared by items 2 and 3.

### Suggested validation per item
- Use `IslandPreviewGame` / `IslandPreviewGame.cs` + `GenerationParameterPanel`
  to eyeball each change against a fixed seed before/after.
- Keep the `IslandPlanner.RunQualityGate` on for items 1, 4, 6 (edge-band,
  land-coverage, enclosed-ocean, beach-ring, and singleton checks).
- Add connectivity assertions (via `IslandPathfinder`/`GridPathfinder`) for
  items 5, 6, and 8.