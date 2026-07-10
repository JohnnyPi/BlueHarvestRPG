using Game.Content.Definitions;
using Game.Generation.Biomes;
using Game.Generation.Island;
using Game.Generation.LocalMaps;
using Game.Generation.Passes;
using Game.Generation.Noise;
using Game.Generation.Validation;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.Seeds;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.LocalMaps;

public sealed class LocalMapGenerator : ILocalMapGenerator
{
    private readonly BoundaryConnectionPass _boundaryConnectionPass = new();
    private readonly StructureGenerationPass _structureGenerationPass = new();
    private readonly StructureStampPass _structureStampPass = new();
    private readonly FenceStampPass _fenceStampPass = new();
    private readonly TunnelStampPass _tunnelStampPass = new();
    private readonly RuinStampPass _ruinStampPass = new();
    private readonly FacilityRoadStampPass _facilityRoadStampPass = new();
    private readonly FacilityClearingPass _facilityClearingPass = new();
    private readonly RiverStampPass _riverStampPass = new();
    private readonly LavaFlowStampPass _lavaFlowStampPass = new();
    private readonly StructureDoorRestorePass _structureDoorRestorePass = new();
    private readonly NavigabilityValidator _navigabilityValidator = new();
    private readonly StructureBlueprintCatalog _blueprintCatalog;
    private readonly StructureFloorGenerator _floorGenerator;
    private readonly BiomeRulesDefinition _biomeRules;

    public LocalMapGenerator(
        StructureBlueprintCatalog? blueprintCatalog = null,
        BiomeRulesDefinition? biomeRules = null)
    {
        _blueprintCatalog = blueprintCatalog ?? StructureBlueprintCatalogDefaults.Create();
        _biomeRules = biomeRules ?? new BiomeRulesDefinition();
        _floorGenerator = new StructureFloorGenerator(_blueprintCatalog);
    }

    public LocalMap Generate(Overworld world, MapKey key)
    {
        if (key.IsStructureInterior)
        {
            return GenerateStructureFloor(world, key);
        }

        return GenerateSurface(world, key.WorldPosition);
    }

    private LocalMap GenerateStructureFloor(Overworld world, MapKey key)
    {
        if (world.IslandPlan is null)
        {
            throw new InvalidOperationException("Cannot generate structure floors without an island plan.");
        }

        StructurePlacement? structure = StructurePlacementQueries.FindByInstanceId(world.IslandPlan, key.StructureInstanceId);
        if (structure is null)
        {
            throw new InvalidOperationException($"Unknown structure instance {key.StructureInstanceId}.");
        }

        return _floorGenerator.Generate(world, key, structure);
    }

    public LocalMap GenerateSurface(Overworld world, WorldCoord coordinate)
    {
        var map = new LocalMap(MapKey.Surface(coordinate));
        WorldCell cell = world.GetCellValue(coordinate);
        IslandPlan? islandPlan = world.IslandPlan;

        ulong localSeed = SeedUtility.Derive(
            world.Seed,
            coordinate.X,
            coordinate.Y,
            WorldGeneratorVersion.Current);

        var random = new DeterministicRandom(localSeed);
        var terrainField = new LocalTerrainField(world.Seed);

        float elevation = cell.Elevation;
        float moisture = cell.Moisture;
        float biomeDepth = 0f;

        if (islandPlan is not null)
        {
            IslandCellData islandCell = islandPlan.GetCell(coordinate);
            elevation = islandCell.Elevation;
            moisture = islandCell.Moisture;
            int index = coordinate.Y * islandPlan.Width + coordinate.X;
            if (islandPlan.BiomeDepth.Length > index)
            {
                biomeDepth = islandPlan.BiomeDepth[index];
            }

            GenerateFromIslandCell(map, islandCell, terrainField, coordinate, elevation, moisture, biomeDepth);
        }
        else
        {
            GenerateFromBiome(map, cell.Biome, terrainField, coordinate, elevation, moisture, biomeDepth, random);
        }

        var context = new LocalGenerationContext
        {
            Seed = localSeed,
            WorldCoordinate = coordinate,
            WorldCell = cell,
            Connections = world.GetEdgeConnections(coordinate).ToArray(),
            IslandPlan = islandPlan,
            BlueprintCatalog = _blueprintCatalog,
            Elevation = elevation,
            Moisture = moisture,
            BiomeDepth = biomeDepth
        };

        if (islandPlan is not null)
        {
            _structureStampPass.Execute(map, context);
            _fenceStampPass.Execute(map, context);
            _tunnelStampPass.Execute(map, context);
            _ruinStampPass.Execute(map, context);
            _facilityClearingPass.Execute(map, context);
            _riverStampPass.Execute(map, context);
            _lavaFlowStampPass.Execute(map, context);
            _facilityRoadStampPass.Execute(map, context);
        }
        else
        {
            _structureGenerationPass.Execute(map, context);
        }

        _boundaryConnectionPass.Execute(map, context);

        if (islandPlan is not null)
        {
            _structureDoorRestorePass.Execute(map, context);
            ValidateNavigability(map, context);
        }

        return map;
    }

    private void GenerateFromIslandCell(
        LocalMap map,
        IslandCellData islandCell,
        LocalTerrainField field,
        WorldCoord coordinate,
        float elevation,
        float moisture,
        float biomeDepth)
    {
        if (!islandCell.IsLand)
        {
            GenerateOcean(map, field, coordinate);
            return;
        }

        if (BiomeBalanceHelper.HasEnterableRole(islandCell.Role))
        {
            GenerateFacilityTerrain(map, islandCell.Role, field, coordinate);
            return;
        }

        GenerateFromBiome(map, islandCell.Biome, field, coordinate, elevation, moisture, biomeDepth, null);
    }

    private void GenerateFromBiome(
        LocalMap map,
        BiomeId biome,
        LocalTerrainField field,
        WorldCoord coordinate,
        float elevation,
        float moisture,
        float biomeDepth,
        DeterministicRandom? random)
    {
        switch (biome)
        {
            case BiomeId.Beach:
                GenerateBeach(map, field, coordinate);
                break;
            case BiomeId.Jungle:
                GenerateJungle(map, field, coordinate, biomeDepth);
                break;
            case BiomeId.Forest:
                GenerateForest(map, field, coordinate, biomeDepth);
                break;
            case BiomeId.Swamp:
                GenerateSwamp(map, field, coordinate, moisture);
                break;
            case BiomeId.Mountains:
            case BiomeId.Volcanic:
                GenerateMountains(map, field, coordinate, elevation, biome == BiomeId.Volcanic);
                break;
            case BiomeId.Hills:
                GenerateHills(map, field, coordinate, elevation);
                break;
            case BiomeId.Plains:
                GeneratePlains(map, field, coordinate, biomeDepth);
                break;
            case BiomeId.Ocean:
                GenerateOcean(map, field, coordinate);
                break;
            case BiomeId.ShallowWater:
                GenerateShallowWater(map, field, coordinate);
                break;
            case BiomeId.Reef:
                GenerateReef(map, field, coordinate);
                break;
            default:
                GeneratePlains(map, field, coordinate, biomeDepth);
                break;
        }
    }

    private static void GenerateFacilityTerrain(
        LocalMap map,
        IslandCellRole role,
        LocalTerrainField field,
        WorldCoord coordinate)
    {
        if ((role & IslandCellRole.Dock) != 0)
        {
            GenerateFacilityDock(map, field, coordinate);
            return;
        }

        if ((role & IslandCellRole.Road) != 0)
        {
            GeneratePlains(map, field, coordinate, 0f);
            return;
        }

        if ((role & IslandCellRole.Paddock) != 0)
        {
            GenerateFacilityPaddock(map, field, coordinate);
            return;
        }

        if ((role & (IslandCellRole.Ruin | IslandCellRole.Fortification | IslandCellRole.Tunnel | IslandCellRole.Cavern)) != 0)
        {
            GenerateFacilityRuin(map, field, coordinate);
            return;
        }

        GenerateFacilityYard(map, field, coordinate);
    }

    private static void GenerateFacilityYard(LocalMap map, LocalTerrainField field, WorldCoord coordinate)
    {
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                (int gx, int gy) = LocalTerrainField.ToGlobalTile(coordinate.X, coordinate.Y, x, y);
                map.SetTerrain(x, y, TerrainId.Grass, TileFlags.None);
                if (field.SampleDensity(gx, gy) < 0.35f)
                {
                    map.SetTerrain(x, y, TerrainId.Dirt, TileFlags.None);
                }
            }
        }
    }

    private static void GenerateFacilityPaddock(LocalMap map, LocalTerrainField field, WorldCoord coordinate)
    {
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                (int gx, int gy) = LocalTerrainField.ToGlobalTile(coordinate.X, coordinate.Y, x, y);
                map.SetTerrain(x, y, TerrainId.Grass, TileFlags.None);
                if (field.SampleAccent(gx, gy) < 0.25f)
                {
                    map.SetTerrain(x, y, TerrainId.Dirt, TileFlags.None);
                }
            }
        }
    }

    private static void GenerateFacilityRuin(LocalMap map, LocalTerrainField field, WorldCoord coordinate)
    {
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                (int gx, int gy) = LocalTerrainField.ToGlobalTile(coordinate.X, coordinate.Y, x, y);
                map.SetTerrain(x, y, TerrainId.Grass, TileFlags.None);
                float density = field.SampleDensity(gx, gy);
                if (density < 0.18f)
                {
                    map.SetTerrain(x, y, TerrainId.Rock, TileFlags.BlocksMovement);
                }
                else if (density < 0.30f)
                {
                    map.SetTerrain(x, y, TerrainId.Dirt, TileFlags.None);
                }
            }
        }
    }

    private static void GenerateFacilityDock(LocalMap map, LocalTerrainField field, WorldCoord coordinate)
    {
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                (int gx, int gy) = LocalTerrainField.ToGlobalTile(coordinate.X, coordinate.Y, x, y);
                map.SetTerrain(x, y, TerrainId.Sand, TileFlags.None);
                if (field.SampleAccent(gx, gy) < 0.12f)
                {
                    map.SetTerrain(
                        x,
                        y,
                        TerrainId.ShallowWater,
                        TileFlags.BlocksMovement | TileFlags.ContainsWater);
                }
            }
        }
    }

    private void ValidateNavigability(LocalMap map, LocalGenerationContext context)
    {
        if (context.IslandPlan is null)
        {
            return;
        }

        LocalCoord entry = ResolveEntryPoint(map, context);
        _navigabilityValidator.ValidateAndRepair(map, entry);
        ValidateStructureDoorApproach(map, context);
    }

    private void ValidateStructureDoorApproach(LocalMap map, LocalGenerationContext context)
    {
        if (context.IslandPlan is null || context.BlueprintCatalog is null)
        {
            return;
        }

        foreach (StructurePlacement structure in context.IslandPlan.Structures)
        {
            StructureBlueprintDefinition blueprint = context.BlueprintCatalog.ResolveById(structure.BlueprintId);
            if (StructurePlacementQueries.DoorCell(structure, blueprint) != context.WorldCoordinate)
            {
                continue;
            }

            LocalCoord door = StructurePlacementQueries.SurfaceDoorLocal(structure, blueprint);
            LocalCoord? nearestRoad = FindNearestTerrain(map, door, TerrainId.Road);
            if (nearestRoad is not null)
            {
                _navigabilityValidator.EnsureConnected(map, nearestRoad.Value, door);
            }
        }
    }

    private static LocalCoord? FindNearestTerrain(LocalMap map, LocalCoord target, TerrainId terrain)
    {
        LocalCoord? nearest = null;
        int bestDistance = int.MaxValue;
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                if (map.Terrain[map.GetIndex(x, y)] != terrain)
                {
                    continue;
                }

                int distance = Math.Abs(x - target.X) + Math.Abs(y - target.Y);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = new LocalCoord(x, y);
                }
            }
        }

        return nearest;
    }

    private LocalCoord ResolveEntryPoint(LocalMap map, LocalGenerationContext context)
    {
        if (context.IslandPlan is not null &&
            context.BlueprintCatalog is not null &&
            OverworldLandmarkCatalog.TryResolveEntryPoint(
                context.IslandPlan,
                context.WorldCoordinate,
                map,
                out LocalCoord landmarkEntry))
        {
            return landmarkEntry;
        }

        return new LocalCoord(LocalMap.Width / 2, LocalMap.Height / 2);
    }

    private static void GenerateForest(
        LocalMap map,
        LocalTerrainField field,
        WorldCoord coordinate,
        float biomeDepth)
    {
        float treeThreshold = 0.72f - biomeDepth * 0.12f;
        float rockThreshold = 0.88f;

        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                (int gx, int gy) = LocalTerrainField.ToGlobalTile(coordinate.X, coordinate.Y, x, y);
                map.SetTerrain(x, y, TerrainId.Grass, TileFlags.None);

                float density = field.SampleDensity(gx, gy);
                if (density > treeThreshold)
                {
                    map.SetTerrain(
                        x,
                        y,
                        TerrainId.Tree,
                        TileFlags.BlocksMovement | TileFlags.BlocksVision);
                }
                else if (density > rockThreshold)
                {
                    map.SetTerrain(x, y, TerrainId.Rock, TileFlags.BlocksMovement);
                }
            }
        }
    }

    private static void GenerateJungle(
        LocalMap map,
        LocalTerrainField field,
        WorldCoord coordinate,
        float biomeDepth)
    {
        float depth = Math.Clamp(biomeDepth, 0f, 1f);
        float canopyThreshold = Lerp(0.96f, 0.76f, depth);
        float treeThreshold = Lerp(0.84f, 0.60f, depth);
        float undergrowthThreshold = Lerp(0.70f, 0.46f, depth);
        float rockAccentThreshold = Lerp(0.16f, 0.08f, depth);
        float dirtAccentThreshold = Lerp(0.30f, 0.18f, depth);

        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                (int gx, int gy) = LocalTerrainField.ToGlobalTile(coordinate.X, coordinate.Y, x, y);
                map.SetTerrain(x, y, TerrainId.Grass, TileFlags.None);

                float density = field.SampleDensity(gx, gy);
                float accent = field.SampleAccent(gx, gy);

                if (density > canopyThreshold)
                {
                    map.SetTerrain(
                        x,
                        y,
                        TerrainId.DenseCanopy,
                        TileFlags.BlocksMovement | TileFlags.BlocksVision);
                }
                else if (density > treeThreshold)
                {
                    map.SetTerrain(
                        x,
                        y,
                        TerrainId.Tree,
                        TileFlags.BlocksMovement | TileFlags.BlocksVision);
                }
                else if (density > undergrowthThreshold)
                {
                    map.SetTerrain(
                        x,
                        y,
                        TerrainId.Undergrowth,
                        TileFlags.BlocksMovement | TileFlags.BlocksVision);
                }
                else if (accent < rockAccentThreshold)
                {
                    map.SetTerrain(x, y, TerrainId.Rock, TileFlags.BlocksMovement);
                }
                else if (accent < dirtAccentThreshold)
                {
                    map.SetTerrain(x, y, TerrainId.Dirt, TileFlags.None);
                }
            }
        }
    }

    private static float Lerp(float start, float end, float amount)
        => start + (end - start) * amount;

    private static void GeneratePlains(
        LocalMap map,
        LocalTerrainField field,
        WorldCoord coordinate,
        float biomeDepth)
    {
        float treeThreshold = 0.90f - biomeDepth * 0.04f;
        float dirtThreshold = 0.55f;

        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                (int gx, int gy) = LocalTerrainField.ToGlobalTile(coordinate.X, coordinate.Y, x, y);
                map.SetTerrain(x, y, TerrainId.Grass, TileFlags.None);

                float density = field.SampleDensity(gx, gy);
                if (density > treeThreshold)
                {
                    map.SetTerrain(
                        x,
                        y,
                        TerrainId.Tree,
                        TileFlags.BlocksMovement | TileFlags.BlocksVision);
                }
                else if (density < dirtThreshold)
                {
                    map.SetTerrain(x, y, TerrainId.Dirt, TileFlags.None);
                }
            }
        }
    }

    private static void GenerateSwamp(
        LocalMap map,
        LocalTerrainField field,
        WorldCoord coordinate,
        float moisture)
    {
        float waterThreshold = 0.72f - moisture * 0.12f;
        float treeThreshold = 0.82f;

        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                (int gx, int gy) = LocalTerrainField.ToGlobalTile(coordinate.X, coordinate.Y, x, y);
                map.SetTerrain(x, y, TerrainId.Mud, TileFlags.None);

                float density = field.SampleDensity(gx, gy);
                if (density < waterThreshold)
                {
                    map.SetTerrain(
                        x,
                        y,
                        TerrainId.ShallowWater,
                        TileFlags.BlocksMovement | TileFlags.ContainsWater);
                }
                else if (density > treeThreshold)
                {
                    map.SetTerrain(
                        x,
                        y,
                        TerrainId.Tree,
                        TileFlags.BlocksMovement | TileFlags.BlocksVision);
                }
            }
        }
    }

    private void GenerateHills(
        LocalMap map,
        LocalTerrainField field,
        WorldCoord coordinate,
        float elevation)
    {
        HighlandBand band = HeightBandResolver.Resolve(elevation, _biomeRules);
        float rockThreshold = band switch
        {
            HighlandBand.Foothills => 0.86f,
            HighlandBand.Hills => 0.78f,
            HighlandBand.SmallMountains => 0.68f,
            _ => 0.60f
        };
        float dirtThreshold = rockThreshold + 0.08f;

        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                (int gx, int gy) = LocalTerrainField.ToGlobalTile(coordinate.X, coordinate.Y, x, y);
                map.SetTerrain(x, y, TerrainId.Grass, TileFlags.None);

                float density = field.SampleDensity(gx, gy);
                if (density > rockThreshold)
                {
                    map.SetTerrain(x, y, TerrainId.Rock, TileFlags.BlocksMovement);
                }
                else if (density > dirtThreshold)
                {
                    map.SetTerrain(x, y, TerrainId.Dirt, TileFlags.None);
                }
            }
        }
    }

    private void GenerateMountains(
        LocalMap map,
        LocalTerrainField field,
        WorldCoord coordinate,
        float elevation,
        bool volcanic)
    {
        HighlandBand band = HeightBandResolver.Resolve(elevation, _biomeRules);
        float grassThreshold = band switch
        {
            HighlandBand.Foothills => 0.94f,
            HighlandBand.Hills => 0.90f,
            HighlandBand.SmallMountains => 0.84f,
            _ => 0.78f
        };

        if (volcanic)
        {
            grassThreshold += 0.04f;
        }

        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                (int gx, int gy) = LocalTerrainField.ToGlobalTile(coordinate.X, coordinate.Y, x, y);
                map.SetTerrain(x, y, TerrainId.Rock, TileFlags.BlocksMovement);

                float density = field.SampleDensity(gx, gy);
                if (density > grassThreshold)
                {
                    map.SetTerrain(x, y, volcanic ? TerrainId.Dirt : TerrainId.Grass, TileFlags.None);
                }
            }
        }
    }

    private static void GenerateBeach(LocalMap map, LocalTerrainField field, WorldCoord coordinate)
    {
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                (int gx, int gy) = LocalTerrainField.ToGlobalTile(coordinate.X, coordinate.Y, x, y);
                map.SetTerrain(x, y, TerrainId.Sand, TileFlags.None);

                if (field.SampleAccent(gx, gy) < 0.15f)
                {
                    map.SetTerrain(
                        x,
                        y,
                        TerrainId.ShallowWater,
                        TileFlags.BlocksMovement | TileFlags.ContainsWater);
                }
            }
        }
    }

    private static void GenerateOcean(LocalMap map, LocalTerrainField field, WorldCoord coordinate)
    {
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                map.SetTerrain(
                    x,
                    y,
                    TerrainId.DeepWater,
                    TileFlags.BlocksMovement | TileFlags.ContainsWater);
            }
        }
    }

    private static void GenerateShallowWater(LocalMap map, LocalTerrainField field, WorldCoord coordinate)
    {
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                map.SetTerrain(
                    x,
                    y,
                    TerrainId.ShallowWater,
                    TileFlags.BlocksMovement | TileFlags.ContainsWater);
            }
        }
    }

    private static void GenerateReef(LocalMap map, LocalTerrainField field, WorldCoord coordinate)
    {
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                (int gx, int gy) = LocalTerrainField.ToGlobalTile(coordinate.X, coordinate.Y, x, y);
                bool sand = field.SampleDensity(gx, gy) > 0.72f;
                map.SetTerrain(
                    x,
                    y,
                    sand ? TerrainId.Sand : TerrainId.ShallowWater,
                    sand ? TileFlags.None : TileFlags.BlocksMovement | TileFlags.ContainsWater);
            }
        }
    }
}
