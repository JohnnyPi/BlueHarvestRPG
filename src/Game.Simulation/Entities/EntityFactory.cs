using Game.Simulation.AI;
using Game.Simulation.Coordinates;
using Game.Simulation.Factions;
using Game.Simulation.LocalMaps;
using Game.Simulation.Seeds;
using Game.Simulation.Session;
using Game.Simulation.Time;
using Game.Simulation.World;

namespace Game.Simulation.Entities;

public static class EntityFactory
{
    private const uint HarvestableTreeSalt = 0xE17A_0001;
    private const uint RaptorSalt = 0xE17A_0003;

    public static EntityId CreateDeterministicId(
        ulong worldSeed,
        WorldCoord mapCoordinate,
        EntityKind kind,
        int ordinal = 0)
    {
        uint kindSalt = ((uint)kind << 16) | (uint)ordinal;
        ulong value = SeedUtility.Derive(worldSeed, mapCoordinate.X, mapCoordinate.Y, kindSalt);

        if (EntityId.IsReserved(value))
        {
            value = SeedUtility.Derive(worldSeed, mapCoordinate.X, mapCoordinate.Y, kindSalt + 0x1000);
        }

        if (EntityId.IsReserved(value))
        {
            value = 1;
        }

        return new EntityId(value);
    }

    public static void SpawnDefaults(Overworld world, LocalMap map)
    {
        if (map.Entities.All.Count > 0)
        {
            return;
        }

        SpawnHarvestableTree(world, map);

        BiomeId biome = world.GetCellValue(map.WorldPosition).Biome;
        if (biome is BiomeId.Forest or BiomeId.Jungle)
        {
            SpawnRaptor(world, map, ordinal: 0);
            if (biome == BiomeId.Jungle)
            {
                SpawnRaptor(world, map, ordinal: 1);
            }
        }
    }

    public static bool TrySpawnEncounterPredator(GameSession session)
    {
        if (session.ViewMode != GameViewMode.LocalMap || session.ActiveLocalMap is null)
        {
            return false;
        }

        LocalMap map = session.ActiveLocalMap;
        LocalCoord? position = PickEncounterPosition(session.Overworld.Seed, map, session.PlayerLocalPosition);
        if (position is null)
        {
            return false;
        }

        if (map.Entities.GetAt(position.Value) is not null)
        {
            return false;
        }

        map.Entities.Add(CreateRaptor(
            session.Overworld.Seed,
            map.WorldPosition,
            position.Value,
            ordinal: 50,
            ambush: true));

        session.MarkRenderDirty();
        return true;
    }

    public static bool TrySpawnPressurePredator(GameSession session)
    {
        if (session.ViewMode != GameViewMode.LocalMap || session.ActiveLocalMap is null)
        {
            return false;
        }

        LocalMap map = session.ActiveLocalMap;
        LocalCoord? position = PickPressurePredatorPosition(session.Overworld.Seed, map, session.PlayerLocalPosition);
        if (position is null)
        {
            return false;
        }

        if (map.Entities.GetAt(position.Value) is not null)
        {
            return false;
        }

        map.Entities.Add(CreateRaptor(
            session.Overworld.Seed,
            map.WorldPosition,
            position.Value,
            ordinal: 900 + session.PressureClock.LastEventThreshold,
            ambush: true));

        session.MarkRenderDirty();
        return true;
    }

    private static Entity CreateRaptor(
        ulong worldSeed,
        WorldCoord mapCoordinate,
        LocalCoord localPosition,
        int ordinal,
        bool ambush = false)
    {
        return new Entity
        {
            Id = CreateDeterministicId(worldSeed, mapCoordinate, EntityKind.Raptor, ordinal),
            Kind = EntityKind.Raptor,
            WorldPosition = mapCoordinate,
            LocalPosition = localPosition,
            BlocksMovement = true,
            IsActive = true,
            Faction = FactionId.Wildlife,
            Actor = new ActorTurnState { Speed = 130, Energy = ActionCostTable.ActionThreshold },
            MaxHealth = 24,
            Health = 24,
            Raptor = new RaptorMemory
            {
                Phase = ambush ? RaptorPhase.Ambush : RaptorPhase.Stalk
            }
        };
    }

    public static Entity CreatePlayer(
        WorldCoord worldPosition,
        LocalCoord localPosition,
        ActorTurnState? actor = null,
        int health = 100,
        int maxHealth = 100)
    {
        return new Entity
        {
            Id = EntityId.Player,
            Kind = EntityKind.Player,
            WorldPosition = worldPosition,
            LocalPosition = localPosition,
            BlocksMovement = false,
            IsActive = true,
            Faction = FactionId.Player,
            Actor = actor,
            MaxHealth = maxHealth,
            Health = health
        };
    }

    private static void SpawnHarvestableTree(Overworld world, LocalMap map)
    {
        LocalCoord? position = PickWalkablePosition(world.Seed, map, HarvestableTreeSalt);
        if (position is null)
        {
            return;
        }

        map.Entities.Add(new Entity
        {
            Id = CreateDeterministicId(world.Seed, map.WorldPosition, EntityKind.HarvestableTree),
            Kind = EntityKind.HarvestableTree,
            WorldPosition = map.WorldPosition,
            LocalPosition = position.Value,
            BlocksMovement = true,
            IsActive = true,
            Faction = FactionId.Neutral
        });
    }

    private static void SpawnRaptor(Overworld world, LocalMap map, int ordinal)
    {
        LocalCoord? position = PickWalkablePosition(world.Seed, map, RaptorSalt + (uint)ordinal);
        if (position is null)
        {
            return;
        }

        map.Entities.Add(CreateRaptor(world.Seed, map.WorldPosition, position.Value, ordinal));
    }

    private static LocalCoord? PickWalkablePosition(ulong worldSeed, LocalMap map, uint salt)
    {
        var candidates = new List<LocalCoord>();

        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                var coord = new LocalCoord(x, y);
                if (IsTerrainWalkable(map, coord) && map.Entities.GetAt(coord) is null)
                {
                    candidates.Add(coord);
                }
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        ulong hash = SeedUtility.Derive(worldSeed, map.WorldPosition.X, map.WorldPosition.Y, salt);
        int index = (int)(hash % (ulong)candidates.Count);
        return candidates[index];
    }

    private static bool IsTerrainWalkable(LocalMap map, LocalCoord coord)
    {
        if (!map.Contains(coord))
        {
            return false;
        }

        int tileIndex = map.GetIndex(coord.X, coord.Y);
        return (map.Flags[tileIndex] & TileFlags.BlocksMovement) == 0;
    }

    private static LocalCoord? PickEncounterPosition(ulong worldSeed, LocalMap map, LocalCoord player)
    {
        const uint salt = 0xE17C0001;
        return PickPredatorPosition(worldSeed, map, player, salt, minDistance: 3, maxDistance: 8);
    }

    private static LocalCoord? PickPressurePredatorPosition(ulong worldSeed, LocalMap map, LocalCoord player)
    {
        const uint salt = 0xE55E550E;
        return PickPredatorPosition(worldSeed, map, player, salt, minDistance: 4, maxDistance: 10);
    }

    private static LocalCoord? PickPredatorPosition(
        ulong worldSeed,
        LocalMap map,
        LocalCoord player,
        uint salt,
        int minDistance,
        int maxDistance)
    {
        var candidates = new List<LocalCoord>();

        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                var coord = new LocalCoord(x, y);
                if (!IsTerrainWalkable(map, coord))
                {
                    continue;
                }

                int distance = Math.Abs(x - player.X) + Math.Abs(y - player.Y);
                if (distance < minDistance || distance > maxDistance)
                {
                    continue;
                }

                if (map.Entities.GetAt(coord) is not null)
                {
                    continue;
                }

                candidates.Add(coord);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        ulong hash = SeedUtility.Derive(worldSeed, map.WorldPosition.X, map.WorldPosition.Y, salt);
        int index = (int)(hash % (ulong)candidates.Count);
        return candidates[index];
    }
}
