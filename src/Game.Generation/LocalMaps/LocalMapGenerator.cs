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
    private readonly StructureDoorRestorePass _structureDoorRestorePass = new();
    private readonly NavigabilityValidator _navigabilityValidator = new();
    private readonly StructureBlueprintCatalog _blueprintCatalog;
    private readonly StructureFloorGenerator _floorGenerator;

    public LocalMapGenerator(StructureBlueprintCatalog? blueprintCatalog = null)
    {
        _blueprintCatalog = blueprintCatalog ?? StructureBlueprintCatalogDefaults.Create();
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

        if (islandPlan is not null)
        {
            IslandCellData islandCell = islandPlan.GetCell(coordinate);
            GenerateFromIslandCell(map, islandCell, random);
        }
        else
        {
            switch (cell.Biome)
            {
                case BiomeId.Forest:
                case BiomeId.Jungle:
                    GenerateForest(map, random, cell.Biome == BiomeId.Jungle);
                    break;
                case BiomeId.Plains:
                    GeneratePlains(map, random);
                    break;
                case BiomeId.Swamp:
                    GenerateSwamp(map, random);
                    break;
                case BiomeId.Mountains:
                case BiomeId.Volcanic:
                    GenerateMountains(map, random, cell.Biome == BiomeId.Volcanic);
                    break;
                case BiomeId.Hills:
                    GenerateHills(map, random);
                    break;
                case BiomeId.Beach:
                    GenerateBeach(map, random);
                    break;
                case BiomeId.Ocean:
                    GenerateOcean(map, random);
                    break;
                default:
                    GeneratePlains(map, random);
                    break;
            }
        }

        var context = new LocalGenerationContext
        {
            Seed = localSeed,
            WorldCoordinate = coordinate,
            WorldCell = cell,
            Connections = world.GetEdgeConnections(coordinate).ToArray(),
            IslandPlan = islandPlan,
            BlueprintCatalog = _blueprintCatalog
        };

        if (islandPlan is not null)
        {
            _structureStampPass.Execute(map, context);
            _fenceStampPass.Execute(map, context);
            _tunnelStampPass.Execute(map, context);
            _ruinStampPass.Execute(map, context);
            _facilityClearingPass.Execute(map, context);
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

    private static void GenerateFromIslandCell(LocalMap map, IslandCellData islandCell, DeterministicRandom random)
    {
        if (!islandCell.IsLand)
        {
            GenerateOcean(map, random);
            return;
        }

        if (BiomeBalanceHelper.HasEnterableRole(islandCell.Role))
        {
            GenerateFacilityTerrain(map, islandCell.Role, random);
            return;
        }

        switch (islandCell.Biome)
        {
            case BiomeId.Beach:
                GenerateBeach(map, random);
                break;
            case BiomeId.Forest:
            case BiomeId.Jungle:
                GenerateForest(map, random, islandCell.Biome == BiomeId.Jungle);
                break;
            case BiomeId.Swamp:
                GenerateSwamp(map, random);
                break;
            case BiomeId.Mountains:
            case BiomeId.Volcanic:
                GenerateMountains(map, random, islandCell.Biome == BiomeId.Volcanic);
                break;
            case BiomeId.Hills:
                GenerateHills(map, random);
                break;
            case BiomeId.Plains:
                GeneratePlains(map, random);
                break;
            default:
                GeneratePlains(map, random);
                break;
        }
    }

    private static void GenerateFacilityTerrain(LocalMap map, IslandCellRole role, DeterministicRandom random)
    {
        if ((role & IslandCellRole.Dock) != 0)
        {
            GenerateFacilityDock(map, random);
            return;
        }

        if ((role & IslandCellRole.Road) != 0)
        {
            GeneratePlains(map, random);
            return;
        }

        if ((role & IslandCellRole.Paddock) != 0)
        {
            GenerateFacilityPaddock(map, random);
            return;
        }

        if ((role & (IslandCellRole.Ruin | IslandCellRole.Fortification | IslandCellRole.Tunnel | IslandCellRole.Cavern)) != 0)
        {
            GenerateFacilityRuin(map, random);
            return;
        }

        GenerateFacilityYard(map, random);
    }

    private static void GenerateFacilityYard(LocalMap map, DeterministicRandom random)
    {
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                map.SetTerrain(x, y, TerrainId.Grass, TileFlags.None);
                float roll = random.NextFloat();
                if (roll < 0.06f)
                {
                    map.SetTerrain(x, y, TerrainId.Dirt, TileFlags.None);
                }
            }
        }
    }

    private static void GenerateFacilityPaddock(LocalMap map, DeterministicRandom random)
    {
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                map.SetTerrain(x, y, TerrainId.Grass, TileFlags.None);
                if (random.NextFloat() < 0.04f)
                {
                    map.SetTerrain(x, y, TerrainId.Dirt, TileFlags.None);
                }
            }
        }
    }

    private static void GenerateFacilityRuin(LocalMap map, DeterministicRandom random)
    {
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                map.SetTerrain(x, y, TerrainId.Grass, TileFlags.None);
                float roll = random.NextFloat();
                if (roll < 0.02f)
                {
                    map.SetTerrain(x, y, TerrainId.Rock, TileFlags.BlocksMovement);
                }
                else if (roll < 0.06f)
                {
                    map.SetTerrain(x, y, TerrainId.Dirt, TileFlags.None);
                }
            }
        }
    }

    private static void GenerateFacilityDock(LocalMap map, DeterministicRandom random)
    {
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                map.SetTerrain(x, y, TerrainId.Sand, TileFlags.None);
                if (random.NextFloat() < 0.03f)
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

    private static void GenerateForest(LocalMap map, DeterministicRandom random, bool denseJungle)
    {
        float treeChance = denseJungle ? 0.28f : 0.18f;
        float rockChance = denseJungle ? 0.04f : 0.03f;

        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                map.SetTerrain(x, y, TerrainId.Grass, TileFlags.None);

                float roll = random.NextFloat();
                if (roll < treeChance)
                {
                    map.SetTerrain(
                        x,
                        y,
                        TerrainId.Tree,
                        TileFlags.BlocksMovement | TileFlags.BlocksVision);
                }
                else if (roll < treeChance + rockChance)
                {
                    map.SetTerrain(x, y, TerrainId.Rock, TileFlags.BlocksMovement);
                }
            }
        }
    }

    private static void GeneratePlains(LocalMap map, DeterministicRandom random)
    {
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                map.SetTerrain(x, y, TerrainId.Grass, TileFlags.None);

                float roll = random.NextFloat();
                if (roll < 0.03f)
                {
                    map.SetTerrain(
                        x,
                        y,
                        TerrainId.Tree,
                        TileFlags.BlocksMovement | TileFlags.BlocksVision);
                }
                else if (roll < 0.10f)
                {
                    map.SetTerrain(x, y, TerrainId.Dirt, TileFlags.None);
                }
            }
        }
    }

    private static void GenerateSwamp(LocalMap map, DeterministicRandom random)
    {
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                map.SetTerrain(x, y, TerrainId.Mud, TileFlags.None);

                float roll = random.NextFloat();
                if (roll < 0.15f)
                {
                    map.SetTerrain(
                        x,
                        y,
                        TerrainId.ShallowWater,
                        TileFlags.BlocksMovement | TileFlags.ContainsWater);
                }
                else if (roll < 0.25f)
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

    private static void GenerateHills(LocalMap map, DeterministicRandom random)
    {
        const float rockChance = 0.30f;
        const float dirtChance = 0.12f;

        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                map.SetTerrain(x, y, TerrainId.Grass, TileFlags.None);

                float roll = random.NextFloat();
                if (roll < rockChance)
                {
                    map.SetTerrain(x, y, TerrainId.Rock, TileFlags.BlocksMovement);
                }
                else if (roll < rockChance + dirtChance)
                {
                    map.SetTerrain(x, y, TerrainId.Dirt, TileFlags.None);
                }
            }
        }
    }

    private static void GenerateMountains(LocalMap map, DeterministicRandom random, bool volcanic)
    {
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                map.SetTerrain(x, y, TerrainId.Rock, TileFlags.BlocksMovement);

                float roll = random.NextFloat();
                if (volcanic && roll < 0.08f)
                {
                    map.SetTerrain(x, y, TerrainId.Dirt, TileFlags.None);
                }
                else if (!volcanic && roll < 0.12f)
                {
                    map.SetTerrain(x, y, TerrainId.Grass, TileFlags.None);
                }
            }
        }
    }

    private static void GenerateBeach(LocalMap map, DeterministicRandom random)
    {
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                map.SetTerrain(x, y, TerrainId.Sand, TileFlags.None);

                float roll = random.NextFloat();
                if (roll < 0.08f)
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

    private static void GenerateOcean(LocalMap map, DeterministicRandom random)
    {
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                float roll = random.NextFloat();
                if (roll < 0.35f)
                {
                    map.SetTerrain(
                        x,
                        y,
                        TerrainId.ShallowWater,
                        TileFlags.BlocksMovement | TileFlags.ContainsWater);
                }
                else
                {
                    map.SetTerrain(
                        x,
                        y,
                        TerrainId.DeepWater,
                        TileFlags.BlocksMovement | TileFlags.ContainsWater);
                }
            }
        }
    }
}
