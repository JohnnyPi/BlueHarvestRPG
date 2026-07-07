using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.Rendering;
using Game.Simulation.Scenarios;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation.World.Island;

public static class OverworldLandmarkCatalog
{
    private static readonly (IslandCellRole Role, string Name)[] LandmarkRoles =
    [
        (IslandCellRole.VisitorCenter, "Visitor Center"),
        (IslandCellRole.Dock, "Dock"),
        (IslandCellRole.Helipad, "Helipad"),
        (IslandCellRole.Maintenance, "Maintenance compound"),
        (IslandCellRole.Hotel, "Hotel"),
        (IslandCellRole.Restaurant, "Restaurant"),
        (IslandCellRole.Attraction, "Attraction"),
        (IslandCellRole.Ruin, "Ruins"),
        (IslandCellRole.Fortification, "Fortification"),
        (IslandCellRole.Paddock, "Paddock"),
        (IslandCellRole.Tunnel, "Tunnel entrance"),
        (IslandCellRole.Cavern, "Cavern"),
    ];

    public static string GetName(IslandPlan plan, int x, int y)
    {
        if (!plan.Contains(x, y))
        {
            return string.Empty;
        }

        if (plan.VisitorCenterCell.X == x && plan.VisitorCenterCell.Y == y)
        {
            return "Visitor Center";
        }

        ref IslandCellData cell = ref plan.GetCell(x, y);
        foreach ((IslandCellRole role, string name) in LandmarkRoles)
        {
            if (role == IslandCellRole.VisitorCenter)
            {
                continue;
            }

            if (cell.Role.HasFlag(role))
            {
                return name;
            }
        }

        foreach (VolcanicSite site in plan.VolcanicSites)
        {
            if (site.X == x && site.Y == y)
            {
                return site.Origin switch
                {
                    VolcanicOrigin.MantlePlume => "Hotspot vent",
                    VolcanicOrigin.SubductionArc => "Volcanic arc",
                    VolcanicOrigin.RiftVolcano => "Rift volcano",
                    VolcanicOrigin.CollisionVolcanism => "Collision peak",
                    _ => "Volcanic vent"
                };
            }
        }

        return string.Empty;
    }

    public static IReadOnlyList<OverworldLandmark> CollectExploredLandmarks(Overworld overworld, RunScenario? scenario)
    {
        if (overworld.IslandPlan is null)
        {
            return [];
        }

        IslandPlan plan = overworld.IslandPlan;
        var landmarks = new List<OverworldLandmark>();
        var nameCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                var coord = new WorldCoord(x, y);
                if (!overworld.Explored[overworld.GetIndex(coord)])
                {
                    continue;
                }

                string baseName = GetName(plan, x, y);
                if (baseName.Length == 0)
                {
                    continue;
                }

                int count = nameCounts.GetValueOrDefault(baseName) + 1;
                nameCounts[baseName] = count;
                string displayName = count > 1 ? $"{baseName} {count}" : baseName;

                landmarks.Add(new OverworldLandmark(
                    coord.X,
                    coord.Y,
                    displayName,
                    ResolveObjectiveKind(scenario, coord)));
            }
        }

        return landmarks;
    }

    public static bool TryResolveEntryPoint(
        IslandPlan plan,
        WorldCoord worldCell,
        LocalMap map,
        out LocalCoord entryPoint)
    {
        entryPoint = default;

        StructurePlacement? structure = FindStructureAt(plan, worldCell);
        if (structure is not null)
        {
            var blueprint = StructureBlueprintCatalogDefaults.Create().ResolveById(structure.BlueprintId);
            LocalCoord door = StructurePlacementQueries.ToLocalCoord(
                worldCell,
                structure,
                blueprint.DoorX,
                blueprint.DoorY);

            if (structure.Type == StructureType.Helipad)
            {
                door = StructurePlacementQueries.ToLocalCoord(
                    worldCell,
                    structure,
                    blueprint.DoorX,
                    blueprint.DoorY);
            }

            if (map.Contains(door))
            {
                entryPoint = WalkabilityHelper.FindNearestWalkable(map, door);
                return true;
            }
        }

        RuinSite? ruin = FindRuinAt(plan, worldCell);
        if (ruin is not null)
        {
            if (TryResolveRuinEntry(ruin, worldCell, map, out entryPoint))
            {
                return true;
            }
        }

        return false;
    }

    private static OverworldLandmarkObjectiveKind ResolveObjectiveKind(RunScenario? scenario, WorldCoord coord)
    {
        if (scenario is null)
        {
            return OverworldLandmarkObjectiveKind.None;
        }

        if (scenario.EscapeTarget == coord)
        {
            return OverworldLandmarkObjectiveKind.Escape;
        }

        if (scenario.MysteryTarget == coord)
        {
            return OverworldLandmarkObjectiveKind.Mystery;
        }

        return OverworldLandmarkObjectiveKind.None;
    }

    private static StructurePlacement? FindStructureAt(IslandPlan plan, WorldCoord worldCell)
    {
        foreach (StructurePlacement structure in plan.Structures)
        {
            if (CoordinateMath.OverlapsCell(
                    structure.GlobalOriginX,
                    structure.GlobalOriginY,
                    structure.Width,
                    structure.Height,
                    worldCell))
            {
                return structure;
            }
        }

        return null;
    }

    private static RuinSite? FindRuinAt(IslandPlan plan, WorldCoord worldCell)
    {
        foreach (RuinSite ruin in plan.RuinSites)
        {
            if (CoordinateMath.OverlapsCell(
                    ruin.GlobalOriginX,
                    ruin.GlobalOriginY,
                    ruin.Width,
                    ruin.Height,
                    worldCell))
            {
                return ruin;
            }
        }

        return null;
    }

    private static bool TryResolveStructureEntry(
        StructurePlacement structure,
        WorldCoord worldCell,
        LocalMap map,
        out LocalCoord entryPoint)
    {
        entryPoint = default;

        int doorGlobalX = structure.GlobalOriginX + structure.Width / 2;
        int doorGlobalY = structure.GlobalOriginY + structure.Height - 1;

        if (structure.Type == StructureType.Helipad)
        {
            doorGlobalX = structure.GlobalOriginX + structure.Width / 2;
            doorGlobalY = structure.GlobalOriginY + structure.Height / 2;
        }
        else if (structure.Type == StructureType.Dock)
        {
            doorGlobalX = structure.GlobalOriginX + structure.Width / 2;
            doorGlobalY = structure.GlobalOriginY + structure.Height - 1;
        }

        return TryGlobalToLocal(worldCell, map, doorGlobalX, doorGlobalY, out entryPoint);
    }

    private static bool TryResolveRuinEntry(
        RuinSite ruin,
        WorldCoord worldCell,
        LocalMap map,
        out LocalCoord entryPoint)
    {
        int entryGlobalX = ruin.GlobalOriginX + ruin.Width / 2;
        int entryGlobalY = ruin.GlobalOriginY + ruin.Height - 1;
        return TryGlobalToLocal(worldCell, map, entryGlobalX, entryGlobalY, out entryPoint);
    }

    private static bool TryGlobalToLocal(
        WorldCoord worldCell,
        LocalMap map,
        int globalX,
        int globalY,
        out LocalCoord entryPoint)
    {
        int localX = globalX - worldCell.X * LocalMap.Width;
        int localY = globalY - worldCell.Y * LocalMap.Height;
        var coord = new LocalCoord(localX, localY);

        if (!map.Contains(coord))
        {
            entryPoint = default;
            return false;
        }

        entryPoint = WalkabilityHelper.FindNearestWalkable(map, coord);
        return true;
    }
}

public readonly record struct OverworldLandmark(
    int X,
    int Y,
    string Name,
    OverworldLandmarkObjectiveKind ObjectiveKind);
