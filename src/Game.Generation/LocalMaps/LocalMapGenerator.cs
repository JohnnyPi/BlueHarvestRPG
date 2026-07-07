using Game.Simulation.World.Island;
using Game.Generation.LocalMaps;
using Game.Generation.Passes;
using Game.Generation.Noise;
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
            GenerateFromIslandCell(map, islandPlan.GetCell(coordinate), random);
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
                case BiomeId.Hills:
                case BiomeId.Volcanic:
                    GenerateMountains(map, random, cell.Biome == BiomeId.Volcanic);
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
        }
        else
        {
            _structureGenerationPass.Execute(map, context);
        }

        _boundaryConnectionPass.Execute(map, context);

        return map;
    }

    private static void GenerateFromIslandCell(LocalMap map, IslandCellData islandCell, DeterministicRandom random)
    {
        if (!islandCell.IsLand)
        {
            GenerateOcean(map, random);
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
            case BiomeId.Hills:
            case BiomeId.Volcanic:
                GenerateMountains(map, random, islandCell.Biome == BiomeId.Volcanic);
                break;
            case BiomeId.Plains:
                GeneratePlains(map, random);
                break;
            default:
                GeneratePlains(map, random);
                break;
        }
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
