The big shift is: **do not let Voronoi cells define the island silhouette**. Use Voronoi for regions, biomes, watersheds, roads, or geological zones, but build the island outline from **smooth fields, blobs, splines, erosion, and distance-to-coast masks**.

The uploaded Isla Nublar image has a few recognizable shape traits:

* A **large rounded north/northwest landmass**
* A **smaller eastern lobe**
* A **long tapering southern peninsula**
* Several **concave bays**, especially on the east/southeast side
* A mostly smooth coastline with **small irregular erosion**, not noisy jaggedness everywhere
* A visible **shallow-water shelf / reef band** around parts of the coast
* Interior structure: darker jungle core, lighter ridges/roads/rivers, and a few cleared areas

## 1. Build the macro island from “blobby primitives,” not cells

A good Isla Nublar-style silhouette can be made as a union of several overlapping ellipses or metaballs:

```text
        large rounded north mass
              ________
          ___/        \__
       __/               \__
      /                    _\ eastern lobe
     |                   _/
     |                 _/
      \              _/
       \            /
        \          /
         \        /
          \      /
           \    /   southern taper
            \__/
```

Use something like:

* Blob A: big northwest oval
* Blob B: central mass
* Blob C: eastern lobe
* Blob D: long southern teardrop/peninsula
* Subtractive blobs: carve bays and coves

Conceptually:

```rust
let north_mass  = ellipse_sdf(p, center=(-0.20,  0.25), radius=(0.75, 0.55));
let east_lobe   = ellipse_sdf(p, center=( 0.38,  0.05), radius=(0.45, 0.42));
let south_taper = ellipse_sdf(p, center=(-0.12, -0.48), radius=(0.32, 0.62));

let mut island = smooth_union(north_mass, east_lobe, 0.20);
island = smooth_union(island, south_taper, 0.18);

// Carve bays
let east_bay = ellipse_sdf(p, center=(0.62, -0.18), radius=(0.30, 0.22));
let ne_bay   = ellipse_sdf(p, center=(0.46,  0.36), radius=(0.28, 0.18));

island = smooth_subtract(island, east_bay, 0.12);
island = smooth_subtract(island, ne_bay, 0.10);
```

That gives you an island that has a **recognizable designed silhouette** before you add randomness.

## 2. Add domain warping to hide artificial smoothness

Once you have the clean blob shape, warp the coordinate field before evaluating it:

```rust
let warp = vec2(
    fbm(p * 1.7 + seed_a),
    fbm(p * 1.7 + seed_b),
) * 0.08;

let q = p + warp;
let island = island_sdf(q);
```

Use **low-frequency noise** for the coastline. Avoid high-frequency noise at this stage. High-frequency coastline noise makes the island look like torn paper instead of a plausible tropical island.

Good rule:

```text
macro shape:     blobs / splines / radial mask
medium detail:   domain warp + smooth erosion
small detail:    only near beaches, rocks, cliffs, river mouths
```

## 3. Use signed distance from coast as your master field

After you have the island mask, compute a signed distance field:

```text
distance_to_coast > 0  = inland
distance_to_coast = 0  = shoreline
distance_to_coast < 0  = ocean
```

This one field can drive almost everything:

```rust
let coast_dist = signed_distance_to_coast(p);

let beach_mask = smoothstep(0.00, 0.04, coast_dist)
               * (1.0 - smoothstep(0.06, 0.12, coast_dist));

let inland_mask = smoothstep(0.08, 0.35, coast_dist);

let shallow_water = smoothstep(-0.18, -0.02, coast_dist)
                  * (1.0 - smoothstep(-0.02, 0.00, coast_dist));
```

For the Isla Nublar look, the shallow water should not be uniform. Put stronger turquoise shelf/reef patches near **concave bays**, especially east and southeast.

## 4. Shape the terrain with ridges, not just height noise

A common problem is making islands with a radial height gradient plus noise. That creates a “muffin island.” Isla Nublar looks more like a jungle-covered volcanic island with internal ridges.

Use ridge splines:

```text
main ridge: north/central → southeast
secondary ridge: central → eastern lobe
southern descending ridge: central → southern peninsula
```

Represent ridges as polylines/splines. Then height contribution is based on distance to those lines:

```rust
let ridge_distance = distance_to_nearest_ridge(p);

let ridge_height =
    exp(-ridge_distance * ridge_distance / ridge_width)
    * ridge_strength;
```

Then combine:

```rust
height =
    sea_level
    + coastal_ramp(coast_dist)
    + broad_volcanic_dome(p)
    + ridge_height
    + fbm(p * 3.0) * 0.08
    + ridged_noise(p * 7.0) * 0.03;
```

The important bit: **the island’s mountains should have structure**. Use noise to roughen the terrain, not to invent the whole terrain.

## 5. Make bays and coves intentional

The reference island has believable concavity. Add subtractive coastal features:

* Northeast inlet / lagoon
* Southeast bay
* Small western bite
* Narrow southern coastline taper
* Eastern bulge with carved coves

Algorithmically, think of this as **boolean sculpting**:

```text
island = union(north_blob, east_blob, south_blob)
island = subtract(east_bay)
island = subtract(northeast_lagoon)
island = subtract(western_cove)
island = warp(island)
island = smooth/island-cleanup
```

After subtraction, apply a smoothing or morphological pass so the cuts do not look like perfect circles.

## 6. Use Voronoi softly

Your current Voronoi cells probably look too defined because the cell borders are influencing visible terrain too directly.

Better uses for Voronoi:

### Good Voronoi uses

* Regional biome seeds
* Geology zones
* Watershed basins
* Points of interest
* Terrain material variation
* Forest density clusters
* Dinosaur territories / ecological zones
* Human infrastructure zones

### Avoid

* Hard coastline borders
* Hard biome color transitions
* Hard elevation plateaus
* Visible polygonal region edges

Instead of:

```rust
biome = nearest_voronoi_cell(p);
```

Use:

```rust
let weights = k_nearest_voronoi_weights(p, 3);
let biome_value =
    weights[0] * biome_a +
    weights[1] * biome_b +
    weights[2] * biome_c;
```

Then warp the lookup position:

```rust
let q = p + fbm_vector(p * 2.0) * 0.05;
let region = soft_voronoi(q);
```

This keeps the regional organization without making the cells obvious.

## 7. Add shallow shelves, reefs, and drop-offs

The uploaded image’s ocean is not just “water next to land.” It has light cyan shallow patches near the island.

Generate bathymetry using distance from coast:

```rust
if coast_dist < 0.0 {
    ocean_depth =
        -smoothstep(0.0, shelf_width, -coast_dist) * shelf_depth
        -smoothstep(shelf_width, deep_width, -coast_dist) * deep_ocean_depth;
}
```

Then modulate shelf width:

```rust
let shelf_width =
    base_shelf_width
    + fbm(p * 2.0) * 0.08
    + bay_bonus(p);
```

Make shelf wider in bays and on the eastern/southeastern side:

```rust
let east_bias = smoothstep(0.0, 1.0, p.x);
let bay_bonus = concavity_mask * 0.15;
```

That gives you the “tropical satellite map” feel.

## 8. Recommended generation pipeline

For this kind of island, I would use this order:

```text
1. Normalize world coordinates to -1..1 island space.

2. Generate macro silhouette:
   - smooth union of ellipses/metaballs
   - optional artist-authored control points
   - subtract bay/cove primitives

3. Apply low-frequency domain warp.

4. Convert silhouette to signed distance field.

5. Clean coastline:
   - remove tiny disconnected islands
   - smooth tiny one-tile artifacts
   - preserve major bays/capes

6. Generate elevation:
   - coast distance ramp
   - volcanic dome
   - ridge splines
   - low/mid-frequency terrain noise
   - cliff masks where slope is high

7. Generate hydrology:
   - ridges define drainage basins
   - rivers follow descending flow
   - river mouths favor bays
   - wetlands/lagoons near low coast

8. Generate bathymetry:
   - shallow shelf around coast
   - wider shelves in bays
   - reef/coral patches in shallow warm water
   - steep drop-off farther out

9. Generate biomes:
   - jungle core
   - beaches
   - cliffs
   - wetlands
   - volcanic uplands
   - cleared areas
   - reef/shallow water/deep ocean

10. Use Voronoi only for soft regions:
   - territories
   - vegetation variation
   - geology/material clusters
   - human site placement
```

## 9. A practical “Isla Nublar recipe”

For a generator config, you might represent the macro island like this:

```yaml
island_shape:
  coordinate_space: normalized_-1_to_1

  additive_blobs:
    - name: north_mass
      center: [-0.22, 0.28]
      radius: [0.78, 0.55]
      rotation_degrees: -8
      strength: 1.0

    - name: east_lobe
      center: [0.36, 0.05]
      radius: [0.45, 0.42]
      rotation_degrees: 12
      strength: 0.85

    - name: south_taper
      center: [-0.12, -0.50]
      radius: [0.32, 0.62]
      rotation_degrees: -5
      strength: 0.75

  subtractive_bays:
    - name: southeast_bay
      center: [0.58, -0.22]
      radius: [0.34, 0.24]
      strength: 0.75

    - name: northeast_lagoon
      center: [0.42, 0.38]
      radius: [0.28, 0.17]
      strength: 0.55

    - name: west_cove
      center: [-0.74, -0.10]
      radius: [0.18, 0.25]
      strength: 0.35

  domain_warp:
    frequency: 1.6
    amplitude: 0.07
    octaves: 3

  coastline_detail:
    frequency: 5.0
    amplitude: 0.025
    preserve_large_bays: true
```

That gives you a repeatable, art-directable silhouette while still being procedural.

## 10. The key mental model

Think of the island as layered fields:

```text
macro mask      = designed island silhouette
coast distance  = beaches, cliffs, shallow water
ridge field     = mountains and drainage
moisture field  = jungle, swamp, dry scrub
slope field     = cliffs, rock, traversability
voronoi field   = soft regions, not visible cells
noise fields    = variation, not structure
```

For an Isla Nublar-like result, the **macro silhouette should be semi-designed**, while the terrain, biomes, rivers, reefs, and details can be procedural. The closer the island is to an iconic shape, the more you want controlled proceduralism rather than pure randomness.
