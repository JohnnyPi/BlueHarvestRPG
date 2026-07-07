using System.Text.Json;
using Game.Simulation.AI;
using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.Factions;
using Game.Simulation.LocalMaps;
using Game.Simulation.Time;
using Game.Simulation.World;

namespace Game.Persistence.Serialization;

public sealed class EntitySaveData
{
    public ulong Id { get; init; }
    public int Kind { get; init; }
    public int LocalX { get; init; }
    public int LocalY { get; init; }
    public bool BlocksMovement { get; init; }
    public bool IsActive { get; init; }
    public int? Faction { get; init; }
    public int? Speed { get; init; }
    public int? Energy { get; init; }
    public int? EnergyRemainder { get; init; }
    public int? MaxHealth { get; init; }
    public int? Health { get; init; }
    public int? RaptorPhase { get; init; }
    public int? RaptorAmbushCooldown { get; init; }
    public bool? RaptorAnnouncedFenceProbe { get; init; }
}

public sealed class LocalMapSaveData
{
    public int WorldX { get; init; }
    public int WorldY { get; init; }
    public int StructureInstanceId { get; init; }
    public int FloorIndex { get; init; }
    public ushort[] Terrain { get; init; } = [];
    public byte[] Flags { get; init; } = [];
    public bool[]? Explored { get; init; }
    public List<EntitySaveData>? Entities { get; init; }
}

public sealed class CharacterProgressSaveData
{
    public int Level { get; init; } = 1;
    public int Experience { get; init; }
    public List<AttributeSaveData> Attributes { get; init; } = [];
}

public sealed class AttributeSaveData
{
    public string Id { get; init; } = string.Empty;
    public int Value { get; init; }
}

public sealed class RunScenarioSaveData
{
    public string Mission { get; init; } = string.Empty;
    public string StartLocation { get; init; } = string.Empty;
    public string EscapeRoute { get; init; } = string.Empty;
    public string Obstacle1 { get; init; } = string.Empty;
    public string Obstacle2 { get; init; } = string.Empty;
    public string Mystery { get; init; } = string.Empty;
    public string FirstEncounter { get; init; } = string.Empty;
    public string IslandSecret { get; init; } = string.Empty;
    public int? EscapeTargetX { get; init; }
    public int? EscapeTargetY { get; init; }
    public int? MysteryTargetX { get; init; }
    public int? MysteryTargetY { get; init; }
    public int? Obstacle1TargetX { get; init; }
    public int? Obstacle1TargetY { get; init; }
    public int? Obstacle2TargetX { get; init; }
    public int? Obstacle2TargetY { get; init; }
    public string? EscapeLandmark { get; init; }
    public string? MysteryLandmark { get; init; }
}

public sealed class MovementStepSaveData
{
    public int X { get; init; }
    public int Y { get; init; }
}

public sealed class WorldSaveData
{
    public const int FormatVersion = 9;

    public int FormatVersionNumber { get; init; } = FormatVersion;
    public uint GeneratorVersion { get; init; }
    public uint BiomeRulesHash { get; init; }
    public ulong Seed { get; init; }
    public int ViewMode { get; init; }
    public int PlayerWorldX { get; init; }
    public int PlayerWorldY { get; init; }
    public int PlayerLocalX { get; init; }
    public int PlayerLocalY { get; init; }
    public int? PlayerSpeed { get; init; }
    public int? PlayerEnergy { get; init; }
    public int? PlayerEnergyRemainder { get; init; }
    public int? PlayerHealth { get; init; }
    public int? PlayerMaxHealth { get; init; }
    public long? WorldTime { get; init; }
    public List<string> MessageLog { get; init; } = [];
    public List<MovementStepSaveData> MovementPath { get; init; } = [];
    public bool? EnterOnArrival { get; init; }
    public bool? TransitionOnArrival { get; init; }
    public int? TransitionEdgeOnArrival { get; init; }
    public int? TransitionBorderX { get; init; }
    public int? TransitionBorderY { get; init; }
    public bool[]? OverworldExplored { get; init; }
    public int? IslandPressure { get; init; }
    public int? PressureLastEventThreshold { get; init; }
    public int? PressureTravelPenalty { get; init; }
    public int? EvacHoursRemaining { get; init; }
    public bool? PendingPredatorSpawn { get; init; }
    public bool? MissedEvacuation { get; init; }
    public int? HazardousTravelX { get; init; }
    public int? HazardousTravelY { get; init; }
    public RunScenarioSaveData? RunScenario { get; init; }
    public List<LocalMapSaveData> LocalMaps { get; init; } = [];
    public List<ItemStackSaveData> Inventory { get; init; } = [];
    public List<QuestProgressSaveData> QuestProgress { get; init; } = [];
    public CharacterProgressSaveData? CharacterProgress { get; init; }
    public int? RunOutcome { get; init; }
    public int? EscapeEnding { get; init; }
    public string? RunEndTitle { get; init; }
    public string? RunEndSummary { get; init; }
    public List<int> FinaleThreats { get; init; } = [];
    public bool? FirstEncounterTriggered { get; init; }
    public int? ActiveStructureInstanceId { get; init; }
    public int? ActiveFloorIndex { get; init; }
}

public sealed class ItemStackSaveData
{
    public int ItemId { get; init; }
    public int Count { get; init; }
}

public sealed class QuestProgressSaveData
{
    public string QuestId { get; init; } = string.Empty;
    public int State { get; init; }
    public int Progress { get; init; }
}

public static class LocalMapSerializer
{
    public static LocalMapSaveData ToSaveData(LocalMap map)
    {
        var terrain = new ushort[map.Terrain.Length];
        var flags = new byte[map.Flags.Length];

        for (int i = 0; i < map.Terrain.Length; i++)
        {
            terrain[i] = (ushort)map.Terrain[i];
            flags[i] = (byte)map.Flags[i];
        }

        return new LocalMapSaveData
        {
            WorldX = map.WorldPosition.X,
            WorldY = map.WorldPosition.Y,
            StructureInstanceId = map.StructureInstanceId,
            FloorIndex = map.FloorIndex,
            Terrain = terrain,
            Flags = flags,
            Explored = map.Explored.ToArray(),
            Entities = map.Entities.All
                .Select(entity => new EntitySaveData
                {
                    Id = entity.Id.Value,
                    Kind = (int)entity.Kind,
                    LocalX = entity.LocalPosition.X,
                    LocalY = entity.LocalPosition.Y,
                    BlocksMovement = entity.BlocksMovement,
                    IsActive = entity.IsActive,
                    Faction = (int)entity.Faction,
                    Speed = entity.Actor?.Speed,
                    Energy = entity.Actor?.Energy,
                    EnergyRemainder = entity.Actor?.EnergyRemainder,
                    MaxHealth = entity.MaxHealth,
                    Health = entity.Health,
                    RaptorPhase = entity.Raptor is null ? null : (int)entity.Raptor.Phase,
                    RaptorAmbushCooldown = entity.Raptor?.AmbushCooldown,
                    RaptorAnnouncedFenceProbe = entity.Raptor?.AnnouncedFenceProbe
                })
                .ToList()
        };
    }

    public static LocalMap FromSaveData(LocalMapSaveData data, Overworld? world = null)
    {
        ValidateMapData(data);

        var key = new MapKey(
            new WorldCoord(data.WorldX, data.WorldY),
            data.StructureInstanceId,
            data.FloorIndex);
        var map = new LocalMap(key);

        for (int i = 0; i < map.Terrain.Length; i++)
        {
            map.Terrain[i] = (TerrainId)data.Terrain[i];
            map.Flags[i] = (TileFlags)data.Flags[i];
        }

        if (data.Explored is not null && data.Explored.Length == map.Explored.Length)
        {
            Array.Copy(data.Explored, map.Explored, map.Explored.Length);
        }

        if (data.Entities is not null)
        {
            map.Entities.ReplaceAll(data.Entities.Select(entityData => ToEntity(entityData, map.WorldPosition)));
        }
        else if (world is not null)
        {
            EntityFactory.SpawnDefaults(world, map);
        }

        return map;
    }

    private static void ValidateMapData(LocalMapSaveData data)
    {
        int expectedLength = LocalMap.Width * LocalMap.Height;
        if (data.Terrain.Length != expectedLength || data.Flags.Length != expectedLength)
        {
            throw new InvalidDataException("Map terrain or flags length is invalid.");
        }

        foreach (ushort terrain in data.Terrain)
        {
            if (!Enum.IsDefined(typeof(TerrainId), (TerrainId)terrain))
            {
                throw new InvalidDataException($"Unknown terrain id {terrain}.");
            }
        }
    }

    private static Entity ToEntity(EntitySaveData data, WorldCoord worldPosition)
    {
        var entity = new Entity
        {
            Id = new EntityId(data.Id),
            Kind = (EntityKind)data.Kind,
            WorldPosition = worldPosition,
            LocalPosition = new LocalCoord(data.LocalX, data.LocalY),
            BlocksMovement = data.BlocksMovement,
            IsActive = data.IsActive,
            Faction = data.Faction is int factionValue && Enum.IsDefined(typeof(FactionId), factionValue)
                ? (FactionId)factionValue
                : ((EntityKind)data.Kind).DefaultFaction(),
            MaxHealth = data.MaxHealth ?? 0,
            Health = data.Health ?? 0
        };

        if (data.Speed.HasValue || data.Energy.HasValue || data.EnergyRemainder.HasValue)
        {
            entity.Actor = new ActorTurnState
            {
                Speed = data.Speed ?? 100,
                Energy = data.Energy ?? ActionCostTable.ActionThreshold,
                EnergyRemainder = data.EnergyRemainder ?? 0
            };
        }

        if (entity.Kind == EntityKind.Raptor)
        {
            entity.Raptor = new RaptorMemory
            {
                Phase = data.RaptorPhase.HasValue && Enum.IsDefined(typeof(RaptorPhase), data.RaptorPhase.Value)
                    ? (RaptorPhase)data.RaptorPhase.Value
                    : RaptorPhase.Stalk,
                AmbushCooldown = data.RaptorAmbushCooldown ?? 0,
                AnnouncedFenceProbe = data.RaptorAnnouncedFenceProbe ?? false
            };
        }

        return entity;
    }
}

public static class SaveJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize(WorldSaveData data)
    {
        return JsonSerializer.Serialize(data, Options);
    }

    public static WorldSaveData Deserialize(string json)
    {
        return JsonSerializer.Deserialize<WorldSaveData>(json, Options)
            ?? throw new InvalidDataException("Save file was empty or invalid.");
    }
}
